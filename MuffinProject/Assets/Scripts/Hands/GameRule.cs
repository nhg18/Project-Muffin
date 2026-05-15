using DG.Tweening;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.XR;

public class GameRule : SingletonPun<GameRule>
{
    #region fileds
    private PlayerHandsScripts phs;

    public int MyCardsCount = 0;
    
    [Header("OtherHands")]
    [SerializeField] GameObject OtherHands;
    [SerializeField] List<Transform> OtherHandsPosition = new List<Transform>();

    [Header("PlayerInfo")]
    [SerializeField] private int playerHp = 10;
    public bool isChapChu = false;

    public bool isHandMod = false;

    public int startHands = 7;


    const string KEY_TURN = "turn";
    #endregion

    #region UnityCallBacks

    private void Start()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int genCount = myActorNumber;
        phs = GetComponent<PlayerHandsScripts>();


        for(int i = 0; i < (playerCount-1); i++)
        {
            GameObject a = Instantiate(OtherHands, OtherHandsPosition[i]);
            OtherPlayerHands oph = a.GetComponent<OtherPlayerHands>();
            oph.PlayerNumber = (genCount % playerCount + 1);
            genCount++;
        }
        StartFirstTurn();
    }
    #endregion

    #region TurnSystem
    #region turnProperties
    public bool IsMyTurn
    {
        get
        {
            if (!PhotonNetwork.InRoom) return false;
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (!props.ContainsKey(KEY_TURN)) return false;
            return (int)props[KEY_TURN] == PhotonNetwork.LocalPlayer.ActorNumber;
        }
    }

    public int CurrentTurnActor
    {
        get
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            return props.ContainsKey(KEY_TURN) ? (int)props[KEY_TURN] : -1;
        }
    }
    #endregion

    #region turnMethods
    public void StartFirstTurn()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int firstActor = PhotonNetwork.PlayerList[0].ActorNumber;
        Debug.Log(firstActor);
        SetTurn(firstActor);

        photonView.RPC(nameof(RPC_Hand_Out_Cards), RpcTarget.All);
    }

    public int EndTurn()
    {
        if (!IsMyTurn)
        {
            Debug.LogWarning("not your turn");
            return -1;
        }

        photonView.RPC(nameof(RPC_RequestNextTurn),
            RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber);
        return 1;
    }

    [PunRPC]
    void RPC_RequestNextTurn(int requesterActor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (requesterActor != CurrentTurnActor)
        {
            Debug.LogWarning("Requester is don't have turn");
            return;
        }

        int nextActor = GetNextActor(requesterActor);
        SetTurn(nextActor);
    }

    private int GetNextActor(int requesterActor)
    {
        var players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
            if (players[i].ActorNumber == CurrentTurnActor)
                return players[(i + 1) % players.Length].ActorNumber;
        return players[0].ActorNumber;
    }

    void SetTurn(int actorNumber)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { [KEY_TURN] = actorNumber }
        );
    }
    #endregion

    #region PunCallBacks_about_turn
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProperties)
    {
        if (!changedProperties.ContainsKey(KEY_TURN)) return;

        int actorNumber = (int)changedProperties[KEY_TURN];
        Debug.Log($"turnchange : {actorNumber}/ MyTurn?:{IsMyTurn}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (otherPlayer.ActorNumber == CurrentTurnActor)
        {
            Debug.Log("[GameRule] TurnPlayer out, Turn is gived to nextPlayer");
            SetTurn(GetNextActor(otherPlayer.ActorNumber));
        }
    }
    #endregion
    #endregion


    #region Hand_Out
    [PunRPC]
    void RPC_Hand_Out_Cards()
    {
        for (int i = 0; i < startHands; i++)
        {
            phs.draw_A_Card();
        }
    }
    
    #endregion





    public void RefreshMyInfo()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                ["CardsCount"] = MyCardsCount,
                ["PlayerHP"] = playerHp,
                ["isChapChu"] = isChapChu
            }
        );
    }

}
