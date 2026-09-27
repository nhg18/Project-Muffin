using Chapchu.Core;
using Chapchu.Network;
using Chapchu.UI.Popups;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 뷰와 네트워크 · 팝업 · 씬 전환을 잇는다. Scripts/UI/Lobby 에서 네트워크를 아는 유일한 파일이다 (TitlePresenter 와 같은 구조).
    /// 규칙: 방 생성 · 참가는 NetworkManager 에 요청만 하고, 결과는 RoomEvents 로 받는다.
    /// 기준 문서: docs/systems/14-lobby-ui.md 5 · 6절, docs/systems/08-room.md 4 · 7절
    /// </summary>
    [RequireComponent(typeof(LobbyView))]
    public class LobbyPresenter : MonoBehaviour
    {
        private LobbyView _view;

        // 요청을 보낸 뒤 결과가 올 때까지 띄우는 대기 표시. 성공하면 씬 전환이 닫고, 실패하면 여기서 닫는다.
        private LoadingPopup _loading;

        private void Awake()
        {
            _view = GetComponent<LobbyView>();
        }

        private void OnEnable()
        {
            _view.RandomMatchRequested += HandleRandomMatchRequested;
            _view.CreateRoomRequested += HandleCreateRoomRequested;
            _view.JoinRoomRequested += HandleJoinRoomRequested;

            RoomEvents.OnJoinedRoom += HandleJoinedRoom;
            RoomEvents.OnCreateRoomFailed += HandleCreateRoomFailed;
            RoomEvents.OnJoinRoomFailed += HandleJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed += HandleJoinRandomFailed;
        }

        private void OnDisable()
        {
            _view.RandomMatchRequested -= HandleRandomMatchRequested;
            _view.CreateRoomRequested -= HandleCreateRoomRequested;
            _view.JoinRoomRequested -= HandleJoinRoomRequested;

            RoomEvents.OnJoinedRoom -= HandleJoinedRoom;
            RoomEvents.OnCreateRoomFailed -= HandleCreateRoomFailed;
            RoomEvents.OnJoinRoomFailed -= HandleJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed -= HandleJoinRandomFailed;
        }

        private void Start()
        {
            _view.SetNickname(NetworkManager.Nickname);
        }

        private void HandleRandomMatchRequested()
        {
            ShowLoading();
            NetworkManager.Instance.JoinRandomRoom();
        }

        private void HandleCreateRoomRequested()
        {
            ShowLoading();
            NetworkManager.Instance.CreateRoom();
        }

        private void HandleJoinRoomRequested()
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

        private void HandleJoinedRoom()
        {
            SceneFlow.ReturnSceneAfterRoom = ScenePaths.Lobby;
            SceneFlow.GameSceneAfterRoom = ScenePaths.Game;
            SceneManager.LoadScene(ScenePaths.Room);
        }

        private void HandleCreateRoomFailed(short code, string message)
        {
            ShowError("방 생성 실패", GetFailureMessage(code, message));
        }

        private void HandleJoinRoomFailed(short code, string message)
        {
            ShowError("방 참가 실패", GetFailureMessage(code, message));
        }

        private void HandleJoinRandomFailed(short code, string message)
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
