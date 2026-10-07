namespace Chapchu.Core
{
    /// <summary>
    /// 요청 거절 코드 (09-network.md 4.3). 방장이 요청자에게 이 숫자만 보내고, 화면 문구는 UI(RejectText)가 정한다.
    /// 네트워크로 오가는 값이라 번호는 뒤에만 추가하고 바꾸거나 재사용하지 않는다.
    /// </summary>
    public static class RejectCode
    {
        public const int NotYourTurn = 1;   // 내 턴이 아니다
        public const int NotInHand = 2;     // 손패에 없는 카드
        public const int NoCardToDraw = 3;  // 덱 · 버림 더미가 모두 비었다 (미정 — 임시 거절)
        public const int NotActionCard = 4; // 행동 카드가 아니다
        public const int InvalidTarget = 5; // 대상이 카드 대상 타입에 맞지 않는다
    }
}
