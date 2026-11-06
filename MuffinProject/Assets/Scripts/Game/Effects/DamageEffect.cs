using System;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람마다 체력을 amount 만큼 줄인다 (11-card-list.md 5절 Damage · 06-health.md).</summary>
    [Serializable]
    public sealed class DamageEffect : CardEffect
    {
        public EffectSubject subject;
        public int amount;

        // 대상에게 주는 피해만 공격이다 — 나에게 주는 피해(A12)는 아니다
        internal override Bots.BotRole BotRole => subject == EffectSubject.Target ? Bots.BotRole.Attack : Bots.BotRole.None;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.DealDamage(actor, amount, entry.User);
        }
    }
}
