using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람의 다음 턴 스킵 — 겹쳐도 1회 (SkipTurn — A01 · T05, 03-turn.md 7절).</summary>
    [Serializable]
    public sealed class SkipTurnEffect : CardEffect
    {
        // 18-bot.md 7-3 「턴 건너뛰기」
        private const double BotBaseScore = 10;
        private const double BotChapchuScore = 120;   // 대상이 찹츄 상태
        private const double BotNearChapchuWeight = 0.5; // 아니면 「찹츄 가까움」 × 이 값
        private const double BotTwoLeftBonus = 20;    // 둘만 남았다

        public EffectSubject subject;

        internal override EffectTag Tag => EffectTag.SkipTurn;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        // 18-bot 7-3 — 찹츄 상대의 턴을 건너뛰면 그 턴에는 찹츄 판정이 없다 (03-turn 7절). 둘만 남았으면 내 턴이 바로 다시 온다. 이미 걸린 사람에게는 겹치지 않는다
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            if (subject == EffectSubject.Self)
                return double.NaN;

            double score = 0;
            foreach (int actor in receivers)
            {
                if (actor == view.Bot || view.Server.WillSkipTurn(actor))
                    continue;

                score += BotBaseScore + (view.SeesChapchu(actor) ? BotChapchuScore : view.NearChapchu(view.Server.HandCountOf(actor)) * BotNearChapchuWeight);
                if (view.Opponents.Length == 1)
                    score += BotTwoLeftBonus;
            }

            return score;
        }

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.MarkSkipTurn(actor);
        }
    }
}
