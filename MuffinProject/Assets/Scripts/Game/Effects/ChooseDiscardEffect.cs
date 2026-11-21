using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 이 카드를 쓴 사람이 자기 손패에서 1장을 골라 버린다 — 카운터의 대가 (2013 새치기 · 2021 으르렁, 11-card-list 1-3 「처리 중 손패 고르기」).
    /// seconds 안에 안 고르면 방장이 무작위 1장. 손패가 0장이면 버리지 않는다. 고르는 동안 처리가 멈춘다 (04-card.md 9.3).
    /// 봇은 함정 → 카운터 → 행동 순으로 있는 종류의 뒤쪽 카드를 버린다 (18-bot.md 7-4).
    /// </summary>
    [Serializable]
    public sealed class ChooseDiscardEffect : CardEffect
    {
        public float seconds = 5f;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (server.InGame(entry.User) && server.HandCountOf(entry.User) > 0)
                server.BeginHandChoice(entry, this, entry.User);
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
