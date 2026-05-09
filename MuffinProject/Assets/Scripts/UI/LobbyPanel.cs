using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyPanel : MonoBehaviour
{
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    
    [SerializeField] private GameObject joinRoomPanel;

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
        joinRoomPanel.SetActive(true);
    }
}
