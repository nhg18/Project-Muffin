using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PrototypeNetwork : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField NickNameInput;
    [SerializeField] private GameObject ConnectPanel;
    [SerializeField] private TMP_Text ConnectedUserText;

    private string playerList = "";

    private void Awake()
    {
        Screen.SetResolution(960, 540, false);
        PhotonNetwork.SendRate = 60;
        PhotonNetwork.SerializationRate = 30;
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void Connect() => PhotonNetwork.ConnectUsingSettings();

    public void GameStart()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if(PhotonNetwork.CurrentRoom.PlayerCount >= 1)
        {
            PhotonNetwork.LoadLevel("MainGame");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
    }
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.LocalPlayer.NickName = NickNameInput.text;
        PhotonNetwork.JoinOrCreateRoom("Room", new RoomOptions { MaxPlayers = 6 }, null);
    }
    public override void OnJoinedRoom()
    {
        Debug.Log("what");
        RefreshPlayerList();
        //ConnectPanel.SetActive(false);
    }

    void RefreshPlayerList()
    {
        string fullText = "";
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            string nickname = players[i].NickName;
            //bool isMe = players[i].IsLocal;
            //bool isMaster = players[i].IsMasterClient;

            // 닉네임이 비어있으면 기본값
            if (string.IsNullOrEmpty(nickname))
                nickname = $"Player {players[i].ActorNumber}";

            string label = nickname;
            //if (isMe) label += " (me)";
            //if (isMaster) label += " (King)";
            fullText += " " + label;
        }
        ConnectedUserText.text = "Users : " + fullText;
    }
}
