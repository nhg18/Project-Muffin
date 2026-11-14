using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람마다 체력을 amount 만큼 줄인다 (11-card-list.md 5절 Damage · 06-health.md). 감소 · 전환 · 사망 방지는 체인 표시대로.</summary>
    [Serializable]
    public sealed class DamageEffect : CardEffect
    {
        public EffectSubject subject;
        public int amount;

        // 18-bot 7-1 — 받는 사람마다 피해 점수 (쓰러뜨리면 더). 나에게 주는 피해(A12 · 전원 피해)는 감점
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self ? view.SelfDamageScore(amount) : receivers.Sum(actor => view.DamageScore(actor, amount));
        internal override EffectTag Tag => EffectTag.Damage;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) => Affected(server, entry).Contains(actor) ? amount : 0;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.ApplyDamage(entry, actor, amount);
        }
    }
}
