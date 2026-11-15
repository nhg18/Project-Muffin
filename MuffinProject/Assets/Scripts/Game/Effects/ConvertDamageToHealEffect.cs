using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드에서 이 카운터를 낸 사람이 받을 피해를 같은 수치 회복으로 (ConvertDamageToHeal — C03).</summary>
    [Serializable]
    public sealed class ConvertDamageToHealEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.MarkBelow(entry, below => below.DamageToHeal.Add(entry.User));
    }
}
