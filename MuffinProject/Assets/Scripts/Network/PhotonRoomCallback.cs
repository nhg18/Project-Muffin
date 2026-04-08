using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonRoomCallback
{
    private readonly PhotonRoomService service;
    public PhotonRoomCallback(PhotonRoomService service)
    {
        this.service = service;
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
            service.CreateRoom();
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
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Room));
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
        service.CreateRoom();
    }
}