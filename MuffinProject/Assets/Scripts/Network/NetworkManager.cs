using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

namespace Network
{
    public class NetworkManager : SingletonPersistentPun<NetworkManager>
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 6;
    
        public static string Nickname => PhotonNetwork.NickName;
        public static bool IsConnected => PhotonNetwork.IsConnected;
    
        private PhotonConnection _connection;
        private PhotonRoom _room;
        protected override void Awake()
        {
            base.Awake(); // 싱글톤 부모클래스 Awake
        
            _connection = new PhotonConnection();
            _room = new PhotonRoom();
        
            _connection.SetupPhotonNetwork(); // 네트워크 접속 전 세팅해줘야 함
        }
    
        private void Start()
        {
            // _connection.Connect();
        }
    
        // 퍼블릭 메서드
        #region Public Methods

        /// <summary>
        /// 네트워크 접속 함수
        /// </summary>
        public void Connect() => _connection.Connect();
    
        /// <summary>
        /// 닉네임 설정 함수
        /// </summary>
        /// <param name="nickname">닉네임</param>
        public void SetNickname(string nickname) => _connection.SetNickname(nickname);
    
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
    
        #endregion
    
        // 콜백 함수
        #region Pun Callbacks Functions
    
        public override void OnConnectedToMaster() => _connection.OnConnectedToMaster();

        public override void OnDisconnected(DisconnectCause cause) => _connection.OnDisconnected(cause);
    
        public override void OnCreatedRoom() => _room.OnCreatedRoom();

        public override void OnCreateRoomFailed(short returnCode, string message) => _room.OnCreateRoomFailed(returnCode, message);
    
        public override void OnJoinedRoom() => _room.OnJoinedRoom();
    
        public override void OnLeftRoom() => _room.OnLeftRoom();
    
        public override void OnJoinRoomFailed(short returnCode, string message) => _room.OnJoinRoomFailed(returnCode, message);
    
        public override void OnJoinRandomFailed(short returnCode, string message) => _room.OnJoinRandomFailed(returnCode, message);
    
        public override void OnPlayerEnteredRoom(Player newPlayer) => _room.OnPlayerEntered(newPlayer);
    
        public override void OnPlayerLeftRoom(Player otherPlayer) => _room.OnPlayerLeft(otherPlayer);
    
        public override void OnRoomListUpdate(List<RoomInfo> roomList) => _room.OnRoomListUpdate(roomList);

        #endregion
    }
}
