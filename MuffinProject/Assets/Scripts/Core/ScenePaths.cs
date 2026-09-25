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
    }
}
