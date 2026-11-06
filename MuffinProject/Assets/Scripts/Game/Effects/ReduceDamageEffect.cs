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

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.MarkBelow(entry, below => below.DamageReduction[entry.User] = (below.DamageReduction.TryGetValue(entry.User, out int r) ? r : 0) + amount);
        }
    }
}
