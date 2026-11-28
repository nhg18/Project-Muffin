using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 영역 시작 (1008 영역전개, 11-card-list 1008). 쓴 사람의 실제 턴이 rounds 번 시작될 때마다, 나를 뺀 생존자 모두에게서 무작위 take 장을 빼앗고 damage 피해를 준다.
    /// 영역은 판에 하나 — 다른 사람이 쓰면 덮어쓴다. 주인이 최종 사망하거나 나가면 끝. 2배(2005)는 빼앗는 장수 · 피해만, 바퀴 수는 그대로.
    /// </summary>
    [Serializable]
    public sealed class StartDomainEffect : CardEffect
    {
        public int rounds;
        public int take;
        public int damage;

        // 18-bot.md 7-3 「영역 시작」 (프로토타입 v3)
        private const double BotDomainScore = 55;

        // 내 영역이 이미 있으면 다시 쓰지 않는다
        internal override bool BotWants(GameServer server, int bot, int[] receivers) => server.DomainOwner != bot;

        internal override double BotScore(Bots.BotView view, int[] receivers) => view.Opponents.Length > 0 ? BotDomainScore : 0;

        internal override void Apply(GameServer server, ChainEntry entry) =>
            server.StartDomain(entry.User, rounds, server.Scaled(entry, take), server.Scaled(entry, damage));
    }
}
