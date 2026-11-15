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
    /// 난이도가 바꾸는 값 (18-bot.md 7-7). 난이도는 「얼마나 잘 보고 얼마나 실수하는가」 와 「얼마나 뜸을 들이는가」 만 다르다 — 규칙 · 카드 · 보는 정보는 같다.
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
        /// <summary>쓰러뜨릴 수 있는 상대를 먼저 노린다.</summary>
        public readonly bool SeesKill;
        /// <summary>1등 견제 — 상대 HP 1 마다 더하는 점수. 클수록 HP 가 높은 상대부터 때린다. 0 이면 누구부터를 가리지 않는다.</summary>
        public readonly double LeaderFocus;

        /// <summary>내 턴 생각 시간의 배수 (18-bot.md 4절) — 1 보다 크면 느리다.</summary>
        public readonly double Pace;
        /// <summary>내 턴에 한 번 더 뜸을 들일 확률과 그 길이 (초).</summary>
        public readonly double HesitateRate, HesitateMin, HesitateMax;
        /// <summary>반응 창(카운터 · 함정 · 사망 대기 · 고르기)이 열린 뒤 반응하기까지 (초).</summary>
        public readonly double ReactMin, ReactMax;

        private BotLevelSpec(double noise, double mistakeRate, double noticeRate, double foresight, bool seesKill, double leaderFocus,
            double pace, double hesitateRate, double hesitateMin, double hesitateMax, double reactMin, double reactMax)
        {
            Noise = noise;
            MistakeRate = mistakeRate;
            NoticeRate = noticeRate;
            Foresight = foresight;
            SeesKill = seesKill;
            LeaderFocus = leaderFocus;
            Pace = pace;
            HesitateRate = hesitateRate;
            HesitateMin = hesitateMin;
            HesitateMax = hesitateMax;
            ReactMin = reactMin;
            ReactMax = reactMax;
        }

        // 18-bot.md 7-7 표
        public static BotLevelSpec Of(BotLevel level)
        {
            switch (level)
            {
                case BotLevel.Easy:
                    return new BotLevelSpec(noise: 30, mistakeRate: 0.25, noticeRate: 0.5, foresight: 0, seesKill: false, leaderFocus: 0,
                        pace: 1.3, hesitateRate: 0.35, hesitateMin: 1.5, hesitateMax: 3, reactMin: 1.5, reactMax: 3.5);
                case BotLevel.Hard:
                    return new BotLevelSpec(noise: 4, mistakeRate: 0, noticeRate: 1, foresight: 1, seesKill: true, leaderFocus: 0.3,
                        pace: 0.85, hesitateRate: 0.1, hesitateMin: 0.5, hesitateMax: 1.5, reactMin: 0.8, reactMax: 2.5);
                default:
                    return new BotLevelSpec(noise: 15, mistakeRate: 0.1, noticeRate: 0.85, foresight: 0.5, seesKill: true, leaderFocus: 0.1,
                        pace: 1, hesitateRate: 0.2, hesitateMin: 1, hesitateMax: 2.5, reactMin: 1, reactMax: 3);
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
