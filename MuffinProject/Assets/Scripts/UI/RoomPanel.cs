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

namespace Chapchu.UI
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
            // 방 전원이 함께 넘어가야 하므로 LoadScene 이 아닌 LoadLevel (ScenePaths 설명 참고).
            PhotonNetwork.LoadLevel(ScenePaths.Game);
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
            // 정식 흐름은 LobbyScene (B5-2). 지금은 개발용 디버그 로비로 돌아간다.
            SceneManager.LoadScene(ScenePaths.DebugLobby);
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
