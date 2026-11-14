using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 게임을 새로 시작한다 — 이 턴이 끝나면 지금 방에 있는 사람으로 처음처럼, 그 뒤로 턴이 끝날 때마다 모두 피폭 k^n (Restart — 112 대소동, 11-card-list 112).
    /// 봇은 찹츄 상대가 있거나 내 HP 가 꼴찌일 때만 쓴다 (18-bot.md 7-3 ㅂ).
    /// </summary>
    [Serializable]
    public sealed class RestartEffect : CardEffect
    {
        internal override bool BotWants(GameServer server, int bot, int[] receivers)
        {
            int[] others = server.ActorsInGame.Where(a => a != bot).ToArray();
            int myHp = server.StateOf(bot).Hp;
            return others.Any(a => server.StateOf(a).IsChapchu) || others.All(a => server.StateOf(a).Hp >= myHp);
        }

        // 찹츄 상대가 있으면 판을 엎을 값이 크다. 아니면 내가 뒤진 HP 만큼 — 모두 같은 HP 면 0 이라 쓰지 않는다
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            if (view.SeesAnyChapchu)
                return 250;

            int[] opponents = view.Opponents;
            return opponents.Length == 0 ? 0 : Math.Max(0, opponents.Average(actor => (double)view.Server.PlayerHp(actor)) - view.MyHp);
        }

        internal override void Apply(GameServer server, ChainEntry entry) => server.RequestRestart();
    }
}
