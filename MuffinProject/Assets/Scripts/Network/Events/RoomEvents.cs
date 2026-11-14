using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

namespace Chapchu.Network.Events
{
    /// <summary>방 만들기 · 입장 · 나가기 · 인원 변화 통지 (08-room.md). UI 는 Photon 콜백 대신 이것을 구독한다 (CODE_CONVENTION 6절).</summary>
    public static class RoomEvents
    {
        public static event Action OnCreatedRoom;
        public static event Action<short, string> OnCreateRoomFailed;
        public static event Action OnJoinedRoom;
        public static event Action OnLeftRoom;
        public static event Action<short, string> OnJoinRoomFailed;
        public static event Action<short, string> OnJoinRandomFailed;
        public static event Action<Player> OnPlayerEntered;
        public static event Action<Player> OnPlayerLeft;
        public static event Action<List<RoomInfo>> OnRoomListUpdate;
        /// <summary>방장(마스터 클라이언트)이 바뀜. OnPlayerLeft 와의 호출 순서는 보장되지 않는다.</summary>
        public static event Action<Player> OnMasterClientSwitched;
        /// <summary>방의 봇 목록 또는 봇 난이도가 바뀜 (18-bot.md 5절) — 봇 번호 (오름차순). 난이도는 BotRoster.LevelOf 로 읽는다. 방 화면이 칸 · 로그를 갱신한다.</summary>
        public static event Action<int[]> OnBotsChanged;
    
        public static void RaiseCreatedRoom() => OnCreatedRoom?.Invoke();
        public static void RaiseCreateRoomFailed(short returnCode, string message) => OnCreateRoomFailed?.Invoke(returnCode, message);
        public static void RaiseJoinedRoom() => OnJoinedRoom?.Invoke();
        public static void RaiseLeftRoom() => OnLeftRoom?.Invoke();
        public static void RaiseJoinRoomFailed(short returnCode, string message) => OnJoinRoomFailed?.Invoke(returnCode, message);
        public static void RaiseJoinRandomFailed(short returnCode, string message) => OnJoinRandomFailed?.Invoke(returnCode, message);
        public static void RaisePlayerEntered(Player newPlayer) => OnPlayerEntered?.Invoke(newPlayer);
        public static void RaisePlayerLeft(Player newPlayer) => OnPlayerLeft?.Invoke(newPlayer);
        public static void RaiseRoomListUpdate(List<RoomInfo> roomList) => OnRoomListUpdate?.Invoke(roomList);
        public static void RaiseMasterClientSwitched(Player newMasterClient) => OnMasterClientSwitched?.Invoke(newMasterClient);
        public static void RaiseBotsChanged(int[] botNumbers) => OnBotsChanged?.Invoke(botNumbers);
    }
}
