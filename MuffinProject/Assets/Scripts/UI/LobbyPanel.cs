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
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
    }

    private void OnCreateRoomClicked()
    {
        NetworkManager.Instance.CreateRoom();
    }

    private void OnJoinRoomClicked()
    {
        PopupManager.Instance.Show<JoinRoomPopup>();
    }
}
