namespace Chapchu.Network
{
    /// <summary>
    /// Room CustomProperties 키 (09-network.md 6절). 모두가 봐도 되는 작은 값의 "지금 값" 스냅샷만 둔다.
    /// </summary>
    public static class RoomProps
    {
        public const string TurnActor = "turnActor";
        public const string TurnOrder = "turnOrder";       // 턴 순서 — 방장이 섞은 actorNumber 배열 (int[], 03-turn.md 6절 #2). 게임 시작 · 대소동 새 판에 쓴다
        public const string TurnDeadline = "turnDeadline"; // 턴 마감 서버 시각(PhotonNetwork.Time, 초)
        public const string DeckCount = "deckCount";
        public const string DiscardCount = "discardCount"; // 버림 더미 장수 (05-deck.md 7절)
        public const string DiscardTop = "discardTop";     // 버림 더미 맨 위 카드 종류 ID (int). 더미가 비면 키를 지운다 (09-network.md 6절)
        public const string EventSeq = "eventSeq";         // 이 스냅샷이 반영한 마지막 처리 번호 (int, 09-network.md 5절)
        public const string Bots = "bots";                 // 봇 번호 배열 (int[], 18-bot.md 9절). 봇의 공개 값은 BotRoster.PropKey
        public const string BotLevels = "botLv";           // 봇 난이도 (int[3] — [봇 번호 − 1] 자리에 BotLevel, 18-bot.md 9절). 키가 없으면 전부 보통
        public const string TurnDirection = "turnDir"; // 턴 진행 방향 — true 면 역방향 (bool, 03-turn.md 6절 #4). 정방향이면 키를 지운다
        public const string Gaepan = "gaepan";            // 개판 오분전 [건 사람, 남은 바퀴] (int[], 1021). 개판이 없으면 키를 지운다
        public const string Fallout = "fallout";           // 피폭 [n, k] (int[], 1012 대소동 — n 번째 대소동 · 새로 시작한 뒤 피폭이 들어간 턴 수). 피폭이 없으면 키를 지운다
        public const string WaitCasters = "wait";          // 「기다려」 를 건 사람들 (int[], 1023 기다려! — 07 4.3). 아무도 없으면 키를 지운다
        public const string Solo = "solo";                 // 「혼자 하기」 방 (bool) — 다시 열어도 랜덤 매칭에서 뺀다 (18-bot.md 10절)
        public const string Match = "match";               // 랜덤 매칭이 만든 방 (bool) — 랜덤 매칭은 이 방에만 들어간다. 로비 목록에도 실어 거른다 (08-room.md 4절)
        public const string MatchStartAt = "matchAt";      // 매치 방이 시작하는 서버 시각 (double) — 지나면 방장이 빈자리를 봇으로 채우고 시작 (08-room.md 4절)

        // 결과 화면 (07-win-condition.md 11-1) — 방장이 게임 종료 때 새로 쓴다
        public const string ResultDeadline = "resDeadline"; // 이 서버 시각이 지나면 각자 방을 나가 로비로 (double)
        public const string ResultReady = "resReady";       // 「한 판 더」 를 누른 사람 · 준비한 봇 (int[]). 아무도 없으면 키를 지운다
        public const string ResultStartAt = "resStartAt";   // 전원 준비 — 이 서버 시각에 다음 판 시작 (double). 카운트다운이 없으면 키를 지운다
    }
}
