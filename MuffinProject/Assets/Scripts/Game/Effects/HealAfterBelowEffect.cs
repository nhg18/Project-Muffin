using System;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드의 효과를 다 적용한 직후에 이 카운터를 낸 사람이 amount 회복한다 (2001 침착 — 줄어든 피해를 받은 뒤 회복, 04-card.md 7절). 이 카운터가 무효되면 표시도 남지 않는다.</summary>
    [Serializable]
    public sealed class HealAfterBelowEffect : CardEffect
    {
        public int amount;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.MarkBelow(entry, below => below.HealAfter[entry.User] = (below.HealAfter.TryGetValue(entry.User, out int h) ? h : 0) + amount);
        }
    }
}
