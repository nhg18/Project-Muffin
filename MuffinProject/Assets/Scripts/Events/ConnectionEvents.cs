using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connect 관련 외부로 노출되는 Event Hub
/// </summary>
public static class ConnectionEvents
{
    public static event Action<bool> OnConnected;
    
    public static void RaiseConnected(bool connected) => OnConnected?.Invoke(connected);
}
