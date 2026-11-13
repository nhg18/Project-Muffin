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
        internal override Bots.BotRole BotRole => Bots.BotRole.Board;

        internal override bool BotWants(GameServer server, int bot, int[] targets)
        {
            int[] others = server.ActorsInGame.Where(a => a != bot).ToArray();
            int myHp = server.StateOf(bot).Hp;
            return others.Any(a => server.StateOf(a).IsChapchu) || others.All(a => server.StateOf(a).Hp >= myHp);
        }

        internal override void Apply(GameServer server, ChainEntry entry) => server.RequestRestart();
    }
}
