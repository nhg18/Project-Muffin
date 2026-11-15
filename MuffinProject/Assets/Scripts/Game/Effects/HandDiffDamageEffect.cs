using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>(대상 손패 − 쓴 사람 손패) × amount 피해, 차이가 음수면 1 × amount (HandDiffDamage — A26). 차이 0 이면 사건 없음.</summary>
    [Serializable]
    public sealed class HandDiffDamageEffect : CardEffect
    {
        public int amount;

        // 낸 뒤의 내 손패 장수와 견준다
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            receivers.Sum(actor =>
            {
                int diff = view.Server.HandCountOf(actor) - (view.MyHand - 1);
                return view.DamageScore(actor, (diff < 0 ? 1 : diff) * amount);
            });
        internal override EffectTag Tag => EffectTag.Damage;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry).Where(a => DamageTo(server, entry, a) > 0);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) => server.ReceiversOf(EffectSubject.Target, entry).Contains(actor) ? DamageTo(server, entry, actor) : 0;

        private int DamageTo(GameServer server, ChainEntry entry, int actor)
        {
            int diff = server.HandCountOf(actor) - server.HandCountOf(entry.User);
            return (diff < 0 ? 1 : diff) * amount;
        }

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.ApplyDamage(entry, actor, DamageTo(server, entry, actor));
        }
    }
}
