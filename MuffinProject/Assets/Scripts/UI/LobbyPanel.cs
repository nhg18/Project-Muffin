using System;
using System.Collections;
using System.Collections.Generic;
using Network;
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
        NetworkManager.Instance.CreateRoom();
    }

    private void OnJoinRoomClicked()
    {
    }
    
    private void OnRoomJoined()
    {
        Debug.Log("OnRoomJoined");
        
    }

    private void OnRoomCreateFailed(short code, string message)
    {
        
    }

    private void OnJoinRoomFailed(short code, string message)
    {
        
    }
}
