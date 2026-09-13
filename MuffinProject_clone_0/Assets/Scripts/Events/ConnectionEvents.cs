using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Connect 관련 외부로 노출되는 Event Hub
/// </summary>
public static class ConnectionEvents
{
    public static event Action OnConnected;
    public static event Action<DisconnectCause> OnDisconnected;
    public static void RaiseConnected() => OnConnected?.Invoke();
    public static void RaiseDisconnected(DisconnectCause cause) => OnDisconnected?.Invoke(cause);
}
