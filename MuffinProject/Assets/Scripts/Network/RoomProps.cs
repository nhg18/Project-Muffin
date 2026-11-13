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
        public const string DiscardTop = "discardTop";     // 버림 더미 맨 위 카드 종류 ID (int). 더미가 비면 키를 지운다 (09-network.md 6절)
        public const string EventSeq = "eventSeq";         // 이 스냅샷이 반영한 마지막 처리 번호 (int, 09-network.md 5절)
        public const string Bots = "bots";                 // 봇 번호 배열 (int[], 18-bot.md 9절). 봇의 공개 값은 BotRoster.PropKey
        public const string TurnDirection = "turnDir"; // 턴 진행 방향 — true 면 역방향 (bool, 03-turn.md 6절 #4). 정방향이면 키를 지운다
        public const string Fallout = "fallout";           // 피폭 [n, k] (int[], 112 대소동 — n 번째 대소동 · 새로 시작한 뒤 피폭이 들어간 턴 수). 피폭이 없으면 키를 지운다
        public const string WaitCasters = "wait";          // 「기다려」 를 건 사람들 (int[], 123 기다려! — 07 4.3). 아무도 없으면 키를 지운다
        public const string Solo = "solo";                 // 「혼자 하기」 방 (bool) — 다시 열어도 랜덤 매칭에서 뺀다 (18-bot.md 10절)
    }
}
