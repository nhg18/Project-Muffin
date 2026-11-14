namespace Chapchu.Game.Bots
{
    /// <summary>봇 난이도 (18-bot.md 7-7). 방 속성에는 숫자로 간다 — 순서를 바꾸지 않는다.</summary>
    public enum BotLevel
    {
        Easy,   // 쉬움
        Normal, // 보통 — 기본
        Hard    // 어려움
    }

    /// <summary>
    /// 난이도가 바꾸는 값 (18-bot.md 7-7). 난이도는 「얼마나 잘 보고 얼마나 실수하는가」 만 다르다 — 규칙 · 카드 · 보는 정보는 같다.
    /// </summary>
    internal readonly struct BotLevelSpec
    {
        /// <summary>수마다 점수에 섞는 무작위 (± 점).</summary>
        public readonly double Noise;
        /// <summary>가장 좋은 수 대신 손해가 아닌 아무 수나 고를 확률.</summary>
        public readonly double MistakeRate;
        /// <summary>찹츄 상태인 상대를 그 턴에 알아챌 확률.</summary>
        public readonly double NoticeRate;
        /// <summary>찹츄가 가까운 상대(7 ~ 11장) · 내 찹츄 준비(8 ~ 9장)를 얼마나 내다보는가 (0 ~ 1).</summary>
        public readonly double Foresight;
        /// <summary>쓰러뜨릴 수 있는 상대를 먼저 노린다. 못 쓰러뜨릴 때는 약한 상대부터 (LeaderFocus 가 0 일 때).</summary>
        public readonly bool SeesKill;
        /// <summary>1등 견제 — 상대 HP 1 마다 더하는 점수. 0 보다 크면 약한 상대 대신 HP 가 가장 높은 상대부터 때린다.</summary>
        public readonly double LeaderFocus;

        private BotLevelSpec(double noise, double mistakeRate, double noticeRate, double foresight, bool seesKill, double leaderFocus)
        {
            Noise = noise;
            MistakeRate = mistakeRate;
            NoticeRate = noticeRate;
            Foresight = foresight;
            SeesKill = seesKill;
            LeaderFocus = leaderFocus;
        }

        // 18-bot.md 7-7 표
        public static BotLevelSpec Of(BotLevel level)
        {
            switch (level)
            {
                case BotLevel.Easy: return new BotLevelSpec(30, 0.25, 0.5, 0, false, 0);
                case BotLevel.Hard: return new BotLevelSpec(4, 0, 1, 1, true, 0.3);
                default:            return new BotLevelSpec(15, 0.1, 0.85, 0.5, true, 0);
            }
        }
    }

    /// <summary>봇의 성향 (18-bot.md 7-8) — 판마다 무작위로 하나. 좋아하는 일의 점수가 조금 높다.</summary>
    internal enum BotStyle
    {
        Fighter,   // 싸움꾼 — 피해
        Collector, // 수집가 — 내 손패 · 찹츄
        Disruptor  // 훼방꾼 — 상대 손패
    }
}
