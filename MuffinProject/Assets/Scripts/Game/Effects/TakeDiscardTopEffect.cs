using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 처리되는 시점의 버림 더미 맨 위 1장을 쓴 사람 손패로 (TakeDiscardTop — 109 아 맞다!). 고를 수 없다. 이 카드 자신은 가져올 수 없다.
    /// 더미가 비어 있으면 불발 (11-card-list 1-2). 봇은 맨 위 카드가 지금 쓸 만할수록 가져온다 — 맨 위는 공개 정보다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class TakeDiscardTopEffect : CardEffect
    {
        internal override bool BotWants(GameServer server, int bot, int[] receivers) => server.DiscardTop != null;

        // 가져온 카드는 다음 턴에나 쓴다 — 지금 쓸 때 점수의 절반. 맨 위가 같은 카드면 가져올 까닭이 없다
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            Card top = view.Server.DiscardTop;
            if (top == null || top.Effects.Any(effect => effect is TakeDiscardTopEffect))
                return 0;

            return Math.Min(60, Math.Max(0, view.WorthOf(top)) * 0.5);
        }

        internal override int BotHandGain(Bots.BotView view, int[] receivers) => view.Server.DiscardTop != null ? 1 : 0;

        internal override void Apply(GameServer server, ChainEntry entry) => server.TakeDiscardTop(entry.User, entry);
    }
}
