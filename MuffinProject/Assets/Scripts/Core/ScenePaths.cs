namespace Chapchu.Core
{
    /// <summary>
    /// 씬 이름 상수. 씬 전환은 이 형식(경로 없는 이름) 하나만 쓴다.
    ///   로컬 전환:     SceneManager.LoadScene(ScenePaths.Lobby)
    ///   방 전원 전환:  PhotonNetwork.LoadLevel(ScenePaths.Game)
    /// PUN 의 씬 동기화(AutomaticallySyncScene)는 마스터가 올린 룸 프로퍼티 값을
    /// 클라이언트의 활성 씬 "이름" 과 비교한다. 경로 형태("Scenes/GameScene")를 넘기면
    /// 이름이 영원히 일치하지 않아, 비마스터 클라이언트가 룸 프로퍼티가 바뀔 때마다
    /// 씬을 다시 로드한다. (PhotonNetworkPart.cs LoadLevelIfSynced 참고)
    /// </summary>
    public static class ScenePaths
    {
        public const string Title = "TitleScene";
        public const string Lobby = "LobbyScene";
        public const string Room = "RoomScene";
        public const string Game = "GameScene";
        public const string DebugLobby = "DebugLobbyScene";
        public const string TmpGame = "TmpGameScene";
    }

    /// <summary>
    /// 씬 사이에서 넘겨야 하는 전환 정보. 씬을 로드하면 씬 안의 오브젝트는 전부 사라지므로
    /// 정적 필드에 둔다.
    /// </summary>
    public static class SceneFlow
    {
        /// <summary>
        /// 방(Room)에서 나갔을 때 돌아갈 씬. 방에 들어가기 직전에 진입 쪽(Lobby / DebugLobby)이 설정한다.
        /// 기본값은 정식 흐름인 Lobby. (08-room.md 6절: Room ──(나가기)──→ Lobby)
        /// </summary>
        public static string ReturnSceneAfterRoom = ScenePaths.Lobby;

        /// <summary>
        /// 방장이 게임을 시작하면 방 전원이 넘어갈 씬. 방에 들어가기 직전에 진입 쪽(Lobby / DebugLobby)이 설정한다.
        /// 기본값은 인게임. DebugLobby 는 멀티 테스트용 TmpGameScene 으로 바꿀 수 있다.
        /// </summary>
        public static string GameSceneAfterRoom = ScenePaths.Game;
    }
}
