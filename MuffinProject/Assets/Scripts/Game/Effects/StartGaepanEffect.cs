using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 개판 오분전 시작 (1021, 11-card-list 1021). 이 턴이 끝난 뒤부터 턴을 세어 생존자 수만큼이면 한 바퀴, 5바퀴 뒤 반반 기준 꼴찌가 HP 0.
    /// 개판 중에 다시 쓰면 처음부터. 건 사람이 죽거나 나가도 이어진다 (사용자 2026-11-13).
    /// </summary>
    [Serializable]
    public sealed class StartGaepanEffect : CardEffect
    {
        internal override bool BoardWide => true;

        // 18-bot.md 7-3 「개판 시작」
        private const int BotMinHpLead = 20;   // 내 HP − 상대 최저 HP 가 이 이상
        private const int BotMinHandLead = 2;  // 내 손패 − 상대 최소 손패가 이 이상
        private const double BotGaepanScore = 45;

        // 18-bot 7-3 ㅂ (사용자 2026-11-13 · 프로토타입 v2): 개판이 없고, 내 HP 가 상대 최저보다 20 이상 · 내 손패가 상대 최소보다 2장 이상 많을 때만
        internal override bool BotWants(GameServer server, int bot, int[] receivers)
        {
            if (server.IsGaepanActive)
                return false;
            int[] others = server.ActorsInGame.Where(actor => actor != bot && server.StateOf(actor).Life == LifeState.Alive).ToArray();
            if (others.Length == 0)
                return false;
            return server.PlayerHp(bot) - others.Min(server.PlayerHp) >= BotMinHpLead
                && server.HandCountOf(bot) - others.Min(server.HandCountOf) >= BotMinHandLead;
        }

        // 조건이 맞으면 5바퀴 뒤 꼴찌 한 명이 빠진다 — 쓰러뜨림에 조금 못 미치는 값
        internal override double BotScore(Bots.BotView view, int[] receivers) => BotGaepanScore;

        internal override void Apply(GameServer server, ChainEntry entry) => server.StartGaepan(entry.User);
    }
}
