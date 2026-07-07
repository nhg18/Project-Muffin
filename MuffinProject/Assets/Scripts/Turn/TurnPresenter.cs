using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class TurnPresenter : MonoBehaviourPunCallbacks
{
    private TurnModel turnModel;
    [SerializeField] private TurnView turnView;

    private void Awake()
    {
        turnModel = new TurnModel();
    }

    private void Start()
    {
        turnView.OnEndTurnRequested += HandleEndTurnRequest;
        if (PhotonNetwork.IsMasterClient)
        {
            StartFirstTurn();
        }
    }

    private void OnDestroy()
    {
        if(turnView != null)
        {
            turnView.OnEndTurnRequested -= HandleEndTurnRequest;
        }
    }

    #region business
    public void StartFirstTurn()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int firstActor = PhotonNetwork.PlayerList[0].ActorNumber;
        turnModel.SetTurn(firstActor);

        //-----photonView.RPC(nameof(RPC_Hand_Out_Cards), RpcTarget.All);
    }

    private void HandleEndTurnRequest()
    {
        if (!turnModel.IsMyTurn)
        {
            Debug.LogWarning("not your turn");
            return;
        }

        photonView.RPC(nameof(RPC_RequestNextTurn),
            RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber);
    }
    private int GetNextActor(int currentActor)
    {
        var players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
            if (players[i].ActorNumber == turnModel.CurrentTurnActor)
                return players[(i + 1) % players.Length].ActorNumber;
        return players[0].ActorNumber;
    }
    #endregion

    #region Pun RPC & CallBacks
    [PunRPC]
    private void RPC_RequestNextTurn(int requesterActor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (requesterActor != turnModel.CurrentTurnActor)
        {
            Debug.LogWarning("Requester don't have turn");
            return;
        }
        int nextActor = GetNextActor(requesterActor);
        turnModel.SetTurn(nextActor);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProperties)
    {
        if (!changedProperties.ContainsKey(TurnModel.KEY_TURN)) return;

        int actorNumber = (int)changedProperties[TurnModel.KEY_TURN];
        Debug.Log($"turnchange : {actorNumber}/ MyTurn?:{turnModel.IsMyTurn}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (otherPlayer.ActorNumber == turnModel.CurrentTurnActor)
        {
            Debug.Log("[GameRule] TurnPlayer out, Turn is gived to nextPlayer");
            turnModel.SetTurn(GetNextActor(otherPlayer.ActorNumber));
        }
    }
    #endregion
}

