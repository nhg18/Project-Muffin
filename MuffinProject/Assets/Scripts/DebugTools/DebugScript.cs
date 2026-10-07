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
        [Tooltip("켜면 게임 시작 시 GameScene 대신 TempGameScene 으로 넘어간다 (새 GameServer 멀티 테스트). 끄면 예전처럼 GameScene.")]
        [SerializeField] private bool startInTmpGame = true;

        private void Start()
        {
            // 돌아갈 씬 · 시작할 게임 씬은 로비에 들어온 순간 정한다. 입장 콜백(OnJoinedRoom)에서 정하면 안 된다 —
            // 이미 있는 방에 들어가면 PUN 씬 동기화(AutomaticallySyncScene)가 콜백보다 먼저 RoomScene 을 불러
            // 이 오브젝트가 사라지고, 값이 기본값(Lobby)으로 남는다.
            SceneFlow.ReturnSceneAfterRoom = ScenePaths.DebugLobby;
            SceneFlow.GameSceneAfterRoom = startInTmpGame ? ScenePaths.TmpGame : ScenePaths.Game;

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
        

            var roomOptions = NetworkManager.Instance.CreateRoomOptions(4, true, true);
            PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
        }

        private void OnJoinedRoom()
        {
            int actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
            NetworkManager.Instance.SetNickname(NetworkManager.Nickname + $"#{actorNum}");

            // 방을 새로 만든 사람만 여기서 넘어간다. 이미 있는 방에 들어간 사람은 PUN 씬 동기화가 먼저 넘긴다.
            SceneManager.LoadScene(ScenePaths.Room);
        }
    }
}
