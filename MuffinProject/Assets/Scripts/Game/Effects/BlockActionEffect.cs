using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 받는 사람의 다음 실제 턴에 행동 카드 금지 — 겹쳐도 1회 (BlockAction — 107 짖기, 11-card-list 107 · 03-turn.md 7절).
    /// 스킵된 턴은 세지 않는다. 뽑기 · 함정 설치 · 카운터는 그대로. 봇은 내 HP 가 낮을 때 몸을 지키는 데 쓴다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class BlockActionEffect : CardEffect
    {
        public EffectSubject subject;

        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        // 그 상대가 다음 턴에 나를 때리거나 내 손패를 건드리지 못한다 — 내 HP 가 낮을수록 값지다
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self ? double.NaN : receivers.Count(actor => actor != view.Bot) * (8 + (view.MyHp <= Bots.BotView.GuardHp ? 12 : 0));

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.MarkActionBlock(actor);
        }
    }
}
