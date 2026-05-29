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
            if (slotIndex >= slotParents.Length)
            {
                slotIndex = 0;
            }

            var ui = Instantiate(playerInfoPrefab, slotParents[slotIndex]);
            actorToUI[player.ActorNumber] = ui;

            var data = PlayerInfoData.FromPhoton(player, player.CustomProperties);
            ui.Refresh(data);

            slotIndex++;
        }
    }

    // ─────────────────────────────────────────
    // OtherPlayerHands에서 호출하는 메서드
    // ─────────────────────────────────────────

    /// <summary>
    /// 특정 플레이어 UI 갱신 — OnPlayerPropertiesUpdate에서 호출
    /// </summary>
    public void RefreshPlayerInfo(Player player)
    {
        if (!actorToUI.TryGetValue(player.ActorNumber, out var ui)) return;

        var data = PlayerInfoData.FromPhoton(player, player.CustomProperties);
        ui.Refresh(data);
    }
}
