using System;
using Photon.Realtime;
using UnityEditor.MemoryProfiler;
using UnityEngine;

public class ConnectionCallback
{
    private ConnectionService service;
    public ConnectionCallback(ConnectionService service)
    {
        this.service = service;
    }

    /// <summary>
    /// 서버 연결 완료시 호출되는 콜백 함수
    /// 서버 연결시 자동으로 로비 참가
    /// </summary>
    public void OnConnectedToMaster()
    {
        Debug.Log("On Connected To Master");
        // PhotonNetwork.JoinLobby(); // 매치 메이킹 없으면 로비 없어도 됨.
        ConnectionEvents.RaiseConnected(true);
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
}