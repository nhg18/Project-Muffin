using System;
using System.Collections;
using System.Collections.Generic;
using Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
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
    }

    private void ClickJoinButton()
    {
        if (!NetworkManager.IsConnected)
        {
            Debug.LogError("JoinRoom failed. Client is not connected.");
            return;
        }

        var roomOptions = NetworkManager.Instance.CreateRoomOptions(4, true, true);
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }
}
