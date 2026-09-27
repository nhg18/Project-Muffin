using System.Collections.Generic;
using Chapchu.Core;
using Chapchu.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 방 뷰와 네트워크 · 씬 전환을 잇는다. Scripts/UI/RoomScreen 에서 네트워크를 아는 유일한 파일이다 (LobbyPresenter 와 같은 구조).
    /// 기준 문서: docs/systems/16-room-ui.md 5 · 6절, docs/systems/08-room.md 5절
    /// </summary>
    [RequireComponent(typeof(RoomView))]
    public class RoomPresenter : MonoBehaviour
    {
        private RoomView _view;

        // 방 입장 시 1회 초기화. 씬이 먼저 뜨고 OnJoinedRoom 이 나중에 오는 경우(AutomaticallySyncScene 으로 끌려온 참가자)와
        // 이미 방에 있는 채로 씬이 뜨는 경우(방장) 둘 다 여기로 모은다.
        private bool _initialized;

        private void Awake()
        {
            _view = GetComponent<RoomView>();
        }

        private void OnEnable()
        {
            _view.StartRequested += HandleStartRequested;
            _view.LeaveRequested += HandleLeaveRequested;

            RoomEvents.OnPlayerEntered += HandlePlayerEntered;
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
            // 방장이 나가면 PUN 이 새 방장을 정한다. OnPlayerLeft 와의 순서가 보장되지 않으므로 둘 다에서 갱신한다.
            RoomEvents.OnMasterClientSwitched += HandleMasterClientSwitched;
            RoomEvents.OnJoinedRoom += HandleJoinedRoom;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
        }

        private void OnDisable()
        {
            _view.StartRequested -= HandleStartRequested;
            _view.LeaveRequested -= HandleLeaveRequested;

            RoomEvents.OnPlayerEntered -= HandlePlayerEntered;
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
            RoomEvents.OnMasterClientSwitched -= HandleMasterClientSwitched;
            RoomEvents.OnJoinedRoom -= HandleJoinedRoom;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
        }

        private void Start()
        {
            if (PhotonNetwork.InRoom)
                Initialize();
        }

        private void HandleJoinedRoom() => Initialize();

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _view.SetRoomCode(PhotonNetwork.CurrentRoom.Name);
            _view.AddLog($"{NetworkManager.Nickname}님이 입장했습니다.");
            Refresh();
        }

        private void HandlePlayerEntered(Player player)
        {
            _view.AddLog($"{player.NickName}님이 입장했습니다.");
            Refresh();
        }

        private void HandlePlayerLeft(Player player)
        {
            _view.AddLog($"{player.NickName}님이 퇴장했습니다.");
            Refresh();
        }

        private void HandleMasterClientSwitched(Player newMaster)
        {
            _view.AddLog($"{newMaster.NickName}님이 방장이 되었습니다.");
            Refresh();
        }

        private void HandleStartRequested()
        {
            if (!PhotonNetwork.IsMasterClient || !CanStartGame()) return;

            NetworkManager.Instance.UpdateRoomOptions(isVisible: false, isOpen: false);
            // 방 전원이 함께 넘어가야 하므로 LoadScene 이 아닌 LoadLevel (ScenePaths 설명 참고).
            PhotonNetwork.LoadLevel(SceneFlow.GameSceneAfterRoom);
        }

        private void HandleLeaveRequested()
        {
            NetworkManager.Instance.LeaveRoom();
        }

        private void HandleLeftRoom()
        {
            // 들어온 곳으로 돌아간다. Lobby 에서 왔으면 Lobby, DebugLobby 에서 왔으면 DebugLobby.
            SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);
        }

        // 08-room.md 5절 #2: 최소 인원 2명 (준비 상태 없음)
        private static bool CanStartGame() => PhotonNetwork.CurrentRoom.PlayerCount >= NetworkManager.MinPlayers;

        private void Refresh()
        {
            if (!PhotonNetwork.InRoom) return;

            // PlayerList 는 ActorNumber 순 — 들어온 순서대로 칸을 채운다.
            Player[] players = PhotonNetwork.PlayerList;
            var entries = new List<(string name, bool isHost)>(players.Length);
            foreach (Player player in players)
                entries.Add((player.NickName, player.IsMasterClient));

            _view.SetPlayers(entries);
            _view.SetPlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
            _view.SetStartButton(PhotonNetwork.IsMasterClient, CanStartGame());
        }
    }
}
