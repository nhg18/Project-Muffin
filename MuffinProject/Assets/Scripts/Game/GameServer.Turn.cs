using System.Collections.Generic;

namespace Chapchu.Game
{
    // GameServer.Turn.cs — 턴 순서 · 턴 넘기기 · 제한 시간 (03-turn.md)
    public partial class GameServer
    {
        // 확정 (03-turn.md 2절).
        private const double TurnTimeLimit = 20;

        private double _turnDeadline;
        private bool _mainActionUsed;     // 이번 턴에 메인 행동을 했다 — 체인 처리 · 후처리 연출이 끝날 때까지 (03-turn.md 3 · 4절)
        private double _nextTurnAt = -1;  // 메인 행동의 체인을 처리했고, 이 시각에 다음 턴 (04-card.md 8절). 음수면 없음
        private double _resumeAt = -1;    // 메인 행동 전에 끼어든 체인(함정)의 후처리 연출이 끝나는 시각 — 그때까지 턴이 멈춰 있다
        private bool _turnPaused;         // 메인 행동 전인데 체인 · 함정 시간 · 사망 대기로 턴 제한 시간이 멈췄다
        private double _lastTickAt;

        /// <summary>
        /// 방장이 매 프레임 부른다. 지금 멈춰 있는 것 하나를 시각에 따라 진행한다 —
        /// 반응 마감 → 체인 처리, 함정 시간 마감 → 닫기, 사망 대기 마감 → 최종 사망, 후처리 시간 → 다음 턴, 턴 마감 → 넘김 (03-turn.md 4절 — 자동 제출 없음).
        /// 메인 행동 전에 멈췄으면 턴 제한 시간을 그만큼 미루고, 풀리면 재개를 알린다 (03-turn.md 4절).
        /// </summary>
        public IReadOnlyList<GameEvent> Tick()
        {
            if (_gameOver)
                return TakeEvents();

            double now = _clock();
            Advance(now);
            UpdateTurnClock(now);
            return TakeEvents();
        }

        // 멈춰 있는 것을 순서대로 본다. 아직 기다려야 하면 거기서 멈추고, 끝났으면 다음으로 넘어간다 (한 프레임에 이어서 진행).
        private void Advance(double now)
        {
            if (_chain.Count > 0)
            {
                if (now < _reactionDeadline)
                    return;
                ResolveChain();
                if (_gameOver)
                    return; // 체인 중에 나가 1명만 남았다 — 처리 뒤 바로 끝, 턴을 넘기지 않는다 (07 3 · 6절)
            }

            if (IsTrapWindowOpen)
            {
                if (now < _trapWindowDeadline)
                    return;
                CloseTrapWindow();
                CheckElimination(); // 함정 시간 중에 나가 1명만 남았을 수 있다 (07 3절)
                if (_gameOver)
                    return;
            }

            if (IsDeathPending)
            {
                if (now < _deathDeadline)
                    return;
                FinalizeDeaths();
                CheckElimination();
                if (_gameOver)
                    return;
            }

            if (now < _resumeAt)
                return; // 끼어든 체인의 후처리 연출 중

            if (_nextTurnAt >= 0)
            {
                if (now >= _nextTurnAt)
                    AdvanceTurn();
            }
            else if (CurrentTurnActor != -1 && _turnPaused && !_mainActionUsed && !IsInGame(CurrentTurnActor))
            {
                AdvanceTurn(); // 멈춘 사이에 턴 주인이 나갔거나 최종 사망했다 — 바로 다음 사람에게 (03-turn.md 7절)
            }
            else if (CurrentTurnActor != -1 && now >= _turnDeadline)
            {
                AdvanceTurn();
            }
        }

        /// <summary>
        /// 나간 사람을 턴 순서 · 대상 후보에서 뺀다 (01-game-flow.md 6절 · 03-turn.md 7절). 턴 주인이었으면 다음 사람에게 넘긴다.
        /// 이미 카드를 냈으면 체인을 끝까지 처리하고 그 뒤에 넘어간다 (09-network.md 11절 「처리 중 요청자가 사망」).
        /// </summary>
        public IReadOnlyList<GameEvent> RemovePlayer(int actor)
        {
            if (!_players.TryGetValue(actor, out PlayerState player) || player.HasLeft)
                return TakeEvents();

            player.HasLeft = true;
            DiscardAll(actor);
            _events.Add(new PlayerLeft(actor, _discardPile.Count));
            if (_gameOver)
                return TakeEvents();

            CheckElimination(); // 체인 · 함정 시간 · 사망 대기가 없으면 바로 (07 3절). 있으면 그것이 끝난 뒤
            if (!_gameOver && actor == CurrentTurnActor && !_mainActionUsed && !IsPaused(_clock()))
                AdvanceTurn();

            return TakeEvents();
        }

        // 참가자 순서를 무작위로 섞어 턴 순서를 정하고 첫 사람에게 턴을 준다 (03-turn.md 6절).
        private void StartFirstTurn()
        {
            _turnOrder.Clear();
            _turnOrder.AddRange(_players.Keys);
            Shuffle(_turnOrder);

            SetTurn(_turnOrder[0]);
        }

        // 턴을 넘기는 곳은 여기 하나다 — 뽑기 · 카드 사용 · 시간 초과 · 나감이 모두 이걸 부른다.
        // 다음 사람이 찹츄 상태면 그 턴이 시작되는 순간 승리 (07 4.3 · 5절 — 가장 먼저 자기 턴이 돌아온 사람).
        // 「다음 턴 스킵」 이 걸린 사람은 건너뛴다 — 스킵된 턴은 시작하지 않은 것 (찹츄 판정 없음, 03-turn.md 7절). 스킵 직후 「턴 스킵된 후」 함정 시간.
        private void AdvanceTurn()
        {
            int next = GetNextActor(CurrentTurnActor);
            bool skipped = false;
            for (int guard = 0; guard < _turnOrder.Count && _players[next].SkipNextTurn; guard++)
            {
                _players[next].SkipNextTurn = false;
                _events.Add(new TurnSkipped(next));
                RecordAfter(AfterKind.TurnSkipped, next, -1, 1);
                skipped = true;
                next = GetNextActor(next);
            }

            if (_players[next].IsChapchu)
            {
                EndGame(next, GameEndReason.Chapchu);
                return;
            }

            SetTurn(next);
            if (skipped)
                OpenTrapWindowIfNeeded(_clock());
        }

        private void SetTurn(int actor)
        {
            CurrentTurnActor = actor;
            _lastTickAt = _clock();
            _turnDeadline = _lastTickAt + TurnTimeLimit;
            _mainActionUsed = false;
            _nextTurnAt = -1;
            _resumeAt = -1;
            _turnPaused = false;
            TurnSerial++;

            _events.Add(new TurnStarted(actor, _turnDeadline));

            // 다음 실제 턴 시작에 더 뽑기 (T15) — 메인 행동이 아니다
            int extra = _players[actor].TurnStartDraws;
            _players[actor].TurnStartDraws = 0;
            for (int i = 0; i < extra; i++)
                DrawOne(actor);
            CheckDeathPending(_clock()); // 빈 덱 피해로 HP 0 이면 바로 사망 대기 — 턴이 멈춘다 (06 8절)
        }

        // 체인 · 함정 시간 · 사망 대기 · 끼어든 체인의 후처리 연출 — 턴이 진행되지 않는다.
        private bool IsPaused(double now) => _chain.Count > 0 || IsTrapWindowOpen || IsDeathPending || now < _resumeAt;

        // 메인 행동 전에 멈춘 동안은 턴 마감을 미룬다. 풀리는 순간 남은 시간으로 다시 알린다 (03-turn.md 4절).
        private void UpdateTurnClock(double now)
        {
            if (!_gameOver && CurrentTurnActor != -1 && !_mainActionUsed)
            {
                if (IsPaused(now))
                {
                    _turnDeadline += now - _lastTickAt;
                    _turnPaused = true;
                }
                else if (_turnPaused && IsInGame(CurrentTurnActor))
                {
                    // 턴 주인이 빠졌으면 멈춘 채로 둔다 — 넘김은 Tick 의 Advance 가 한다 (요청 처리 중 정산은 턴을 바꾸지 않는다)
                    // 끼어든 체인의 후처리 연출이 이번 프레임 안에서 끝났으면 그 시각까지는 멈춘 것으로
                    _turnDeadline += System.Math.Max(0, System.Math.Min(now, _resumeAt) - _lastTickAt);
                    _turnPaused = false;
                    _events.Add(new TurnResumed(CurrentTurnActor, _turnDeadline));
                }
            }

            _lastTickAt = now;
        }

        // 턴 순서에서 다음으로 게임에 남은 사람(나감 · 최종 사망 제외). 지금 사람이 빠졌어도 그 자리를 기준으로 찾는다.
        private int GetNextActor(int currentActor)
        {
            int index = _turnOrder.IndexOf(currentActor);
            for (int step = 1; step <= _turnOrder.Count; step++)
            {
                int actor = _turnOrder[(index + step) % _turnOrder.Count];
                if (IsInGame(actor))
                    return actor;
            }

            return currentActor;
        }

        // 게임에 남은 사람 = 나가지 않았고 최종 사망하지 않았다. 사망 대기는 아직 남아 있다 (06 12절).
        private bool IsInGame(int actor) => !_players[actor].HasLeft && _players[actor].Life != LifeState.Dead;
    }
}
