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

        // 대상에게 주는 피해만 공격이다 — 나에게 주는 피해(A12)는 아니다
        internal override Bots.BotRole BotRole => subject == EffectSubject.Target ? Bots.BotRole.Attack : Bots.BotRole.None;
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
