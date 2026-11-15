using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람마다 체력을 amount 만큼 올린다 (Heal — A08 · A13 · A19 · T02). 최대 100, 사망 대기 · 사망은 받지 않는다 (06 6절). 회복 → 피해 전환(C09)은 체인 표시대로.</summary>
    [Serializable]
    public sealed class HealEffect : CardEffect
    {
        public EffectSubject subject;
        public int amount;

        // 18-bot 7-1 — 내 회복은 HP 가 낮을수록 값지다. 상대를 회복시키면 감점 (106 은 그래서 나에게 쓴다)
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self ? view.HealScore(view.Bot, amount) : receivers.Sum(actor => view.HealScore(actor, amount));
        internal override EffectTag Tag => EffectTag.Heal;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.ApplyHeal(entry, actor, amount);
        }
    }
}
