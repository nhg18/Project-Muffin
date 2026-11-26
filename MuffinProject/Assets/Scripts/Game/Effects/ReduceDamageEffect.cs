using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드에서 이 카운터를 낸 사람이 받을 피해를 amount 줄인다 (ReduceDamage — C10).</summary>
    [Serializable]
    public sealed class ReduceDamageEffect : CardEffect
    {
        public int amount;
        // 켜면 반응한 카드의 피해를 받는 사람 모두에게서 줄인다 — 카드 전체형 (2001 침착). 끄면 이 카운터를 낸 사람 몫만 (C10 · 2016 칼집)
        public bool allReceivers;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (allReceivers)
                server.MarkBelow(entry, below => below.DamageReductionAll += amount);
            else
                server.MarkBelow(entry, below => below.DamageReduction[entry.User] = (below.DamageReduction.TryGetValue(entry.User, out int r) ? r : 0) + amount);
        }
    }
}
