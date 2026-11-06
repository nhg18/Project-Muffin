namespace Chapchu.Game
{
    /// <summary>
    /// 요청 거절 코드 (09-network.md 4.3). 방장이 요청자에게 이 숫자만 보내고, 화면 문구는 UI 가 정한다.
    /// 네트워크로 오가는 값이라 번호는 뒤에만 추가하고 바꾸거나 재사용하지 않는다.
    /// </summary>
    public static class RejectCode
    {
        public const int NotYourTurn = 1;   // 내 턴이 아니다
        public const int NotInHand = 2;     // 손패에 없는 카드
        public const int NoCardToDraw = 3;  // (쓰지 않음 — 빈 덱 드로우는 피해, 05 5절. 번호 재사용 금지)
        public const int NotActionCard = 4; // 행동 카드가 아니다
        public const int InvalidTarget = 5; // 대상이 카드 대상 타입에 맞지 않는다
        public const int NotReactionTime = 6; // 반응 시간이 아니다 — 체인 없음 · 연출 중 · 마감 지남 (04 8절)
        public const int MainActionUsed = 7;  // 이번 턴의 메인 행동을 이미 했다 (03 3절)
        public const int NotCounterCard = 8;  // 카운터 카드가 아니다
        public const int CannotReact = 9;     // 체인 맨 위 카드에 반응할 수 없는 카운터 (04 7절)
        public const int NotAlive = 10;       // 사망했거나 나간 사람의 요청 (06 9절)
        public const int NotTenCards = 11;    // 찹츄 선언 — 손패가 정확히 10장이 아니다 (07 4.1)
        public const int AlreadyChapchu = 12; // 찹츄 선언 — 이미 찹츄 상태
        public const int GameOver = 13;       // 게임이 끝났다
    }
}
