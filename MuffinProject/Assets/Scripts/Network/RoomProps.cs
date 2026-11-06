namespace Chapchu.Network
{
    /// <summary>
    /// Room CustomProperties 키 (09-network.md 6절). 모두가 봐도 되는 작은 값의 "지금 값" 스냅샷만 둔다.
    /// </summary>
    public static class RoomProps
    {
        public const string TurnActor = "turnActor";
        public const string TurnDeadline = "turnDeadline"; // 턴 마감 서버 시각(PhotonNetwork.Time, 초)
        public const string DeckCount = "deckCount";
        public const string DiscardCount = "discardCount"; // 버림 더미 장수 (05-deck.md 7절)
        public const string Bots = "bots";                 // 봇 번호 배열 (int[], 18-bot.md 9절). 봇의 공개 값은 BotRoster.PropKey
        public const string Solo = "solo";                 // 「혼자 하기」 방 (bool) — 다시 열어도 랜덤 매칭에서 뺀다 (18-bot.md 10절)
    }
}
