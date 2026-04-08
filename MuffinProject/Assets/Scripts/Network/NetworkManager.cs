using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : SingletonPersistentPun<NetworkManager>
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 6;
    
    public static string Nickname => PhotonNetwork.NickName;
    
    private PhotonConnection connection;
    private PhotonRoom room;
    protected override void Awake()
    {
        base.Awake(); // 싱글톤 부모클래스 Awake
        
        connection = new PhotonConnection();
        room = new PhotonRoom();
        
        connection.service.SetupPhotonNetwork(); // 네트워크 접속 전 세팅해줘야 함
    }
    
    private void Start()
    {
        connection.service.Connect(); // 네트워크 접속
        
        connection.service.SetupInitNickname(); // 초기 닉네임 설정
    }
    
    // 퍼블릭 메서드
    #region Public Methods
    
    public void SetNickname(string nickname) => connection.service.SetNickname(nickname);
    
    public void JoinRoom(string roomName) => room.service.JoinRoom(roomName);
    
    public void JoinRandomRoom() => room.service.JoinRandomRoom();
    
    public void LeaveRoom() => room.service.LeaveRoom();
    
    public void CreateRoom() => room.service.CreateRoom();
    
    public RoomOptions CreateRoomOptions(int maxPlayers, bool isVisible, bool isOpen) => room.service.CreateRoomOptions(maxPlayers, isVisible, isOpen);
    
    public void UpdateRoomOptions(bool isVisible, bool isOpen) => room.service.UpdateRoomOptions(isVisible, isOpen);

    #endregion
    
    // 콜백 함수
    #region Pun Callbacks Functions
    
    public override void OnConnectedToMaster() => connection.callback.OnConnectedToMaster();

    public override void OnDisconnected(DisconnectCause cause) => connection.callback.OnDisconnected(cause);
    
    public override void OnCreatedRoom() => room.callback.OnCreatedRoom();

    public override void OnCreateRoomFailed(short returnCode, string message) => room.callback.OnCreateRoomFailed(returnCode, message);
    
    public override void OnJoinedRoom() => room.callback.OnJoinedRoom();
    
    public override void OnLeftRoom() => room.callback.OnLeftRoom();
    
    public override void OnJoinRoomFailed(short returnCode, string message) => room.callback.OnJoinRoomFailed(returnCode, message);
    
    public override void OnJoinRandomFailed(short returnCode, string message) => room.callback.OnJoinRandomFailed(returnCode, message);
    
    public override void OnPlayerEnteredRoom(Player newPlayer) => RoomEventHub.RaisePlayerEntered(newPlayer);
    
    public override void OnPlayerLeftRoom(Player otherPlayer) => RoomEventHub.RaisePlayerLeft(otherPlayer);
    
    public override void OnRoomListUpdate(List<RoomInfo> roomList) => RoomEventHub.RaiseRoomUpdateList(roomList);

    #endregion
}
