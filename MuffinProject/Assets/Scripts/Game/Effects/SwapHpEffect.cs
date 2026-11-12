using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 쓴 사람과 대상의 HP 를 서로 바꾼다 (SwapHp — 111 운명 뒤집기). HP 설정이라 피해 · 회복이 아니다 — 감소 · 반사 · 함정이 걸리지 않는다 (11-card-list 1-2).
    /// 봇은 내 HP 를 올리는 카드로 본다 — 대상 HP 가 내 HP 보다 높을 때만 쓴다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class SwapHpEffect : CardEffect
    {
        internal override Bots.BotRole BotRole => Bots.BotRole.Heal;

        internal override bool BotWants(GameServer server, int bot, int[] targets) => targets.Length == 1 && server.PlayerHp(targets[0]) > server.PlayerHp(bot);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int target in server.ReceiversOf(EffectSubject.Target, entry).Where(actor => actor != entry.User).Take(1))
                server.SwapHp(entry.User, target);
        }
    }
}
