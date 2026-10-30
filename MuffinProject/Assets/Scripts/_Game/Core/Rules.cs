namespace Chapchu.Game.Core
{
    /// <summary>
    /// 게임 규칙 수치. 값의 원본은 docs/systems 의 확정 값이다. 바꾸려면 문서를 먼저 고친다.
    /// </summary>
    public static class Rules
    {
        /// <summary>초기 · 최대 HP (06-health.md).</summary>
        public const int MaxHp = 100;

        /// <summary>게임 시작 때 나눠 주는 장수 (05-deck.md 2절).</summary>
        public const int StartingHandSize = 5;

        /// <summary>플레이어당 함정 슬롯 수 (02-player.md).</summary>
        public const int TrapSlotCount = 3;

        /// <summary>턴 제한 시간, 초 (03-turn.md 2절).</summary>
        public const double TurnSeconds = 20;

        /// <summary>카드가 체인에 올라갈 때마다 기다리는 반응 시간, 초 (04-card.md 8절).</summary>
        public const double ReactionSeconds = 5;
    }
}
