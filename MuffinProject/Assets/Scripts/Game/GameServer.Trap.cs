using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Trap.cs — 함정 설치 · 발동 · 함정 시간 (04-card.md 6절)
    public partial class GameServer
    {
        // 확정 (04-card.md 2절) · 2026-11-06 PM 확정 (함정 시간 5초).
        internal const int TrapSlotCount = 3;
        private const double TrapWindowTime = 5;

        // 「피해 받은 후」 조건 — 피해를 받은 사람 → 마지막으로 피해를 준 사람. 체인 처리 뒤 함정 시간이 끝나면 비운다.
        private readonly Dictionary<int, int> _damagedBy = new Dictionary<int, int>();
        private double _trapWindowOpensAt;
        private double _trapWindowDeadline = -1;

        private bool IsTrapWindowOpen => _trapWindowDeadline >= 0;

        /// <summary>함정 설치 (04-card.md 6절). 내 턴 · 메인 행동 전 · 빈 슬롯이 있으면 몇 장이든. 메인 행동이 아니다. 종류는 소유자만 안다.</summary>
        public IReadOnlyList<GameEvent> SetTrap(int requester, int cardInstanceId)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            if (requester != CurrentTurnActor)
                return Reject(requester, RejectCode.NotYourTurn);

            if (_mainActionUsed)
                return Reject(requester, RejectCode.MainActionUsed);

            PlayerState player = _players[requester];
            int index = player.Hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotInHand);

            Card card = player.Hand[index];
            if (card.Type != CardType.Trap)
                return Reject(requester, RejectCode.NotTrapCard);

            if (player.Traps.Count >= TrapSlotCount)
                return Reject(requester, RejectCode.TrapSlotFull);

            player.Hand.RemoveAt(index);
            player.Traps.Add(card);
            _events.Add(new TrapSet(requester, card.InstanceId, card.CardId, player.Hand.Count, player.Traps.Count));
            OnHandChanged(requester);
            return TakeEvents();
        }

        /// <summary>
        /// 함정 발동 (04-card.md 6절 「발동 시점」). 소유자가 직접, 턴과 관계없이. 공개되고 체인에 오른다 — 체인이 없으면 새 체인의 맨 아래.
        /// 동시 요청은 도착 순서 — 먼저 온 것이 체인에 오르면 늦은 것은 연출 중이라 거절되고 슬롯에 남는다.
        /// </summary>
        public IReadOnlyList<GameEvent> ActivateTrap(int requester, int cardInstanceId, int[] requestedTargets)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            if (!IsInGame(requester))
                return Reject(requester, RejectCode.NotAlive);

            PlayerState player = _players[requester];
            int index = player.Traps.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotSetTrap);

            Card card = player.Traps[index];

            // 사망 대기 중엔 사망 관련 함정만 (06 3절)
            if (player.Life == LifeState.DeathPending && card.Trigger != TrapTrigger.SelfDeathPending)
                return Reject(requester, RejectCode.TrapConditionNotMet);

            double now = _clock();
            bool hasChain = _chain.Count > 0;
            bool reactionOpen = hasChain && now >= _reactionOpensAt && now < _reactionDeadline;
            if (hasChain && !reactionOpen)
                return Reject(requester, RejectCode.NotReactionTime); // 연출 중 · 마감 뒤

            int source = -1; // 발동시킨 사람 — 대상 TriggerSource
            switch (card.Trigger)
            {
                case TrapTrigger.ReactToCard:
                    if (!hasChain)
                        return Reject(requester, RejectCode.NotReactionTime);

                    ChainEntry top = _chain[_chain.Count - 1];
                    if (!card.ReactsTo.Contains(top.Card.Type))
                        return Reject(requester, RejectCode.CannotReact);

                    source = top.User;
                    break;

                case TrapTrigger.AfterDamaged:
                    if (hasChain || !IsTrapWindowOpen || now < _trapWindowOpensAt || !_damagedBy.TryGetValue(requester, out source))
                        return Reject(requester, RejectCode.TrapConditionNotMet);
                    break;

                case TrapTrigger.HpAtMost:
                    if (player.Hp > card.TriggerValue)
                        return Reject(requester, RejectCode.TrapConditionNotMet);
                    break;

                case TrapTrigger.SelfDeathPending:
                    if (player.Life != LifeState.DeathPending)
                        return Reject(requester, RejectCode.TrapConditionNotMet);
                    break;

                case TrapTrigger.ChapchuExists:
                    if (!_turnOrder.Any(a => a != requester && IsInGame(a) && _players[a].IsChapchu))
                        return Reject(requester, RejectCode.TrapConditionNotMet);
                    break;

                default:
                    return Reject(requester, RejectCode.TrapConditionNotMet);
            }

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets, source);
            if (targets == null)
                return Reject(requester, RejectCode.InvalidTarget);

            UpdateTurnClock(now); // 멈추기 직전까지의 턴 시간을 정산 — 새 체인이 턴을 멈춘다
            player.Traps.RemoveAt(index);
            if (card.Trigger == TrapTrigger.AfterDamaged)
                _damagedBy.Remove(requester); // 한 번의 피해에 한 번
            _trapWindowDeadline = -1;         // 함정이 나오면 함정 시간은 끝 — 그 체인을 처리한 뒤 다시 확인한다 (04 6절)

            AddToChain(card, requester, targets, fromTrapSlot: true);
            return TakeEvents();
        }

        // 피해가 들어갈 때 기록한다 — 「피해 받은 후」 함정의 조건 (04 6절). 다른 플레이어에게 받은 피해만.
        private void RecordDamage(int victim, int source)
        {
            if (source >= 0 && source != victim)
                _damagedBy[victim] = source;
        }

        // 체인을 처리한 뒤 부른다. 「피해 받은 후」 조건이 맞는 함정을 가진 사람이 있으면 후처리 연출 뒤 5초 함정 시간을 연다 (04 6절).
        private void OpenTrapWindowIfNeeded(double opensAt)
        {
            bool anyone = _damagedBy.Keys.Any(victim => IsInGame(victim) && _players[victim].Life == LifeState.Alive
                                                         && _players[victim].Traps.Any(c => c.Trigger == TrapTrigger.AfterDamaged));
            if (!anyone)
            {
                _damagedBy.Clear();
                return;
            }

            _trapWindowOpensAt = opensAt;
            _trapWindowDeadline = opensAt + TrapWindowTime;
            _events.Add(new TrapWindowStarted(_trapWindowOpensAt, _trapWindowDeadline));
        }

        // 아무도 내지 않고 함정 시간이 끝났다 — 그 기회는 사라진다.
        private void CloseTrapWindow()
        {
            _trapWindowDeadline = -1;
            _damagedBy.Clear();
        }
    }
}
