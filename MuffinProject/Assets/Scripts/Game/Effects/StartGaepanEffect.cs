using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 개판 오분전 시작 (121, 11-card-list 121). 이 턴이 끝난 뒤부터 턴을 세어 생존자 수만큼이면 한 바퀴, 5바퀴 뒤 반반 기준 꼴찌가 HP 0.
    /// 개판 중에 다시 쓰면 처음부터. 건 사람이 죽거나 나가도 이어진다 (사용자 2026-11-13).
    /// </summary>
    [Serializable]
    public sealed class StartGaepanEffect : CardEffect
    {
        internal override Bots.BotRole BotRole => Bots.BotRole.Board;

        // 18-bot 7-3 ㅂ (사용자 2026-11-13 · 프로토타입 v2): 개판이 없고, 내 HP 가 상대 최저보다 20 이상 · 내 손패가 상대 최소보다 2장 이상 많을 때만
        internal override bool BotWants(GameServer server, int bot, int[] targets)
        {
            if (server.IsGaepanActive)
                return false;
            int[] others = server.ActorsInGame.Where(actor => actor != bot && server.StateOf(actor).Life == LifeState.Alive).ToArray();
            if (others.Length == 0)
                return false;
            return server.PlayerHp(bot) - others.Min(server.PlayerHp) >= 20
                && server.HandCountOf(bot) - others.Min(server.HandCountOf) >= 2;
        }

        internal override void Apply(GameServer server, ChainEntry entry) => server.StartGaepan(entry.User);
    }
}
