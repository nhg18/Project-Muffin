using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : SingletonPersistentPun<NetworkManager>
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 6;
    
    public static string Nickname => PhotonNetwork.NickName;
    public static bool IsConnected => PhotonNetwork.IsConnected;
    
    protected override void Awake()
    {
        base.Awake(); // 싱글톤 부모클래스 Awake
        
        SetupPhotonNetwork(); // 네트워크 접속 전 세팅해줘야 함
    }
    
    private void Start()
    {
        // connection.service.Connect(); // 네트워크 접속 -> 접속 버튼 대체
        
        SetupInitNickname(); // 초기 닉네임 설정
    }
    
    /// <summary>
    /// 네트워크 접속 전 환경 세팅 함수
    /// </summary>
    private void SetupPhotonNetwork()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }
    
    /// <summary>
    /// 네트워크 접속 함수
    /// </summary>
    private void Connect()
    {
        PhotonNetwork.ConnectUsingSettings();
    }
    
    /// <summary>
    /// 네트워크 접속시 초기 닉네임 설정
    /// 기존에 설정해둔 닉네임이 있다면 불러와서 적용
    /// Connect 이후에 사용해야 함
    /// </summary>
    private void SetupInitNickname()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKeys.playerName)) return;
        
        string defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);

        if (string.IsNullOrEmpty(defaultName)) return; // null empty 체크
        if (!PhotonNetwork.IsConnected) return; // 연결 체크
        
        SetNickname(defaultName);
    }
    
    // 퍼블릭 함수
    #region Public Functions
    
    /// <summary>
    /// 닉네임 설정 함수
    /// </summary>
    /// <param name="nickname">닉네임</param>
    public void SetNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogError("Nickname cannot be null or empty");
            return;
        }
        
        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString(PlayerPrefsKeys.playerName, nickname);
    }
    
    /// <summary>
    /// 룸 참가 함수
    /// </summary>
    public void JoinRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            RoomEvents.RaiseJoinRoomFailed(ErrorCode.InvalidOperation, "방 코드가 없습니다.");
            return;
        }
        PhotonNetwork.JoinRoom(roomName.Trim().ToUpper());
    }
    
    /// <summary>
    /// 랜덤 룸 참가 함수
    /// </summary>
    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }
    
    /// <summary>
    /// 룸 나가기 함수
    /// </summary>
    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }
    
    /// <summary>
    /// 룸 생성 함수
    /// </summary>
    public void CreateRoom()
    {
        RoomOptions options = CreateRoomOptions(MaxPlayers, true, true);
        string code = RandomCode.GenerateRandomCode();
        
        PhotonNetwork.CreateRoom(code.Trim().ToUpper(), options);
    }
    
    /// <summary>
    /// 룸 옵션 생성 함수
    /// </summary>
    /// <param name="maxPlayers">최대 플레이어 수</param>
    /// <param name="isVisible">로비 노출 여부</param>
    /// <param name="isOpen">공개 비공개 여부</param>
    /// <returns>RoomOptions 객체 리턴</returns>
    public RoomOptions CreateRoomOptions(int maxPlayers, bool isVisible, bool isOpen)
    {
        return new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = isVisible,
            IsOpen = isOpen,
        };
    }

    /// <summary>
    /// 룸 옵션 업데이트 함수
    /// </summary>
    /// <param name="isVisible">로비 노출 여부</param>
    /// <param name="isOpen">공개 비공개 여부</param>
    public void UpdateRoomOptions(bool isVisible, bool isOpen)
    {
        PhotonNetwork.CurrentRoom.IsVisible = isVisible;
        PhotonNetwork.CurrentRoom.IsOpen = isOpen;
    }
    
    #endregion
    
    // 콜백 함수
    #region Pun Callbacks Functions
    
    /// <summary>
    /// 서버 연결 완료시 호출되는 콜백 함수
    /// 서버 연결시 자동으로 로비 참가
    /// </summary>
    public override void OnConnectedToMaster()
    {
        Debug.Log("On Connected To Master");
        ConnectionEvents.RaiseConnected(true);
        PopupManager.Instance.Hide();
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
    }

    /// <summary>
    /// 서버 연결 끊어졌을 때 호출되는 콜백 함수
    /// </summary>
    /// <param name="cause">
    /// 끊긴 사유가 담긴 Enum 집합체
    /// </param>
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"On Disconnected: {cause}");
        ConnectionEvents.RaiseConnected(false);

        switch (cause)
        {
            // 클라이언트에 의해 의도적 종료
            case DisconnectCause.DisconnectByClientLogic:
                // SceneManager.LoadScene(SceneType.Title);
                break;

            // 서버, 클라이언트 타임 아웃, 일시 끊김
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.Exception:
                // TryReconnect();
                break;

            // 서버가 강제 종료 → 이유 표시 후 타이틀
            case DisconnectCause.DisconnectByServerLogic:
            case DisconnectCause.InvalidAuthentication:
                // ShowErrorAndGoTitle(cause);
                break;
        }
    }
    
    /// <summary>
    /// 룸 생성 완료시 호출되는 콜백 함수
    /// </summary>
    public override void OnCreatedRoom()
    {
        Debug.Log("On Created Room" + PhotonNetwork.CurrentRoom.Name);
        RoomEvents.RaiseCreatedRoom();
    }
    
    /// <summary>
    /// 룸 생성 실패시 호출되는 콜백 함수
    /// </summary>
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log($"On Create Room Failed [{returnCode}]: {message}");

        switch (returnCode)
        {
            // 랜덤 코드로 방 생성시 겹치는 문제시 CreateRoom 함수 호출
            case 32766:
                CreateRoom();
                return;
        }
        RoomEvents.RaiseCreateRoomFailed(returnCode, message);
    }
    
    /// <summary>
    /// 룸 참가시 호출되는 콜백 함수
    /// 룸 씬 로드
    /// </summary>
    public override void OnJoinedRoom()
    {
        Debug.Log("On Joined Room");
        RoomEvents.RaiseJoinedRoom();
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Room));
        PopupManager.Instance.HideAll();
    }
    
    /// <summary>
    /// 룸 나가기시 호출되는 콜백 함수
    /// 타이틀 씬 로드
    /// </summary>
    public override void OnLeftRoom()
    {
        Debug.Log("On Left Room");
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
    }
    
    /// <summary>
    /// 룸 참가 실패시 호출되는 콜백 함수
    /// </summary>
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"On Join Room Failed: {message}");
        RoomEvents.RaiseJoinRoomFailed(returnCode, message);
    }
    
    /// <summary>
    /// 랜덤 룸 참가 실패시 호출되는 콜백 함수
    /// </summary>
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"On Join Random Room Failed: {message}");
        Debug.Log("No Empty Room -> Create New Room");
        CreateRoom();
    }
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RoomEvents.RaisePlayerEntered(newPlayer);
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RoomEvents.RaisePlayerLeft(otherPlayer);
    }

    
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        RoomEvents.RaiseRoomListUpdate(roomList);
    }

    #endregion
}
