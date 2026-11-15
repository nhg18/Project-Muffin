using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드를 효과 없이 시전자 손패로 (ReturnToHand — C01).</summary>
    [Serializable]
    public sealed class ReturnToHandEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.ReturnBelowToHand(entry);
    }
}
