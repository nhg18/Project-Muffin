using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UI.Components;
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
        RoomEvents.OnJoinedRoom += OnRoomJoined;
        RoomEvents.OnCreateRoomFailed += OnRoomCreateFailed;
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
        
        // RoomEvents.OnRoomCreating -= 
        RoomEvents.OnJoinedRoom -= OnRoomJoined;
        RoomEvents.OnCreateRoomFailed -= OnRoomCreateFailed;
    }

    private void OnCreateRoomClicked()
    {
        PopupManager.Instance.Show<LoadingPopup>();
        NetworkManager.Instance.CreateRoom();
    }

    private void OnJoinRoomClicked()
    {
        PopupManager.Instance.Show<JoinRoomPopup>();
    }
    
    private void OnRoomJoined()
    {
        PopupManager.Instance.Hide();
    }

    private void OnRoomCreateFailed()
    {
        PopupManager.Instance.Hide();
    }
}
