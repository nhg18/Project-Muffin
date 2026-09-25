using Chapchu.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Chapchu.Core;
using UnityEngine.SceneManagement;

namespace Chapchu.UI
{

    public class RoomPanel : MonoBehaviour
    {
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button startButton;

        private void OnEnable()
        {
            RoomEvents.OnLeftRoom += OnLeftRoom;
            RoomEvents.OnPlayerEntered += RefreshStartButton;
            RoomEvents.OnPlayerLeft += RefreshStartButton;
            // 방장이 나가면 PUN 이 새 방장을 정한다. OnPlayerLeft 와의 순서가 보장되지 않으므로 둘 다에서 갱신한다.
            RoomEvents.OnMasterClientSwitched += RefreshStartButton;

            leaveButton.onClick.AddListener(OnLeaveClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnDisable()
        {
            RoomEvents.OnLeftRoom -= OnLeftRoom;
            RoomEvents.OnPlayerEntered -= RefreshStartButton;
            RoomEvents.OnPlayerLeft -= RefreshStartButton;
            RoomEvents.OnMasterClientSwitched -= RefreshStartButton;

            leaveButton.onClick.RemoveListener(OnLeaveClicked);
            startButton.onClick.RemoveListener(OnStartClicked);
        }

        private void Start()
        {
            RefreshStartButton(null);
        }

        private void OnLeaveClicked()
        {
            NetworkManager.Instance.LeaveRoom();
        }

        private void OnStartClicked()
        {
            if (!CanStartGame()) return;
            NetworkManager.Instance.UpdateRoomOptions(isVisible: false, isOpen: false);
            // 방 전원이 함께 넘어가야 하므로 LoadScene 이 아닌 LoadLevel (ScenePaths 설명 참고).
            PhotonNetwork.LoadLevel(ScenePaths.Game);
        }

        // 08-room.md: 최소 인원 2명 (확정). 준비 상태 등 다른 조건은 미정.
        private bool CanStartGame() => PhotonNetwork.CurrentRoom.PlayerCount >= NetworkManager.MinPlayers;

        private void OnLeftRoom()
        {
            // 들어온 곳으로 돌아간다. Lobby 에서 왔으면 Lobby, DebugLobby 에서 왔으면 DebugLobby.
            SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);
        }

        // 시작 버튼은 방장에게만 보이고, 최소 인원이 찼을 때만 누를 수 있다.
        private void RefreshStartButton(Player _)
        {
            startButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
            startButton.interactable = CanStartGame();
        }
    }
}
