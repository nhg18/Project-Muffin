using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Network;
using TMPro;
using Chapchu.UI.Components;
using Chapchu.UI.Popup;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Chapchu.Core;

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
            var joinPopup = PopupManager.Instance.OpenModal(PopupManager.Get<InputPopup>());
            joinPopup.PlaceholderText = "방 코드 입력";
            joinPopup.SubmitButtonText = "참가";
            joinPopup.CharacterLimit = 4;

            joinPopup.OnClickedSubmitButton = () =>
            {
                NetworkManager.Instance.JoinRoom(joinPopup.InputText);
            };

            joinPopup.OnClickedExitButton = () =>
            {
                PopupManager.Instance.CloseModal(joinPopup);
            };
        }
    
        private void OnJoinedRoom()
        {
            SceneManager.LoadScene(ScenePaths.Get(SceneType.Room));
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
            var popup = PopupManager.Instance.OpenModal(PopupManager.Get<WarningPopup>());
            popup.MainText = title;
            popup.SubText = message;
            popup.OnClickedOkButton = () => PopupManager.Instance.CloseModal(popup);
        }
    }
}
