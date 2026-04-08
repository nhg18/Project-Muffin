using Photon.Pun;
using Photon.Realtime;

public class RoomService
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
        RoomOptions options = CreateRoomOptions(NetworkManager.MaxPlayers, true, true);
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
}