using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람의 찹츄 상태 제거 (ClearChapChu — A21).</summary>
    [Serializable]
    public sealed class ClearChapChuEffect : CardEffect
    {
        public EffectSubject subject;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.ClearChapchu(actor);
        }
    }
}
