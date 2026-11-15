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

        // 18-bot 7-1 — 상대는 잃고(찹츄에서 멀어지면 더) 나는 얻는다
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            receivers.Sum(actor => view.HandScore(actor, -Math.Min(count, view.Server.HandCountOf(actor))));

        internal override int BotHandGain(Bots.BotView view, int[] receivers) =>
            receivers.Where(actor => actor != view.Bot).Sum(actor => Math.Min(count, view.Server.HandCountOf(actor)));
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.StealRandom(actor, entry.User, count);
        }
    }
}
