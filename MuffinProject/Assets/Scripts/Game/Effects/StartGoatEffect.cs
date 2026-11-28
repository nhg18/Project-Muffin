using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// GOAT 시작 (1067 GOAT, 11-card-list 1067). 쓴 사람의 HP 가 hp 가 되고, 쓴 사람의 실제 턴이 rounds 번 시작될 때까지 최대 HP 가 hp · 남에게 주는 피해가 damageTimes 배.
    /// 끝나도 HP 는 그대로 — 100 을 넘는 HP 는 깎이기만 한다. 최종 사망하거나 나가면 끝. 2배(2005)는 걸리지 않는다 (피해 · 회복 · 장수가 아니다).
    /// </summary>
    [Serializable]
    public sealed class StartGoatEffect : CardEffect
    {
        public int rounds;
        public int hp;
        public int damageTimes;

        // 18-bot.md 7-3 「GOAT 시작」 (프로토타입 v3)
        private const double BotGoatScore = 55;
        private const double BotMissingHpWeight = 0.25; // (100 − 내 HP) × 이 값을 더한다

        // 이미 GOAT 면 다시 쓰지 않는다
        internal override bool BotWants(GameServer server, int bot, int[] receivers) => !server.IsGoat(bot);

        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            BotGoatScore + Math.Max(0, GameServer.MaxHp - view.MyHp) * BotMissingHpWeight;

        internal override void Apply(GameServer server, ChainEntry entry) => server.StartGoat(entry.User, rounds, hp, damageTimes);
    }
}
