using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

namespace Chapchu.Network.Events
{
    /// <summary>서버 연결 결과 통지 (09-network.md). UI 는 Photon 콜백 대신 이것을 구독한다 (CODE_CONVENTION 6절).</summary>
    public static class ConnectionEvents
    {
        public static event Action OnConnected;
        public static event Action<DisconnectCause> OnDisconnected;
        public static void RaiseConnected() => OnConnected?.Invoke();
        public static void RaiseDisconnected(DisconnectCause cause) => OnDisconnected?.Invoke(cause);
    }
}
