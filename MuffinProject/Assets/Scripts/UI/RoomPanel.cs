using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class RoomPanel : MonoBehaviour
{
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button startButton;

    private void OnEnable()
    {
        leaveButton.onClick.AddListener(OnLeaveClicked);
        startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnDisable()
    {
        leaveButton.onClick.RemoveListener(OnLeaveClicked);
        startButton.onClick.RemoveListener(OnStartClicked);
    }

    private void OnLeaveClicked()
    {
        NetworkManager.Instance.LeaveRoom();
    }

    private void OnStartClicked()
    {
        if (!CanStartGame()) return;
        NetworkManager.Instance.UpdateRoomOptions(isVisible: false, isOpen: false);
        PhotonNetwork.LoadLevel("Scenes/GameScene");
    }

    private bool CanStartGame()
    {
        // Master Client만 시작 가능, UI에서는 시작버튼이 안 보이게 설정
        if (!PhotonNetwork.IsMasterClient) return false;
        
        // CurrentRoom null check
        if (PhotonNetwork.CurrentRoom == null) return false;

        if (PhotonNetwork.CurrentRoom.PlayerCount < NetworkManager.MinPlayers)
        {
            Debug.Log("It must have at least 2 players");
            // 플레이어 2명 이상 경고문 UI 처리
            return false;
        }
        return true;
    }
}
