using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 쓴 사람과 대상의 HP 를 서로 바꾼다 (SwapHp — 111 운명 뒤집기). HP 설정이라 피해 · 회복이 아니다 — 감소 · 반사 · 함정이 걸리지 않는다 (11-card-list 1-2).
    /// 봇은 대상 HP 가 내 HP 보다 높을 때만 쓴다 — 차이가 클수록 점수가 높다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class SwapHpEffect : CardEffect
    {
        internal override bool BotWants(GameServer server, int bot, int[] receivers) => receivers.Length == 1 && server.PlayerHp(receivers[0]) > server.PlayerHp(bot);

        // 내가 얻는 HP + 상대가 잃는 HP 의 절반
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            receivers.Length == 1 ? (view.Server.PlayerHp(receivers[0]) - view.MyHp) * 1.5 : 0;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int target in server.ReceiversOf(EffectSubject.Target, entry).Where(actor => actor != entry.User).Take(1))
                server.SwapHp(entry.User, target);
        }
    }
}
