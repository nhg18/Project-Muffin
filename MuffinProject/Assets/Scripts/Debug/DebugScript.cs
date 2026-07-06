using System;
using System.Collections;
using System.Collections.Generic;
using Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class DebugScript : MonoBehaviour
{
    [SerializeField] private Button joinButton;
    [SerializeField] private string nickname = "Player";    
    [SerializeField] private string roomName = "Debug";
    
    private void Start()
    {
        if (!NetworkManager.IsConnected)
            NetworkManager.Instance.Connect();
        
        NetworkManager.Instance.SetNickname(nickname);
        
        joinButton.onClick.AddListener(ClickJoinButton);
        RoomEvents.OnJoinRoomFailed += HandleJoinRoomFailed;
    }

    private void ClickJoinButton()
    {
        if (!NetworkManager.IsConnected) Debug.LogError("JoinRoom failed. Client is not connected.");
        NetworkManager.Instance.JoinRoom(roomName);
    }

    private void HandleJoinRoomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.GameDoesNotExist)
        {
            PhotonNetwork.CreateRoom(
                roomName, 
                NetworkManager.Instance.CreateRoomOptions(4, false, true));
        }
        else
        {
            Debug.LogError(message);
        }
    }
}
