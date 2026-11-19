namespace Chapchu.Core
{
    /// <summary>
    /// 씬 이름 상수. 씬 전환은 이 형식(경로 없는 이름) 하나만 쓴다.
    ///   로컬 전환:     SceneManager.LoadScene(ScenePaths.Lobby)
    ///   방 전원 전환:  NetworkManager.Instance.LoadSceneForAll(ScenePaths.Game) — 방장만
    /// PUN 의 씬 동기화(AutomaticallySyncScene)는 쓰지 않는다 — 방 속성 「curScn」 을 누가 썼는지 보지 않아
    /// 아무 클라나 방 전원의 씬을 바꿀 수 있다 (09-network.md 13절). 방 씬은 각자 로비가 연다.
    /// </summary>
    public static class ScenePaths
    {
        public const string Title = "TitleScene";
        public const string Lobby = "LobbyScene";
        public const string Room = "RoomScene";
        public const string Game = "GameScene";
        public const string Result = "ResultScene";
        public const string DebugLobby = "DebugLobbyScene";
    }

    /// <summary>
    /// 씬 사이에서 넘겨야 하는 전환 정보. 씬을 로드하면 씬 안의 오브젝트는 전부 사라지므로
    /// 정적 필드에 둔다.
    /// </summary>
    public static class SceneFlow
    {
        /// <summary>
        /// 방(Room)에서 나갔을 때 돌아갈 씬. 로비(Lobby / DebugLobby)가 열릴 때(Start) 설정한다.
        /// 기본값은 정식 흐름인 Lobby. (08-room.md 6절: Room ──(나가기)──→ Lobby)
        /// </summary>
        public static string ReturnSceneAfterRoom = ScenePaths.Lobby;

        /// <summary>
        /// 방장이 게임을 시작하면 방 전원이 넘어갈 씬. 로비(Lobby / DebugLobby)가 열릴 때(Start) 설정한다.
        /// 기본값은 인게임.
        /// </summary>
        public static string GameSceneAfterRoom = ScenePaths.Game;

        /// <summary>
        /// 로비가 10초 시간 초과로 포기한 방 요청이 있다 (14-lobby-ui 6절). 늦은 입장 응답이 로비가 사라진 뒤
        /// 방 씬에서 오면, 방 화면이 이 값을 보고 바로 방을 나간다.
        /// 포기한 요청이 끝났을 때만 끈다 — 늦은 입장을 나갔을 때 · 늦은 실패 응답이 왔을 때 · 서버가 새 요청을 받을 수 있는 상태에서
        /// 새 방 요청을 보낼 때. 옛 요청이 처리 중일 때 누른 새 요청은 보내지지 않으므로 끄지 않는다.
        /// </summary>
        public static bool IsRoomRequestAbandoned;
    }
}
