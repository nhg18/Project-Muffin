using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Room에서 외부로 노출되는 Event Hub
/// </summary>
public static class RoomEventHub
{
    public static event Action<Player> OnPlayerEnteredEvent;
    public static event Action<Player> OnPlayerLeftEvent;
    public static event Action<List<RoomInfo>> OnRoomListUpdateEvent;
    
    /// <summary>
    /// 다른 플레이어가 룸에 참가시 호출되는 콜백 함수에 연결
    /// event Action에 Invoke 
    /// </summary>
    public static void RaisePlayerEntered(Player newPlayer)
    {
        OnPlayerEnteredEvent?.Invoke(newPlayer);
    }
    
    /// <summary>
    /// 다른 플레이어가 룸 나가기시 호출되는 콜백 함수에 연결
    /// event Action에 Invoke 
    /// </summary>
    public static void RaisePlayerLeft(Player newPlayer)
    {
        OnPlayerLeftEvent?.Invoke(newPlayer);
    }

    /// <summary>
    /// 룸 리스트 중 정보가 변경된 룸 리스트
    /// </summary>
    /// <param name="roomList">List RoomInfo</param>
    public static void RaiseRoomUpdateList(List<RoomInfo> roomList)
    {
        OnRoomListUpdateEvent?.Invoke(roomList);
    }
}
