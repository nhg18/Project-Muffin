using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomCallback
{
    private readonly RoomService _service;
    public RoomCallback(RoomService service)
    {
        this._service = service;
    }

    /// <summary>
    /// 룸 생성 완료시 호출되는 콜백 함수
    /// </summary>
    public void OnCreatedRoom()
    {
        Debug.Log("On Created Room" + PhotonNetwork.CurrentRoom.Name);
        RoomEvents.RaiseRoomCreated();
    }

    /// <summary>
    /// 룸 생성 실패시 호출되는 콜백 함수
    /// </summary>
    public void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log($"On Create Room Failed [{returnCode}]: {message}");

        switch (returnCode)
        {
            // 랜덤 코드로 방 생성시 겹치는 문제시 CreateRoom 함수 호출
            case 32766:
                _service.CreateRoom();
                return;
        }
        RoomEvents.RaiseRoomCreateFailed();
    }
    
    /// <summary>
    /// 룸 참가시 호출되는 콜백 함수
    /// 룸 씬 로드
    /// </summary>
    public void OnJoinedRoom()
    {
        Debug.Log("On Joined Room");
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Room));
        RoomEvents.RaiseRoomJoined();
    }
    
    /// <summary>
    /// 룸 나가기시 호출되는 콜백 함수
    /// 타이틀 씬 로드
    /// </summary>
    public void OnLeftRoom()
    {
        Debug.Log("On Left Room");
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
    }
    
    /// <summary>
    /// 룸 참가 실패시 호출되는 콜백 함수
    /// </summary>
    public void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"On Join Room Failed: {message}");
    }

    /// <summary>
    /// 랜덤 룸 참가 실패시 호출되는 콜백 함수
    /// </summary>
    public void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"On Join Random Room Failed: {message}");
        Debug.Log("No Empty Room -> Create New Room");
        _service.CreateRoom();
    }

    public void OnPlayerEntered(Player player)
    {
        RoomEvents.RaisePlayerEntered(player);
    }

    public void OnPlayerLeft(Player player)
    {
        RoomEvents.RaisePlayerLeft(player);
    }

    public void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        RoomEvents.RaiseRoomListUpdate(roomList);
    }
}