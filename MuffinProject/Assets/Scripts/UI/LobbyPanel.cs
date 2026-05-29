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
        RoomEvents.OnJoinRoomFailed += OnJoinRoomFailed;
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
        
        // RoomEvents.OnRoomCreating -= 
        RoomEvents.OnJoinedRoom -= OnRoomJoined;
        RoomEvents.OnCreateRoomFailed -= OnRoomCreateFailed;
        RoomEvents.OnJoinRoomFailed -= OnJoinRoomFailed;
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
        Debug.Log("OnRoomJoined");
        PopupManager.Instance.HideAll(); // LoadingPopup, JoinRoomPopup 모두 제거
    }

    private void OnRoomCreateFailed(short code, string message)
    {
        PopupManager.Instance.Hide(); // LoadingPopup 제거
        ToastPopupManager.Instance.Show(message);
    }

    private void OnJoinRoomFailed(short code, string message)
    {
        PopupManager.Instance.Hide(); // LoadingPopup 제거
        ToastPopupManager.Instance.Show(message);
    }
}
