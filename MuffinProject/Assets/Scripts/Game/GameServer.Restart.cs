using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Restart.cs — 112 대소동: 게임 새로 시작 · 피폭 피해 (11-card-list 112)
    public partial class GameServer
    {
        private readonly List<Card> _allCards = new List<Card>(); // 이 게임의 덱 전체 — 새로 시작할 때 다시 섞는다 (대소동 자신도 들어간다)
        private int _restartCount;      // 이 판에서 대소동을 쓴 횟수 n — 새로 시작해도 남는다
        private int _falloutTurns;      // 새로 시작한 뒤 피폭이 들어간 턴 수 k. _restartCount 가 0 이면 피폭 없음
        private bool _restartPending;   // 대소동이 처리됐다 — 지금 턴이 끝나면 피폭 없이 새 판
        private int _turnEndStage;      // 이번 턴 끝 처리를 어디까지 했나 — 0 아직 · 1 피폭 · 2 개판 세기. 그 사망 대기 · 판정을 기다렸다 다시 올 때 건너뛴다. SetTurn 이 푼다

        internal int RestartCount => _restartCount;
        internal int FalloutTurns => _falloutTurns;

        /// <summary>대소동 효과 (RestartEffect). 남은 처리는 버리고, 이 턴이 끝나면 새 판을 시작한다.</summary>
        internal void RequestRestart()
        {
            _restartPending = true;
            _resolving.Clear(); // 체인에 남은 것 · 그 뒤처리는 새 판에서 의미가 없다
        }

        // 턴이 끝나는 순간 (AdvanceTurn 첫머리) — 프로토타입 v2 turnEnd2 순서: 대소동이면 새 판 → 피폭 → 개판 세기.
        // 피폭 피해의 사망 대기 · 함정 시간, 개판 판정이 생기면 true (끝난 뒤 Advance 가 다시 오고, 한 단계는 한 번만).
        // 스킵된 턴은 여기를 지나지 않는다 — 피폭 없음 · k 도 그대로 (사용자 2026-11-13). 개판은 스킵된 턴도 센다 (AdvanceTurn).
        private bool HoldTurnEnd()
        {
            if (_restartPending)
            {
                Restart();
                return true;
            }

            if (CurrentTurnActor < 0)
                return false;

            if (_turnEndStage == 0)
            {
                _turnEndStage = 1;
                if (_restartCount > 0)
                {
                    _falloutTurns++;
                    int damage = FalloutDamage(_falloutTurns, _restartCount);
                    _events.Add(new FalloutChanged(_restartCount, _falloutTurns));
                    foreach (int actor in AliveActors())
                        DealDamage(actor, damage, -1);

                    double at = _clock();
                    CheckDeathPending(at);
                    OpenTrapWindowIfNeeded(at);
                    if (HoldForTurnEnd())
                        return true;
                }
            }

            if (_turnEndStage == 1)
            {
                _turnEndStage = 2;
                TickGaepan();
                if (HoldForTurnEnd())
                    return true;
            }

            return false;
        }

        // 이 턴은 끝났다 — 사망 · 함정 처리 · 개판 판정이 끝나면 Advance 가 다시 넘긴다 (남은 시간을 되살리지 않게 메인 행동을 쓴 것으로)
        private bool HoldForTurnEnd()
        {
            if (!IsDeathPending && !IsTrapWindowOpen && !IsGaepanJudging)
                return false;

            _mainActionUsed = true;
            _nextTurnAt = _clock();
            return true;
        }

        /// <summary>피폭 피해 k^n (11-card-list 112). 화면도 다음 턴 피해를 이 식으로 보인다.</summary>
        public static int FalloutDamage(int turns, int n) => (int)Math.Min(int.MaxValue, Math.Pow(turns, n));

        // 지금 방에 있는 사람(나간 사람 제외, 죽은 사람 포함)으로 처음처럼 — 대소동 횟수 · 통계 · 기록만 남는다 (11-card-list 112).
        private void Restart()
        {
            _restartPending = false;
            _restartCount++;
            int user = CurrentTurnActor; // 대소동은 행동 카드라 쓴 사람의 턴이 끝나며 새 판이 된다
            _falloutTurns = 0;

            int[] members = _turnOrder.Where(a => !_players[a].HasLeft).ToArray();
            foreach (int actor in members)
                _players[actor] = new PlayerState();

            _chain.Clear();
            _resolving.Clear();
            _deathDeadline = -1;
            _trapWindowDeadline = -1;
            _afterRecords.Clear();
            _waitCasters.Clear();
            _reversed = false;
            _extraTurnActor = -1;
            _heldNext = -1;
            ResetGaepan(); // 개판도 처음으로 (사용자 2026-11-13)
            InitDeck(_allCards);

            _turnOrder.Clear();
            _turnOrder.AddRange(members);
            Shuffle(_turnOrder);
            _events.Add(new GameRestarted(user, _restartCount, _turnOrder.ToArray()));

            foreach (int actor in _turnOrder)
                SetHp(actor, MaxHp);
            DealInitialHands(_turnOrder);
            _events.Add(new FalloutChanged(_restartCount, 0));

            CurrentTurnActor = -1; // 지난 판의 턴 주인 상태(행동 금지 해제 등)는 이미 지웠다
            SetTurn(_turnOrder[0]);
        }
    }
}
