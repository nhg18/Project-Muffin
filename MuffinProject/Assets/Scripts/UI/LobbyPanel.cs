using Chapchu.Core;
using Chapchu.Network;
using Chapchu.UI.Popups;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Chapchu.UI
{

    public class LobbyPanel : MonoBehaviour
    {
        [SerializeField] private Button randomMatchButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;

        private void OnEnable()
        {
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            randomMatchButton.onClick.AddListener(OnRandomMatchClicked);

            RoomEvents.OnJoinedRoom += OnJoinedRoom;
            RoomEvents.OnCreateRoomFailed += OnRoomCreateFailed;
            RoomEvents.OnJoinRoomFailed += OnJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed += OnJoinRandomFailed;
        }

        private void OnDisable()
        {
            createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
            joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
            randomMatchButton.onClick.RemoveListener(OnRandomMatchClicked);

            RoomEvents.OnJoinedRoom -= OnJoinedRoom;
            RoomEvents.OnCreateRoomFailed -= OnRoomCreateFailed;
            RoomEvents.OnJoinRoomFailed -= OnJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed -= OnJoinRandomFailed;
        }

        private void OnCreateRoomClicked()
        {
            NetworkManager.Instance.CreateRoom();
        }

        private void OnRandomMatchClicked()
        {
            NetworkManager.Instance.JoinRandomRoom();
        }

        private void OnJoinRoomClicked()
        {
            InputPopup joinPopup = PopupManager.Instance.OpenModal<InputPopup>();
            joinPopup.PlaceholderText = "방 코드 입력";
            joinPopup.SubmitButtonText = "참가";
            joinPopup.CharacterLimit = 4;
            joinPopup.OnSubmitted += roomCode => NetworkManager.Instance.JoinRoom(roomCode);
        }
    
        private void OnJoinedRoom()
        {
            SceneManager.LoadScene(ScenePaths.Room);
        }

        private void OnRoomCreateFailed(short code, string message)
        {
            ShowError("방 생성 실패", message);
        }

        private void OnJoinRoomFailed(short code, string message)
        {
            ShowError("방 참가 실패", message);
        }

        private void OnJoinRandomFailed(short code, string message)
        {
            ShowError("빠른 참가 실패", "참가할 수 있는 방이 없습니다.");
        }

        private void ShowError(string title, string message)
        {
            WarningPopup popup = PopupManager.Instance.OpenModal<WarningPopup>();
            popup.MainText = title;
            popup.SubText = message;
        }
    }
}
