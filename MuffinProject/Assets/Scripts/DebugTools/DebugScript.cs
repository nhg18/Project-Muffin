using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Chapchu.Core;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Chapchu.DebugTools
{


    public class DebugScript : MonoBehaviour
    {
        [SerializeField] private Button joinButton;
        [SerializeField] private string nickname = "Player";    
        [SerializeField] private string roomName = "Debug";
        [Tooltip("켜면 게임 시작 시 GameScene 대신 TmpGameScene 으로 넘어간다 (새 GameServer 멀티 테스트). 끄면 예전처럼 GameScene.")]
        [SerializeField] private bool startInTmpGame = true;

        private void Start()
        {
            // AutomaticallySyncScene 은 NetworkManager.Awake(PhotonConnection.Initialize) 가 true 로 켠다.
            // 여기서 false 로 덮어쓰면 방장의 LoadLevel 이 참가자에게 동기화되지 않아 마스터만 인게임으로 넘어간다.
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

            SceneFlow.ReturnSceneAfterRoom = ScenePaths.DebugLobby;
            SceneFlow.GameSceneAfterRoom = startInTmpGame ? ScenePaths.TmpGame : ScenePaths.Game;
            SceneManager.LoadScene(ScenePaths.Room);
        }
    }
}
