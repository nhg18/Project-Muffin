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

            if (!IsInGame(requester) || _players[requester].Life != LifeState.Alive)
                return Reject(requester, RejectCode.NotAlive); // 사망 대기 중에도 설치는 못 한다 (06 3절)

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
            AfterRecord after = null;
            switch (card.Trigger)
            {
                case TrapTrigger.ReactToCard:
                    if (!hasChain)
                        return Reject(requester, RejectCode.NotReactionTime);

                    ChainEntry top = _chain[_chain.Count - 1];
                    if (!CanReactTo(card, requester, top))
                        return Reject(requester, RejectCode.CannotReact);

                    source = top.User;
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
                    // 「~한 후」 함정 — 함정 시간에, 맞는 사건 기록이 있을 때 (04 6절)
                    if (!IsAfterTrigger(card.Trigger) || hasChain || !IsTrapWindowOpen || now < _trapWindowOpensAt)
                        return Reject(requester, RejectCode.TrapConditionNotMet);

                    after = FindAfterRecord(requester, card);
                    if (after == null)
                        return Reject(requester, RejectCode.TrapConditionNotMet);

                    source = TriggerTargetOf(after);
                    break;
            }

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets, source, fromHand: false);
            if (targets == null)
                return Reject(requester, RejectCode.InvalidTarget);

            UpdateTurnClock(now); // 멈추기 직전까지의 턴 시간을 정산 — 새 체인이 턴을 멈춘다
            player.Traps.RemoveAt(index);
            if (after != null)
                _afterRecords.Remove(after); // 사건 한 번에 함정 한 번
            _trapWindowDeadline = -1;        // 함정이 나오면 함정 시간은 끝 — 그 체인을 처리한 뒤 다시 확인한다 (04 6절)

            ChainEntry entry = AddToChain(card, requester, targets, fromTrapSlot: true);
            entry.TriggerAmount = after?.Amount ?? 0;
            return TakeEvents();
        }
    }
}
