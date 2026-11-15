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

        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self ? 0 : receivers.Sum(actor => view.HandScore(actor, -Math.Min(count, view.Server.HandCountOf(actor))));

        // 내 손패를 버리는 카드 (A21) — 낸 카드를 뺀 장수에서
        internal override int BotHandGain(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self ? -Math.Min(count, view.MyHand - 1) : 0;
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.DiscardRandom(actor, count, entry.User);
        }
    }
}
