using System;
using System.Collections;
using System.Collections.Generic;
using Network;
using TMPro;
using UI.Components;
using UI.Popup;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyPanel : MonoBehaviour
{
    [SerializeField] private Button randomMatchButton;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;

    private void OnEnable()
    {
        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        
        // RoomEvents.OnRoomCreating += 
        RoomEvents.OnJoinedRoom += OnJoinedRoom;
        RoomEvents.OnCreateRoomFailed += OnRoomCreateFailed;
        RoomEvents.OnJoinRoomFailed += OnJoinRoomFailed;
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
        
        // RoomEvents.OnRoomCreating -= 
        RoomEvents.OnJoinedRoom -= OnJoinedRoom;
        RoomEvents.OnCreateRoomFailed -= OnRoomCreateFailed;
        RoomEvents.OnJoinRoomFailed -= OnJoinRoomFailed;
    }

    private void OnCreateRoomClicked()
    {
        NetworkManager.Instance.CreateRoom();
    }

    private void OnJoinRoomClicked()
    {
        var joinPopup = PopupManager.Instance.OpenModal(PopupManager.Get<InputPopup>());
        joinPopup.PlaceholderText = "방 코드 입력";
        joinPopup.SubmitButtonText = "참가";
        joinPopup.CharacterLimit = 4;

        joinPopup.OnClickedSubmitButton = () =>
        {
            Debug.Log("Clicked on the join room");
            NetworkManager.Instance.JoinRoom(joinPopup.InputText);
        };

        joinPopup.OnClickedExitButton = () =>
        {
            Debug.Log("Clicked on exit");
            PopupManager.Instance.CloseModal(joinPopup);
        };
    }
    
    private void OnJoinedRoom()
    {
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Room));
    }

    private void OnRoomCreateFailed(short code, string message)
    {
        
    }

    private void OnJoinRoomFailed(short code, string message)
    {
        
    }
}
