using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class RoomInfoPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text playerInfoText;
    [SerializeField] private TMP_Text logText;
    
    private void OnEnable()
    {
        RoomEventHub.OnPlayerEnteredEvent += OnPlayerEntered;
        RoomEventHub.OnPlayerLeftEvent += OnPlayerLeft;
        
        if (PhotonNetwork.InRoom)
        {
            OnJoined();
        }
    }

    private void OnDisable()
    {
        RoomEventHub.OnPlayerEnteredEvent -= OnPlayerEntered;
        RoomEventHub.OnPlayerLeftEvent -= OnPlayerLeft;
    }
    
    private void UpdateRoomInfo()
    {
        Player[] players = PhotonNetwork.PlayerList;
        Room room = PhotonNetwork.CurrentRoom;

        playerCountText.text = "플레이어 ( " + room.PlayerCount + " / " + room.MaxPlayers + " )";
        roomCodeText.text = "Room Code : " + room.Name;

        playerInfoText.text = "";
        foreach (var player in players)
        {
            playerInfoText.text += player.NickName + (player.IsMasterClient ? "(Host)" : "") + "\n";
        }
    }

    private void OnJoined()
    {
        AddLog($"{NetworkManager.Nickname}님이 입장했습니다.");
        UpdateRoomInfo();
    }
    
    private void OnPlayerEntered(Player newPlayer)
    {
        AddLog($"{newPlayer.NickName}님이 입장했습니다.");
        UpdateRoomInfo();
    }

    private void OnPlayerLeft(Player oldPlayer)
    {
        AddLog($"{oldPlayer.NickName}님이 퇴장했습니다.");
        UpdateRoomInfo();
    }

    private void AddLog(string text)
    {
        logText.text += "\n" + text;
    }
}
