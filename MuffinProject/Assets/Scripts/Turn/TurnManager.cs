using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class TurnManager : SingletonPun<TurnManager>
{
    public const string KEY_TURN = "turn";
    
    public int CurrentTurnActor {
        get
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            return props.ContainsKey(KEY_TURN) ? (int)props[KEY_TURN] : -1;
        }
    }
    
    public bool IsMyTurn {
        get
        {
            if (!PhotonNetwork.InRoom) return false;
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (!props.ContainsKey(KEY_TURN)) return false;
            
            return (int)props[KEY_TURN] == PhotonNetwork.LocalPlayer.ActorNumber;
        }
    }

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            StartFirstTurn();
        }
    }
    
    private void SetTurn(int actorNumber)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { [KEY_TURN] = actorNumber }
        );
    }


    #region business
    private void StartFirstTurn()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int firstActor = PhotonNetwork.PlayerList[0].ActorNumber;
        SetTurn(firstActor);

        //-----photonView.RPC(nameof(RPC_Hand_Out_Cards), RpcTarget.All);
    }

    private int GetNextActor(int currentActor)
    {
        var players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
            if (players[i].ActorNumber == CurrentTurnActor)
                return players[(i + 1) % players.Length].ActorNumber;
        return players[0].ActorNumber;
    }
    
    public void RequestEndTurn()
    {
        if (!IsMyTurn)
        {
            Debug.LogWarning("not your turn");
            return;
        }

        photonView.RPC(nameof(RPC_RequestNextTurn),
            RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber);
    }
    #endregion

    #region Pun RPC & CallBacks
    [PunRPC]
    private void RPC_RequestNextTurn(int requesterActor)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (requesterActor != CurrentTurnActor)
        {
            Debug.LogWarning("Requester don't have turn");
            return;
        }
        int nextActor = GetNextActor(requesterActor);
        SetTurn(nextActor);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProperties)
    {
        if (!changedProperties.ContainsKey(KEY_TURN)) return;

        int actorNumber = (int)changedProperties[KEY_TURN];
        Debug.Log($"turnchange : {actorNumber}/ MyTurn?:{IsMyTurn}");
        
        TurnEvents.RaiseTurnChanged();
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
}