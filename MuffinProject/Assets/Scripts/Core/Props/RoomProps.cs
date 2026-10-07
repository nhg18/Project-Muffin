namespace Chapchu.Core
{

    /// <summary>
    /// Room CustomProperties 키 (09-network.md 6절).
    /// </summary>
    public static class RoomProps
    {
        public const string TurnActor = "turnActor";
        public const string DeckCount = "deckCount";
        public const string DiscardCount = "discardCount"; // 버림 더미 장수 — 전체 공개 (05-deck.md 7절)
        public const string TurnDeadline = "turnDeadline"; // 턴 마감 서버 시각(PhotonNetwork.Time, 초). UI 는 남은 시간을 로컬에서 계산한다
    }
}
