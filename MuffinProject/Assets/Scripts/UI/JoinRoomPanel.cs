using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinRoomPanel : MonoBehaviour
{
    [SerializeField] private Button joinButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_InputField codeInput;

    private void OnEnable()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
        backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDisable()
    {
        joinButton.onClick.RemoveListener(OnJoinClicked);
        backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void OnJoinClicked()
    {
        NetworkManager.Instance.JoinRoom(codeInput.text);
    }

    private void OnBackClicked()
    {
        gameObject.SetActive(false);
    }
}
