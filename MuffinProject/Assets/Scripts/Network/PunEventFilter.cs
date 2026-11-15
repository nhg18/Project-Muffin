using System;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.Network
{
    /// <summary>
    /// PUN 이 받는 이벤트 중 이 게임이 쓰지 않는 것을 버린다 (09-network.md 13절).
    /// PUN 은 남이 보낸 Destroy · Instantiate · 소유권 · 직렬화 이벤트를 보낸 사람 확인 없이 처리한다 —
    /// 아무 클라나 씬의 GameServer 오브젝트를 모든 화면에서 지울 수 있다. 이 게임은 RPC(200) 만 쓴다.
    /// </summary>
    internal static class PunEventFilter
    {
        // PunEvent (PUN 내부 상수) — SendSerialize · Instantiation · CloseConnection · Destroy · RemoveCachedRPCs
        // · SendSerializeReliable · DestroyPlayer · OwnershipRequest · OwnershipTransfer · VacantViewIds · OwnershipUpdate
        private static readonly byte[] Blocked = { 201, 202, 203, 204, 205, 206, 207, 209, 210, 211, 212 };

        private static Action<EventData> _punOnEvent;
        private static object _installedOn; // 바꿔 끼운 NetworkingClient — PUN 이 새로 만들면 다시 끼운다

        /// <summary>PUN 의 이벤트 처리를 걸러 주는 처리로 바꿔 끼운다. 여러 번 불러도 한 번만.</summary>
        public static void Install()
        {
            if (_installedOn == PhotonNetwork.NetworkingClient) return;

            MethodInfo method = typeof(PhotonNetwork).GetMethod("OnEvent", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(EventData) }, null);
            if (method == null)
            {
                Debug.LogError("[PunEventFilter] PhotonNetwork.OnEvent 를 찾지 못했다 — PUN 버전이 바뀌었으면 이 파일을 고친다. 이벤트를 거르지 않는다");
                return;
            }

            _punOnEvent = (Action<EventData>)Delegate.CreateDelegate(typeof(Action<EventData>), method);
            PhotonNetwork.NetworkingClient.EventReceived -= _punOnEvent;
            PhotonNetwork.NetworkingClient.EventReceived -= Filter;
            PhotonNetwork.NetworkingClient.EventReceived += Filter;
            _installedOn = PhotonNetwork.NetworkingClient;
        }

        private static void Filter(EventData photonEvent)
        {
            if (Array.IndexOf(Blocked, photonEvent.Code) >= 0)
            {
                Debug.LogWarning($"[PunEventFilter] 쓰지 않는 PUN 이벤트를 버림 — 코드 {photonEvent.Code}, 보낸 사람 {photonEvent.Sender}");
                return;
            }

            _punOnEvent(photonEvent);
        }
    }
}
