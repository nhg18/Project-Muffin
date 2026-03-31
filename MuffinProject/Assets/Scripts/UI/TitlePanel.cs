using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmButton;
    
    [SerializeField] private GameObject joinRoomPanel;

    private void Start()
    {
        UpdateNicknameUI();
    }

    private void OnEnable()
    {
        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
        confirmButton.onClick.RemoveListener(OnConfirmClicked);
    }

    private void OnCreateRoomClicked()
    {
        NetworkManager.Instance.CreateRoom();
    }

    private void OnJoinRoomClicked()
    {
        joinRoomPanel.SetActive(true);
    }

    private void OnConfirmClicked()
    {
        string nickname = nicknameInput.text;
        NetworkManager.Instance.SetNickname(nickname);
        Debug.Log($"Set Your Nickname : {NetworkManager.Nickname}");
    }
    
    private void UpdateNicknameUI()
    {
        string defaultName = String.Empty;

        if (nicknameInput != null)
        {
            if (PlayerPrefs.HasKey(PlayerPrefsKeys.playerName))
            {
                defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);
                nicknameInput.text = defaultName;
            }
        }
    }
}
