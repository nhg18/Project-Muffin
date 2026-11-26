using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드에서 이 카운터를 낸 사람이 받을 피해를 amount 줄인다 (ReduceDamage — C10 · 2016). everyone 이면 그 카드의 피해를 받는 사람 모두 각각 (2001).</summary>
    [Serializable]
    public sealed class ReduceDamageEffect : CardEffect
    {
        public int amount;
        public bool everyone;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (everyone)
            {
                server.MarkBelow(entry, below => below.DamageReductionAll += amount);
                return;
            }

            server.MarkBelow(entry, below => below.DamageReduction[entry.User] = (below.DamageReduction.TryGetValue(entry.User, out int r) ? r : 0) + amount);
        }
    }
}
