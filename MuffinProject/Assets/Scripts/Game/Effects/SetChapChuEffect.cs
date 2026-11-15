using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람에게 찹츄 부여 (SetChapChu — T16). 손패가 정확히 10장이 아니면 붙지 않는다 (07 7절).</summary>
    [Serializable]
    public sealed class SetChapChuEffect : CardEffect
    {
        public EffectSubject subject;

        internal override EffectTag Tag => EffectTag.GiveChapchu;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.GiveChapchu(actor, entry.User);
        }
    }
}
