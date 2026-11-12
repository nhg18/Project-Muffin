using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chapchu.Core;
using Chapchu.Network.Events;

namespace Chapchu.Network
{
    /// <summary>
    /// Photon 연결 · 방 요청의 창구 (09-network.md). 씬이 바뀌어도 남고, 다른 코드는 PhotonNetwork 대신 이것을 부른다.
    /// 연결은 PhotonConnection, 방은 PhotonRoom 이 하고, 결과는 Network/Events 로 알린다.
    /// </summary>
    public class NetworkManager : SingletonPersistentPun<NetworkManager>
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;
    
        private PhotonConnection _connection;
        private PhotonRoom _room;
        private Coroutine _connectTimeout;

        public static string Nickname => PhotonNetwork.NickName;
        public static bool IsConnected => PhotonNetwork.IsConnected;
        /// <summary>서버 접속이 완료되어 요청을 보낼 수 있는 상태인지</summary>
        public static bool IsReady => PhotonNetwork.IsConnectedAndReady;
        /// <summary>방 입장 응답을 받았는지 — 입장 알림(RoomEvents.OnJoinedRoom) · 씬 동기화의 방 씬 로드보다 먼저 참이 되고, 방을 떠나면 거짓</summary>
        public static bool HasEnteredRoom => PhotonNetwork.CurrentRoom != null;

        /// <summary>방장이 정한 봇 목록 — 원본 (09-network.md 3절). 방장이 아니면 null. 읽기는 BotRoster.Numbers 로.</summary>
        internal int[] HostBots => _room.HostBots;
        
        protected override void Awake()
        {
            base.Awake(); // 싱글톤 부모클래스 Awake
        
            _connection = new PhotonConnection();
            _room = new PhotonRoom();
        }
    
        private void Start()
        {
            _connection.Initialize();

            // 타이틀은 약관 동의 · 로그인 뒤에 접속한다 — 동의 전에는 어디에도 접속하지 않는다 (23-login 3절 #9).
            // 그 밖의 씬(디버그 로비 등)에서 실행하면 여기서 바로 접속한다. (GameBootstrap 이 생성)
            if (SceneManager.GetActiveScene().name == ScenePaths.Title)
                return;

            Connect();
        }
    
        // 퍼블릭 메서드
        #region Public Methods
        
        public void Initialize() => _connection.Initialize();

        /// <summary>
        /// 네트워크 접속 함수. 접속 실패 후 재시도에도 사용한다.
        /// 실패 사유는 ConnectionEvents.OnDisconnected 로 전달된다.
        /// </summary>
        public void Connect()
        {
            if (!_connection.Connect()) return;

            StopConnectTimeout();
            _connectTimeout = StartCoroutine(ConnectTimeoutRoutine());
        }
    
        /// <summary>
        /// 닉네임 설정 함수
        /// </summary>
        /// <param name="nickname">닉네임</param>
        public void SetNickname(string nickname) => _connection.SetNickname(nickname);

        /// <summary>
        /// 플레이어 식별자 — UGS 플레이어 ID (23-login 3절 #10). 접속 전에 넣는다.
        /// </summary>
        public void SetUserId(string userId) => PhotonNetwork.AuthValues = new AuthenticationValues(userId);

        /// <summary>
        /// 접속을 끊는다 — 타이틀에서 로그인한 계정을 버릴 때. 결과는 ConnectionEvents.OnDisconnected(DisconnectByClientLogic).
        /// </summary>
        public void Disconnect()
        {
            StopConnectTimeout();
            PhotonNetwork.Disconnect();
        }
    
        /// <summary>
        /// 룸 참가 함수
        /// </summary>
        public void JoinRoom(string roomName) => _room.JoinRoom(roomName);
    
        /// <summary>
        /// 랜덤 룸 참가 함수
        /// </summary>
        public void JoinRandomRoom() => _room.JoinRandomRoom();
    
        /// <summary>
        /// 룸 나가기 함수
        /// </summary>
        public void LeaveRoom() => _room.LeaveRoom();
    
        /// <summary>
        /// 룸 생성 함수
        /// </summary>
        public void CreateRoom() => _room.CreateRoom();
    
        /// <summary>
        /// 룸 옵션 생성 함수
        /// </summary>
        /// <param name="maxPlayers">최대 플레이어 수</param>
        /// <param name="isVisible">로비 노출 여부</param>
        /// <param name="isOpen">공개 비공개 여부</param>
        /// <returns>RoomOptions 객체 리턴</returns>
        public RoomOptions CreateRoomOptions(int maxPlayers, bool isVisible, bool isOpen) => _room.CreateRoomOptions(maxPlayers, isVisible, isOpen);
    
        /// <summary>
        /// 현재 룸 옵션 업데이트 함수
        /// </summary>
        /// <param name="isVisible">로비 노출 여부</param>
        /// <param name="isOpen">공개 비공개 여부</param>
        public void UpdateRoomOptions(bool isVisible, bool isOpen) => _room.UpdateRoomOptions(isVisible, isOpen);

        /// <summary>「혼자 하기」 — 봇 3 이 든 방 생성 (18-bot.md 10절). 입장은 OnJoinedRoom 으로 알린다.</summary>
        public void CreateSoloRoom() => _room.CreateSoloRoom();

        /// <summary>봇 추가 — 방장 · 시작 전 · 빈 칸 (18-bot.md 5절). 못 하면 false. 결과는 RoomEvents.OnBotsChanged.</summary>
        public bool AddBot() => _room.AddBot();

        /// <summary>봇 제거 — 방장 · 시작 전. 봇 번호(1 ~ 3)를 받는다. 못 하면 false.</summary>
        public bool RemoveBot(int botNumber) => _room.RemoveBot(botNumber);

        #endregion

        #region Connect Timeout

        private IEnumerator ConnectTimeoutRoutine()
        {
            yield return new WaitForSecondsRealtime(PhotonConnection.ConnectTimeoutSeconds);
            _connectTimeout = null;
            _connection.OnConnectTimeout();
        }

        // 마스터 접속 이후(룸 입장 중 서버 이동 등)에 타임아웃이 끼어들지 않도록 접속 완료 · 끊김 시 반드시 멈춘다.
        private void StopConnectTimeout()
        {
            if (_connectTimeout == null) return;
            StopCoroutine(_connectTimeout);
            _connectTimeout = null;
        }

        #endregion

        // 콜백 함수
        #region Pun Callbacks Functions

        public override void OnConnectedToMaster()
        {
            StopConnectTimeout();
            _connection.OnConnectedToMaster();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            StopConnectTimeout();
            _connection.OnDisconnected(cause);
        }
    
        public override void OnCreatedRoom() => _room.OnCreatedRoom();

        public override void OnCreateRoomFailed(short returnCode, string message) => _room.OnCreateRoomFailed(returnCode, message);
    
        public override void OnJoinedRoom() => _room.OnJoinedRoom();
    
        public override void OnLeftRoom() => _room.OnLeftRoom();
    
        public override void OnJoinRoomFailed(short returnCode, string message) => _room.OnJoinRoomFailed(returnCode, message);
    
        public override void OnJoinRandomFailed(short returnCode, string message) => _room.OnJoinRandomFailed(returnCode, message);
    
        public override void OnPlayerEnteredRoom(Player newPlayer) => _room.OnPlayerEntered(newPlayer);
    
        public override void OnPlayerLeftRoom(Player otherPlayer) => _room.OnPlayerLeft(otherPlayer);

        public override void OnMasterClientSwitched(Player newMasterClient) => _room.OnMasterClientSwitched(newMasterClient);
    
        public override void OnRoomListUpdate(List<RoomInfo> roomList) => _room.OnRoomListUpdate(roomList);

        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) => _room.OnRoomPropertiesUpdate(propertiesThatChanged);

        #endregion
    }
}
