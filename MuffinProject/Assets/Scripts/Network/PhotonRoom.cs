using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonRoom
{
    /// <summary>
    /// 룸 참가 함수
    /// </summary>
    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
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
        RoomOptions options = CreateRoomOptions(NetworkManager.maxPlayers, true, true);
        string code = RandomCode.GenerateRandomCode();
        PhotonNetwork.CreateRoom(code, options);
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

    /// <summary>
    /// 룸 생성 완료시 호출되는 콜백 함수
    /// </summary>
    public void OnCreatedRoom()
    {
        Debug.Log("On Created Room");
    }

    /// <summary>
    /// 룸 생성 실패시 호출되는 콜백 함수
    /// </summary>
    public void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log($"On Create Room Failed [{returnCode}]: {message}");

        // 랜덤 코드로 방 생성시 겹치는 문제시 CreateRoom 함수 호출
        if (returnCode == 32766)
        {
            CreateRoom();
            return;
        }
    }
    
    /// <summary>
    /// 룸 참가시 호출되는 콜백 함수
    /// 룸 씬 로드
    /// </summary>
    public void OnJoinedRoom()
    {
        Debug.Log("On Joined Room");
        SceneManager.LoadScene("Scenes/RoomScene");
    }
    
    /// <summary>
    /// 룸 나가기시 호출되는 콜백 함수
    /// 타이틀 씬 로드
    /// </summary>
    public void OnLeftRoom()
    {
        Debug.Log("On Left Room");
        SceneManager.LoadScene("Scenes/LobbyScene");
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
        CreateRoom();
    }
}
