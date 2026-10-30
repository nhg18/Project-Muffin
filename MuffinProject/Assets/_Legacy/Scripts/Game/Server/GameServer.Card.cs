using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    // GameServer.Card.cs — 카드 사용 처리 (04-card.md). 카드 한 장의 규칙값은 Card 가 들고 있다
    public partial class GameServer
    {
        /// <summary>
        /// 행동 카드 사용 (04-card.md 5절). 검사 → 손패에서 버림 더미로 → 효과 → 알림 → 턴 넘김.
        /// 요청은 인스턴스 ID 만 받는다 — 무슨 카드인지는 손패에서 찾은 장이 안다 (09-network.md 7절).
        /// 반응 5초 · 카운터는 기능 5 — 지금은 바로 처리한다.
        /// </summary>
        public void PlayCard(int requester, int cardInstanceId, int[] requestedTargets)
        {
            if (requester != CurrentTurnActor)
            {
                _outbox.Reject(requester, RejectCode.NotYourTurn);
                return;
            }

            // 요청자 손패에 그 장이 있는가 (존재 · 소유 검증)
            List<Card> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
            {
                _outbox.Reject(requester, RejectCode.NotInHand);
                return;
            }

            Card card = hand[index];
            if (card.Type != CardType.Action)
            {
                _outbox.Reject(requester, RejectCode.NotActionCard);
                return;
            }

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets);
            if (targets == null)
            {
                _outbox.Reject(requester, RejectCode.InvalidTarget);
                return;
            }

            // 적용 — 검사가 모두 끝난 뒤에만 상태를 바꾼다
            hand.RemoveAt(index);
            _discardPile.Add(card);

            foreach (CardEffect effect in card.Effects)
                ApplyEffect(requester, targets, effect);

            // 내보내기 — 일어난 사실만. 전원에게 (누가 · 어느 장 · 무슨 카드 · 누구에게)
            _outbox.SendCardUsed(requester, card.InstanceId, card.CardId, targets);
            _outbox.SetPlayerState(requester, PlayerProps.HandCount, hand.Count);
            _outbox.SetRoomState(RoomProps.DiscardCount, _discardPile.Count);

            // 행동 카드 사용은 메인 행동 — 끝나면 턴 종료 (03-turn.md 3절)
            AdvanceTurn();
        }

        // 효과 하나를 적용한다. 받는 사람은 효과마다 — 카드의 대상이거나 쓴 사람 본인.
        // 감소 · 무효 · 전환을 모은 뒤 1회 반영(06-health.md 4절)은 아직 — 지금은 바로 적용한다.
        private void ApplyEffect(int requester, int[] targets, CardEffect effect)
        {
            int[] receivers = effect.subject == EffectSubject.Self ? new[] { requester } : targets;

            switch (effect.type)
            {
                case EffectType.Damage:
                    foreach (int actor in receivers)
                        SetHp(actor, _players[actor].Hp - effect.amount);
                    break;
            }
        }

        // 대상 타입에 맞게 대상을 정한다. 자동 타입은 서버가 정하고, 고르는 타입은 요청을 검사한다. 맞지 않으면 null.
        // 살아 있는 사람 = 턴 순서에 있는 사람 (생존 상태는 기능 7).
        private int[] ResolveTargets(int requester, TargetType type, int[] requested)
        {
            int[] others = _turnOrder.Where(actor => actor != requester).ToArray();

            switch (type)
            {
                case TargetType.None:      return new int[0];
                case TargetType.Self:      return new[] { requester };
                case TargetType.AllOthers: return others;
                case TargetType.Everyone:  return _turnOrder.ToArray();
                case TargetType.OneOther:  return PickOthers(requested, 1, others);
                case TargetType.TwoOthers: return PickOthers(requested, 2, others);
                default:                   return null;
            }
        }

        // 고른 대상이 정확히 count 명이고, 서로 다르고, 모두 나를 뺀 사람인지 본다.
        private static int[] PickOthers(int[] requested, int count, int[] others)
        {
            if (requested == null || requested.Length != count) return null;
            if (requested.Distinct().Count() != count) return null;
            if (!requested.All(others.Contains)) return null;

            return requested;
        }
    }
}
