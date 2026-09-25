using Chapchu.Core;
using Chapchu.Network;
using Chapchu.UI.Popups;
using Photon.Realtime;
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

        // 요청을 보낸 뒤 결과가 올 때까지 띄우는 대기 표시. 성공하면 씬 전환이 닫고, 실패하면 여기서 닫는다.
        private LoadingPopup _loading;

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
            ShowLoading();
            NetworkManager.Instance.CreateRoom();
        }

        private void OnRandomMatchClicked()
        {
            ShowLoading();
            NetworkManager.Instance.JoinRandomRoom();
        }

        private void OnJoinRoomClicked()
        {
            InputPopup joinPopup = PopupManager.Instance.OpenModal<InputPopup>();
            joinPopup.PlaceholderText = "방 코드 입력";
            joinPopup.SubmitButtonText = "참가";
            joinPopup.CharacterLimit = RandomCode.Length;
            joinPopup.OnSubmitted += roomCode =>
            {
                joinPopup.Close();
                ShowLoading();
                NetworkManager.Instance.JoinRoom(roomCode);
            };
        }
    
        private void OnJoinedRoom()
        {
            SceneFlow.ReturnSceneAfterRoom = ScenePaths.Lobby;
            SceneManager.LoadScene(ScenePaths.Room);
        }

        private void OnRoomCreateFailed(short code, string message)
        {
            ShowError("방 생성 실패", GetFailureMessage(code, message));
        }

        private void OnJoinRoomFailed(short code, string message)
        {
            ShowError("방 참가 실패", GetFailureMessage(code, message));
        }

        private void OnJoinRandomFailed(short code, string message)
        {
            ShowError("랜덤 매칭 실패", GetFailureMessage(code, message));
        }

        // 08-room.md 7절. Photon 원문(message)은 우리 코드가 직접 만든 사유(InvalidOperation)일 때만 그대로 쓴다.
        private static string GetFailureMessage(short code, string message)
        {
            switch (code)
            {
                case ErrorCode.GameDoesNotExist: return "방을 찾을 수 없습니다.";
                case ErrorCode.GameFull: return "방이 가득 찼습니다.";
                case ErrorCode.GameClosed: return "이미 시작된 방입니다.";
                case ErrorCode.NoRandomMatchFound: return "참가할 수 있는 방이 없습니다.";
                case ErrorCode.InvalidOperation: return message;
                default: return "잠시 후 다시 시도해 주세요.";
            }
        }

        private void ShowLoading()
        {
            if (_loading != null) return;
            _loading = PopupManager.Instance.OpenModal<LoadingPopup>();
        }

        private void HideLoading()
        {
            if (_loading == null) return;
            _loading.Close();
            _loading = null;
        }

        private void ShowError(string title, string message)
        {
            HideLoading();
            WarningPopup popup = PopupManager.Instance.OpenModal<WarningPopup>();
            popup.MainText = title;
            popup.SubText = message;
        }
    }
}
