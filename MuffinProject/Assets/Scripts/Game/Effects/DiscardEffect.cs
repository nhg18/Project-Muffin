using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람마다 손패에서 무작위 count 장을 버림 더미로 (Discard — A05 · A21 · A23 · T04 · T07 · T12 · T14).</summary>
    [Serializable]
    public sealed class DiscardEffect : CardEffect
    {
        public EffectSubject subject;
        public int count;

        internal override Bots.BotRole BotRole => subject == EffectSubject.Target ? Bots.BotRole.HandChange : Bots.BotRole.None;
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.DiscardRandom(actor, count, entry.User);
        }
    }
}
