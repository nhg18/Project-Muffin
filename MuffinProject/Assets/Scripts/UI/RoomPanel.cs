using System;
using System.Collections;
using System.Collections.Generic;
using Muffin.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Muffin.Core;

namespace Muffin.UI
{

    public class RoomPanel : MonoBehaviour
    {
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button startButton;

        private void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
        }

        private void OnEnable()
        {
            RoomEvents.OnLeftRoom += OnLeftRoom;
        
            RoomEvents.OnPlayerEntered += OnRoomStateChanged;
            RoomEvents.OnPlayerLeft += OnRoomStateChanged;

            RoomEvents.OnPlayerEntered += UpdateStartButtonState;
            RoomEvents.OnPlayerLeft += UpdateStartButtonState;
        
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        private void OnDisable()
        {
            RoomEvents.OnLeftRoom -= OnLeftRoom;
        
            RoomEvents.OnPlayerEntered -= OnRoomStateChanged;
            RoomEvents.OnPlayerLeft -= OnRoomStateChanged;
        
            RoomEvents.OnPlayerEntered -= UpdateStartButtonState;
            RoomEvents.OnPlayerLeft -= UpdateStartButtonState;
        
            leaveButton.onClick.RemoveListener(OnLeaveClicked);
        }

        private void Start()
        {
            UpdateStartButtonState(null);
            RefreshStartButton();
        }

        private void OnLeaveClicked()
        {
            NetworkManager.Instance.LeaveRoom();
        }

        private void OnStartClicked()
        {
            if (!CanStartGame()) return;
            NetworkManager.Instance.UpdateRoomOptions(isVisible: false, isOpen: false);
            // PUN 씬 동기화는 경로가 아닌 씬 이름으로 비교한다. Get() 을 쓰면 클라이언트가 무한 재로드된다.
            PhotonNetwork.LoadLevel(ScenePaths.GetName(SceneType.Game));
        }

        private bool CanStartGame()
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount < NetworkManager.MinPlayers)
            {
                Debug.Log("It must have at least 2 players");
                // 플레이어 2명 이상 경고문 UI 처리
                return false;
            }
            return true;
        }

        private void OnLeftRoom()
        {
            // SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
            SceneManager.LoadScene(ScenePaths.Get(SceneType.DebugLobby)); // 디버깅 로비 씬
        }
    
        private void UpdateStartButtonState(Player player)
        {
            startButton.interactable = CanStartGame();
        }
    
        private void OnRoomStateChanged(Player player)
        {
            RefreshStartButton();
        }
    
        private void RefreshStartButton()
        {
            // 마스터 클라이언트인지 확인
            if (PhotonNetwork.IsMasterClient)
            {
                startButton.gameObject.SetActive(true);
            
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnStartClicked);
            }
            else
            {
                startButton.gameObject.SetActive(false);
                startButton.onClick.RemoveAllListeners();
            }
        }
    }
}
