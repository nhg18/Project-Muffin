using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 이 카드를 쓴 사람이 자기 손패에서 무작위 1장(2배면 2장)을 버린다 — 카운터의 대가 (2013 새치기 · 2021 으르렁, 11-card-list 1-3).
    /// 손패가 0장이면 버리지 않는다. 2026-11-28 PM 「선택하게 하는 것은 다 무작위로」 — 고르지 않는다 (이름 · seconds · BotPick 은 카드 에셋 · 고르기 흐름 때문에 둔다).
    /// </summary>
    [Serializable]
    public sealed class ChooseDiscardEffect : CardEffect
    {
        public float seconds = 5f;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (server.InGame(entry.User) && server.HandCountOf(entry.User) > 0)
                server.DiscardRandom(entry.User, server.Scaled(entry, 1), entry.User); // 내가 낸 대가 — 「버려진 후」 기록 없음 (DiscardFromHand 와 같다)
        }

        internal int BotPick(GameServer server, int bot)
        {
            List<Card> hand = server.StateOf(bot).Hand;
            foreach (CardType type in new[] { CardType.Trap, CardType.Counter, CardType.Action })
            {
                Card card = hand.LastOrDefault(c => c.Type == type);
                if (card != null)
                    return card.InstanceId;
            }
            return -1;
        }
    }
}
