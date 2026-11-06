using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상마다 손패에서 무작위 count 장을 쓴 사람에게 (Steal — A04 · T08).</summary>
    [Serializable]
    public sealed class StealEffect : CardEffect
    {
        public int count;

        internal override Bots.BotRole BotRole => Bots.BotRole.HandChange;
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.StealRandom(actor, entry.User, count);
        }
    }
}
