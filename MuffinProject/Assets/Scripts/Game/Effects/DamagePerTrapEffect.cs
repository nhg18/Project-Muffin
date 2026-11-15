using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상의 설치 함정 1장당 amount 피해 (DamagePerTrap — A22).</summary>
    [Serializable]
    public sealed class DamagePerTrapEffect : CardEffect
    {
        public int amount;

        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            receivers.Sum(actor => view.DamageScore(actor, amount * view.Server.TrapCountOf(actor)));
        internal override EffectTag Tag => EffectTag.Damage;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry).Where(a => server.TrapCountOf(a) > 0);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) => Affected(server, entry).Contains(actor) ? amount * server.TrapCountOf(actor) : 0;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.ApplyDamage(entry, actor, amount * server.TrapCountOf(actor));
        }
    }
}
