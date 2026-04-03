using DG.Tweening;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.XR;

public class GameRule : MonoBehaviourPunCallbacks
{
    #region fileds
    public static GameRule Instance { get; private set; }

    private int MyCardsCount = 0;

    [Header("Cards of this game")]
    [SerializeField] List<GameObject> Cards = new List<GameObject>();

    [SerializeField] List<GameObject> Hands = new List<GameObject>();

    [SerializeField] SplineContainer splineContainer;

    [SerializeField] Transform drawPosition;
    [SerializeField] Transform HandPosition;

    public bool isHandMod = false;


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


    #region DrawCards
    [PunRPC]
    void RPC_Hand_Out_Cards()
    {
        for (int i = 0; i < 7; i++)
        {
            draw_A_Card();
        }
    }

    public void draw_A_Card()
    {
        GameObject x = Instantiate(Cards[0],HandPosition);
        x.transform.position = drawPosition.position;
        Hands.Add(x);
        MyCardsCount++;
        PutAwayMyCards();
        RefreshMyInfo();
    }

    private void PutAwayMyCards()
    {
        if (Hands.Count == 0) return;
        float cardSpacing = 1f / 10f;
        float firstCardPosition = 0.5f - (Hands.Count - 1) * cardSpacing / 2;
        float duration = 1f;
        Spline spline = splineContainer.Spline;
        for (int i = 0; i < Hands.Count; i++)
        {
            float p = firstCardPosition + i * cardSpacing;
            Vector3 splinePosition = spline.EvaluatePosition(p);
            Vector3 forward = spline.EvaluateTangent(p);
            Vector3 up = spline.EvaluateUpVector(p);
            quaternion rotation = Quaternion.LookRotation(-up, Vector3.Cross(-up, forward).normalized);
            Hands[i].transform.DOMove(splinePosition + HandPosition.position + 0.01f * i * Vector3.back + new Vector3(0, 0, -i), duration);
            Hands[i].transform.DORotateQuaternion(rotation, duration);
        }
        return;

    }
    #endregion


    public void HandsUp()
    {
        Debug.Log("Up!");
        isHandMod = true;
        HandPosition.DOMove(new Vector3(0, -4.5f, 0),1f);
    }
    public void HandsDown()
    {
        Debug.Log("down!");
        isHandMod = false;
        HandPosition.DOMove(new Vector3(0, -6.5f, 0), 1f);
    }


    private void RefreshMyInfo()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                ["CardsCount"] = MyCardsCount
            }
            );
    }

}
