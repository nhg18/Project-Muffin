using System.Collections.Generic;

namespace Chapchu.Game
{
    // GameServer.Turn.cs — 턴 순서 · 턴 넘기기 · 제한 시간 (03-turn.md)
    public partial class GameServer
    {
        // 확정 (03-turn.md 2절).
        private const double TurnTimeLimit = 20;

        private double _turnDeadline;
        private bool _mainActionUsed;     // 이번 턴에 카드를 냈다 — 체인 처리 · 후처리 연출이 끝날 때까지 (03-turn.md 3 · 4절)
        private double _nextTurnAt = -1;  // 체인을 처리했고, 이 시각에 다음 턴 (04-card.md 8절). 음수면 없음

        /// <summary>
        /// 방장이 매 프레임 부른다. 반응 마감 → 체인 처리, 후처리 시간 → 다음 턴, 턴 마감 → 넘김 (03-turn.md 4절 — 자동 제출 없음).
        /// 카드를 낸 뒤(Resolving)에는 턴 제한 시간이 멈춘다 (03-turn.md 4절).
        /// </summary>
        public IReadOnlyList<GameEvent> Tick()
        {
            double now = _clock();
            if (_chain.Count > 0)
            {
                if (now >= _reactionDeadline)
                    ResolveChain();
            }
            else if (_nextTurnAt >= 0)
            {
                if (now >= _nextTurnAt)
                    AdvanceTurn();
            }
            else if (CurrentTurnActor != -1 && now >= _turnDeadline)
            {
                AdvanceTurn();
            }

            return TakeEvents();
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
            if (actor == CurrentTurnActor && !_mainActionUsed)
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
        private void AdvanceTurn()
        {
            SetTurn(GetNextActor(CurrentTurnActor));
        }

        private void SetTurn(int actor)
        {
            CurrentTurnActor = actor;
            _turnDeadline = _clock() + TurnTimeLimit;
            _mainActionUsed = false;
            _nextTurnAt = -1;

            _events.Add(new TurnStarted(actor, _turnDeadline));
        }

        // 턴 순서에서 다음으로 게임에 남은 사람. 지금 사람이 나갔어도 그 자리를 기준으로 찾는다.
        // TODO: 사망한 사람도 건너뛴다 (기능 7) — IsInGame 에 더한다.
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

        private bool IsInGame(int actor) => !_players[actor].HasLeft;
    }
}
