using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : SingletonPersistentPun<NetworkManager>
{
    public const int minPlayers = 2;
    public const int maxPlayers = 6;
    
    public static string Nickname => PhotonNetwork.NickName;
    
    private PhotonConnection connection;
    private PhotonRoom room;
    protected override void Awake()
    {
        base.Awake(); // 싱글톤 부모클래스 Awake
        
        connection = new PhotonConnection();
        room = new PhotonRoom();
        
        connection.SetupPhotonNetwork(); // 네트워크 접속 전 세팅해줘야 함

        // PlayerPrefs 저장된 닉네임이 존재하면 닉네임 설정
        if (!PlayerPrefs.HasKey(PlayerPrefsKeys.playerName)) return;
        String defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);
        SetNickname(defaultName);
    }
    
    private void Start()
    {
        connection.Connect(); // 네트워크 접속
    }
    
    // 퍼블릭 메서드
    #region Public Methods
    
    public void SetNickname(string nickname) => connection.SetNickname(nickname);
    
    public void JoinRoom(string roomName) => room.JoinRoom(roomName);
    
    public void JoinRandomRoom() => room.JoinRandomRoom();
    
    public void LeaveRoom() => room.LeaveRoom();
    
    public void CreateRoom() => room.CreateRoom();
    
    public RoomOptions CreateRoomOptions(int maxPlayers, bool isVisible, bool isOpen) => room.CreateRoomOptions(maxPlayers, isVisible, isOpen);
    
    public void UpdateRoomOptions(bool isVisible, bool isOpen) => room.UpdateRoomOptions(isVisible, isOpen);

    #endregion
    
    // 콜백 함수
    #region Pun Callbacks Functions
    
    public override void OnConnectedToMaster() => connection.OnConnectedToMaster();

    public override void OnDisconnected(DisconnectCause cause) => connection.OnDisconnected(cause);
    
    public override void OnCreatedRoom() => room.OnCreatedRoom();

    public override void OnCreateRoomFailed(short returnCode, string message) => room.OnCreateRoomFailed(returnCode, message);
    
    public override void OnJoinedRoom() => room.OnJoinedRoom();
    
    public override void OnLeftRoom() => room.OnLeftRoom();
    
    public override void OnJoinRoomFailed(short returnCode, string message) => room.OnJoinRoomFailed(returnCode, message);
    
    public override void OnJoinRandomFailed(short returnCode, string message) => room.OnJoinRandomFailed(returnCode, message);
    
    public override void OnPlayerEnteredRoom(Player newPlayer) => RoomEventHub.RaisePlayerEntered(newPlayer);
    
    public override void OnPlayerLeftRoom(Player otherPlayer) => RoomEventHub.RaisePlayerLeft(otherPlayer);
    
    public override void OnRoomListUpdate(List<RoomInfo> roomList) => RoomEventHub.RaiseRoomUpdateList(roomList);

    #endregion
}
