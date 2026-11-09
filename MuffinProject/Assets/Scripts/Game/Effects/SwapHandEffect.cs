using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>손패 전체 교환 (SwapHand) — betweenTargets 면 대상 1 ↔ 대상 2 (A18), 아니면 쓴 사람 ↔ 대상 (A11). 본인 무효로 빠진 사람이 있으면 불발.</summary>
    [Serializable]
    public sealed class SwapHandEffect : CardEffect
    {
        public bool betweenTargets;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            int[] targets = server.ReceiversOf(EffectSubject.Target, entry);
            if (betweenTargets)
            {
                if (targets.Length >= 2)
                    server.SwapHands(targets[0], targets[1]);
            }
            else if (targets.Length >= 1 && !entry.Excluded.Contains(entry.User))
            {
                server.SwapHands(entry.User, targets[0]);
            }
        }
    }
}
