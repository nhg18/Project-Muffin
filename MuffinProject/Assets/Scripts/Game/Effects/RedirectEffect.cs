using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드의 대상 전체를 새 1명으로 (Redirect — C02 = 이 카운터가 고른 사람, C04 = 그 카드의 시전자).</summary>
    [Serializable]
    public sealed class RedirectEffect : CardEffect
    {
        // 켜면 시전자에게 (C04), 끄면 이 카운터의 대상에게 (C02)
        public bool toCaster;

        internal override void Apply(GameServer server, ChainEntry entry) => server.RedirectBelow(entry, toCaster);
    }
}
