using System;
using System.Collections;
using System.Collections.Generic;
using Network;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPanel : MonoBehaviour
{
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button startButton;

    private void OnEnable()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            startButton.gameObject.SetActive(true);
            startButton.onClick.AddListener(OnStartClicked);
        }
        else
        {
            startButton.gameObject.SetActive(false);
        }
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    private void OnDisable()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }
        leaveButton.onClick.RemoveListener(OnLeaveClicked);
    }

    private void OnLeaveClicked()
    {
        NetworkManager.Instance.LeaveRoom();
        // SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
        SceneManager.LoadScene(ScenePaths.Get(SceneType.DebugLobby)); // 디버깅 로비 씬
    }

    private void OnStartClicked()
    {
        if (!CanStartGame()) return;
        NetworkManager.Instance.UpdateRoomOptions(isVisible: false, isOpen: false);
        PhotonNetwork.LoadLevel("RefactorGameScene");//스크립트 수정 원본->PhotonNetwork.LoadLevel(ScenePaths.Get(SceneType.Game));
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
