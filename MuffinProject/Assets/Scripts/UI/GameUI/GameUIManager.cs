using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameUIManager : Singleton<GameUIManager>
{
    [Header("다른 플레이어 정보 UI")]
    [SerializeField] private GamePlayerInfoUI playerInfoPrefab;
    [SerializeField] private Transform[] slotParents =  new Transform[4];

    private readonly Dictionary<int, GamePlayerInfoUI> actorToUI = new();
    private readonly Dictionary<int, PlayerInfoData> cachedData = new();

    // ─────────────────────────────────────────
    // 시작 시 전체 생성
    // ─────────────────────────────────────────

    private void Start()
    {
        var playerList = PhotonNetwork.PlayerList; // ActorNumber 순 정렬
    
        // 로컬 플레이어의 인덱스 찾기
        int localIndex = System.Array.FindIndex(playerList, p => p.IsLocal);
        int slotIndex = 1;

        // 로컬 플레이어 다음부터 순환하며 슬롯 배정
        for (int i = 1; i < playerList.Length; i++)
        {
            int wrappedIndex = (localIndex + i) % playerList.Length;
            var player = playerList[wrappedIndex];
            if (slotIndex >= slotParents.Length) slotIndex = 0;
            
            var ui = Instantiate(playerInfoPrefab, slotParents[slotIndex]);
            actorToUI[player.ActorNumber] = ui;

            cachedData[player.ActorNumber] = new PlayerInfoData
            {
                Nickname = player.NickName
            };
            
            ui.Refresh(cachedData[player.ActorNumber]);

            slotIndex++;
        }
    }

    // ─────────────────────────────────────────
    // OtherPlayerHands에서 호출하는 메서드
    // ─────────────────────────────────────────

    /// <summary>
    /// 특정 플레이어 UI 갱신 — OnPlayerPropertiesUpdate에서 호출
    /// </summary>
    public void RefreshPlayerInfo(Player player, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (!actorToUI.TryGetValue(player.ActorNumber, out var ui)) return;

        // 기존 캐시 가져오기 (없으면 새로 생성)
        if (!cachedData.TryGetValue(player.ActorNumber, out var data))
            data = new PlayerInfoData();

        // 닉네임은 항상 최신으로
        data.Nickname = player.NickName;

        // changedProps에 있는 키만 덮어쓰기 (없는 키는 이전 값 유지)
        if (changedProps.TryGet("PlayerHP",   out int hp))    data.HP         = hp;
        if (changedProps.TryGet("CardsCount", out int cards)) data.CardsCount = cards;
        if (changedProps.TryGet("isChapChu",  out bool trap)) data.IsChapChu  = trap;

        cachedData[player.ActorNumber] = data;
        ui.Refresh(data);
    }
}
