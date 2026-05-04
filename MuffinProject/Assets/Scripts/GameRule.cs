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

    [Header("Hands of Player")]
    [SerializeField] List<GameObject> Hands = new List<GameObject>();


    [Header("GameObjects")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] Transform drawPosition;
    [SerializeField] Transform HandPosition;

    [Header("OtherHands")]
    [SerializeField] GameObject OtherHands;
    [SerializeField] List<Transform> OtherHandsPosition = new List<Transform>();

    public bool isHandMod = false;

    public int startHands = 7;


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
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int genCount = myActorNumber;



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


    #region DrawCards
    [PunRPC]
    void RPC_Hand_Out_Cards()
    {
        for (int i = 0; i < startHands; i++)
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

    public void PutAwayMyCards()
    {
        float cardSpacing;
        if (Hands.Count == 0) return;
        else if(Hands.Count > 10)
        {
            cardSpacing = 1f/ (Hands.Count + 1f);
        }
        else
        {
            cardSpacing = 1f / 10f;
        }
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

    #region ThrowCards

    public void destoryCards(int number)
    {
        Destroy(Hands[number]);
        Hands.RemoveAt(number);
        MyCardsCount--;
        PutAwayMyCards();
        RefreshMyInfo();
    }

    #endregion

    #region HandFunc
    public void HandsUp()
    {
        Debug.Log("Up!");
        isHandMod = true;
        HandPosition.DOMove(new Vector3(0, -3.8f, 0), 1f);
    }
    public void HandsDown()
    {
        Debug.Log("down!");
        isHandMod = false;
        HandPosition.DOMove(new Vector3(0, -6.5f, 0), 1f);
        //PutAwayMyCards();
    }
    #endregion



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
