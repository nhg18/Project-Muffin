using Photon.Realtime;
using UnityEngine;

public class PhotonConnectionCallback
{
    /// <summary>
    /// 서버 연결 완료시 호출되는 콜백 함수
    /// 서버 연결시 자동으로 로비 참가
    /// </summary>
    public void OnConnectedToMaster()
    {
        Debug.Log("On Connected To Master");
        // PhotonNetwork.JoinLobby(); // 매치 메이킹 없으면 로비 업서도 됨.
    }

    /// <summary>
    /// 서버 연결 끊어졌을 때 호출되는 콜백 함수
    /// </summary>
    /// <param name="cause">
    /// 끊긴 사유가 담긴 Enum 집합체
    /// </param>
    public void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"On Disconnected: {cause}");
    }
}