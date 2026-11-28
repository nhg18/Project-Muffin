using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 받는 사람마다 체력을 amount 만큼 올린다 (Heal — A08 · A13 · A19 · T02). full 이면 amount 대신 그 사람의 최대 HP 까지 채운다 (3012 편안).
    /// 최대 HP 는 사람마다 다르다 — 평소 100, GOAT 동안 GOAT 의 최대 HP (06 2절). 사망 대기 · 사망은 받지 않는다 (06 6절). 회복 → 피해 전환(C09)은 체인 표시대로.
    /// </summary>
    [Serializable]
    public sealed class HealEffect : CardEffect
    {
        public EffectSubject subject;
        public int amount;
        public bool full; // 최대 HP 까지 — 그때는 amount 를 쓰지 않는다

        /// <summary>최대 HP 에서 missingHp 만큼 모자란 사람이 받을 회복량 — 이펙트 시험대처럼 규칙 밖에서 양을 보여 줄 때.</summary>
        public int AmountWhenMissing(int missingHp) => full ? missingHp : amount;

        // 18-bot 7-1 — 내 회복은 HP 가 낮을수록 값지다. 상대를 회복시키면 감점 (1006 은 그래서 나에게 쓴다). 실제 회복량은 HealScore 가 그 사람의 최대 HP 로 자른다
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self ? view.HealScore(view.Bot, BotAmount) : receivers.Sum(actor => view.HealScore(actor, BotAmount));
        internal override EffectTag Tag => EffectTag.Heal;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        private int BotAmount => full ? int.MaxValue : amount;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.ApplyHeal(entry, actor, full ? server.MissingHp(actor) : amount);
        }
    }
}
