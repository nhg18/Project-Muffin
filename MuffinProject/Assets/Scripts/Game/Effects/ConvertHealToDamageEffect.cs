using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드에서 나 아닌 사람이 받을 회복을 같은 수치 피해로 (ConvertHealToDamage — C09).</summary>
    [Serializable]
    public sealed class ConvertHealToDamageEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.MarkBelow(entry, below =>
            {
                foreach (int actor in server.AffectedBy(below, EffectTag.Heal).Where(a => a != entry.User).ToArray())
                    below.HealToDamage.Add(actor);
            });
        }
    }
}
