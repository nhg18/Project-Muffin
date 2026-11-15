using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 쓴 사람의 다음 실제 턴이 시작되기 전까지 아무도 찹츄로 이기지 않는다 (StartWait — 123 기다려!, 11-card-list 123 · 07 4.3).
    /// 처치 승리는 막지 않는다. 쓴 사람이 최종 사망하거나 나가면 즉시 끝. 봇은 찹츄 상대를 깰 수 없을 때 시간을 버는 데 쓴다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class StartWaitEffect : CardEffect
    {
        // 찹츄 상대가 있으면 내 다음 턴까지 승리를 미룬다 (깨는 것보다는 낮다). 없으면 찹츄가 가까운 상대가 있을 때만 조금
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            if (view.SeesAnyChapchu)
                return 110;

            int[] opponents = view.Opponents;
            return opponents.Length == 0 ? 0 : opponents.Max(actor => view.NearChapchu(view.Server.HandCountOf(actor))) * 0.3;
        }

        internal override void Apply(GameServer server, ChainEntry entry) => server.StartWait(entry.User);
    }
}
