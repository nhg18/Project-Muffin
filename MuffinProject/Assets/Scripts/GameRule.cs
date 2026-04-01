using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameRule : MonoBehaviourPunCallbacks
{
    #region fileds
    public static GameRule Instance { get; private set; }


    const string KEY_TURN = "turn";
    #endregion

    #region UnityCallBacks
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
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



}
