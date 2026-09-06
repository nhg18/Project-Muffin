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
        PhotonNetwork.AutomaticallySyncScene = false;
        
        if (!NetworkManager.IsConnected)
            NetworkManager.Instance.Connect();
        
        NetworkManager.Instance.SetNickname(nickname);
        
        joinButton.onClick.AddListener(ClickJoinButton);
    }

    private void OnEnable()
    {
        RoomEvents.OnJoinedRoom += OnJoinedRoom;
    }

    private void OnDisable()
    {
        RoomEvents.OnJoinedRoom -= OnJoinedRoom;
    }
    
    private void ClickJoinButton()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogError("JoinRoom failed. Client is not connected.");
            return;
        }
        
        Debug.Log("Click join button");

        var roomOptions = NetworkManager.Instance.CreateRoomOptions(4, true, true);
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    private void OnJoinedRoom()
    {
        Debug.Log("JoinRoom");
        int actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
        NetworkManager.Instance.SetNickname(NetworkManager.Nickname + $"#{actorNum}");
        
        Debug.Log("OnJoinedRoom " + NetworkManager.Nickname);
        
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Room));
    }
}
