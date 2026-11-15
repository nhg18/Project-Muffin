using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Chapchu.Core;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 디버그 로비 씬의 입장 버튼. 정해진 이름의 방에 바로 들어간다 — 멀티 테스트용이라 로비 · 방 코드 흐름(08-room.md)을 거치지 않는다.
    /// </summary>
    public class DebugScript : MonoBehaviour
    {
        [SerializeField] private Button joinButton;
        [Tooltip("입장할 때 쓸 닉네임. 뒤에 #번호가 붙는다")]
        [SerializeField] private string nickname = "Player";
        [Tooltip("들어갈 방 이름. 클라이언트끼리 같아야 한 방에 모인다")]
        [SerializeField] private string roomName = "Debug";

        private void Awake()
        {
            if (joinButton == null)
                Debug.LogError($"[{nameof(DebugScript)}] {nameof(joinButton)} 참조가 비어 있습니다.", this);
        }

        private void Start()
        {
            // 돌아갈 씬 · 시작할 게임 씬은 로비에 들어온 순간 정한다.
            SceneFlow.ReturnSceneAfterRoom = ScenePaths.DebugLobby;
            SceneFlow.GameSceneAfterRoom = ScenePaths.Game;

            if (!NetworkManager.IsConnected)
                NetworkManager.Instance.Connect();
        
            NetworkManager.Instance.SetNickname(nickname);
        
            joinButton.onClick.AddListener(ClickJoinButton);
        }

        private void OnEnable()
        {
            RoomEvents.OnJoinedRoom += HandleJoinedRoom;
        }

        private void OnDisable()
        {
            RoomEvents.OnJoinedRoom -= HandleJoinedRoom;
        }
    
        private void ClickJoinButton()
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.LogError($"[{nameof(DebugScript)}] 서버에 연결되지 않아 방에 들어갈 수 없다");
                return;
            }
        

            // 비공개 — 실제 로비의 랜덤 매칭이 이 방에 섞여 들어오지 않게. 이름으로 들어가는 JoinOrCreateRoom 은 비공개 방에도 들어간다.
            RoomOptions roomOptions = NetworkManager.Instance.CreateRoomOptions(NetworkManager.MaxPlayers, false, true);
            PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
        }

        private void HandleJoinedRoom()
        {
            int actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
            NetworkManager.Instance.SetNickname(NetworkManager.Nickname + $"#{actorNum}");

            // 방 씬은 각자 연다 — PUN 씬 동기화는 쓰지 않는다 (ScenePaths 설명 참고)
            SceneManager.LoadScene(ScenePaths.Room);
        }
    }
}
