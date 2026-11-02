using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Card.cs — 카드 사용 처리 (04-card.md). 카드 한 장의 규칙값은 Card 가 들고 있다
    public partial class GameServer
    {
        /// <summary>
        /// 행동 카드 사용 (04-card.md 5절). 검증 → 손패에서 버림 더미로 → 효과 → 턴 넘김.
        /// 요청은 인스턴스 ID 만 받는다 — 무슨 카드인지는 손패에서 찾은 장이 안다 (09-network.md 7절).
        /// 반응 5초 · 카운터는 기능 5 — 지금은 바로 처리한다.
        /// </summary>
        public IReadOnlyList<GameEvent> PlayCard(int requester, int cardInstanceId, int[] requestedTargets)
        {
            if (requester != CurrentTurnActor)
                return Reject(requester, RejectCode.NotYourTurn);

            List<Card> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotInHand);

            Card card = hand[index];
            if (card.Type != CardType.Action)
                return Reject(requester, RejectCode.NotActionCard);

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets);
            if (targets == null)
                return Reject(requester, RejectCode.InvalidTarget);

            // 적용 — 검증이 모두 끝난 뒤에만 상태를 바꾼다. 카드가 먼저 나오고, 효과가 뒤따른다.
            hand.RemoveAt(index);
            _discardPile.Add(card);
            _events.Add(new CardPlayed(requester, card.InstanceId, card.CardId, targets, hand.Count, _discardPile.Count));

            foreach (CardEffect effect in card.Effects)
                effect.Apply(this, requester, targets);

            AdvanceTurn(); // 행동 카드 사용은 메인 행동 (03-turn.md 3절)
            return TakeEvents();
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
            if (requested == null || requested.Length != count)
                return null;

            if (requested.Distinct().Count() != count)
                return null;

            if (!requested.All(others.Contains))
                return null;

            return requested;
        }
    }
}
