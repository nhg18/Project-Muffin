using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Room에서 외부로 노출되는 Event Bus
/// </summary>
public static class RoomEvents
{
    public static event Action<Player> OnPlayerEntered;
    public static event Action<Player> OnPlayerLeft;
    public static event Action<List<RoomInfo>> OnRoomListUpdated;
    
    public static void RaisePlayerEntered(Player newPlayer) => OnPlayerEntered?.Invoke(newPlayer);
    
    public static void RaisePlayerLeft(Player newPlayer) => OnPlayerLeft?.Invoke(newPlayer);

    public static void RaiseRoomListUpdate(List<RoomInfo> roomList) => OnRoomListUpdated?.Invoke(roomList);
}
