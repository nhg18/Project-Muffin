using System.Collections.Generic;
using Chapchu.Network;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace Chapchu.UI
{

    public class RoomInfoPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text playerInfoText;
        [SerializeField] private TMP_Text logText;

        private const int MaxLogLines = 30;
        private readonly Queue<string> _logLines = new Queue<string>();

        private void OnEnable()
        {
            RoomEvents.OnPlayerEntered += OnPlayerEntered;
            RoomEvents.OnPlayerLeft += OnPlayerLeft;
            RoomEvents.OnMasterClientSwitched += OnMasterClientSwitched;

            if (PhotonNetwork.InRoom)
            {
                OnJoined();
            }
        }

        private void OnDisable()
        {
            RoomEvents.OnPlayerEntered -= OnPlayerEntered;
            RoomEvents.OnPlayerLeft -= OnPlayerLeft;
            RoomEvents.OnMasterClientSwitched -= OnMasterClientSwitched;
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

        // (Host) 표기 갱신. OnPlayerLeft 와의 순서가 보장되지 않아 따로 구독한다. 로그 문구는 08-room #4 미정이라 남기지 않는다.
        private void OnMasterClientSwitched(Player newMaster)
        {
            UpdateRoomInfo();
        }

        private void AddLog(string text)
        {
            _logLines.Enqueue(text);
            while (_logLines.Count > MaxLogLines)
                _logLines.Dequeue();

            logText.text = string.Join("\n", _logLines);
        }
    }
}
