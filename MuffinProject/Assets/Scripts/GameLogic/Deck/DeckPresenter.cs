using System;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DeckPresenter : MonoBehaviourPunCallbacks
{
    [SerializeField] private Deck deck = new();
    [SerializeField] private DeckView deckView; // 인스펙터에서 할당
    [SerializeField] private CardDatabase cardDatabase;
    [SerializeField] public static int startHands = 7;
    [SerializeField] private DeckRecipe startingDeckRecipe;
    private const string DECK_PROPERTY_KEY = "RoomDeck";

    private void Awake()
    {
        // 1. 모델 생성
        //deck = new Deck();
    }

    private void Start()
    {
        // 2. 임시 카드로 덱 초기화 (실제 게임에서는 별도의 데이터 매니저에서 받아옴) 수정 필요!!
        List<Card> startingCards = new List<Card>(startingDeckRecipe.cardIDs);
        deck.InitDeck(startingCards);

        for(int i = 0; i < startHands; i++)
        {
            RequestDrawCard();
        }

        // 3. View의 버튼 클릭 이벤트 구독
        deckView.OnDrawButtonClicked += RequestDrawCard;
    }

    public void RequestDrawCard()
    {
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        if (PhotonNetwork.IsMasterClient)
        {
            ExecuteDrawAndSync(myActorNumber);
        }
        else
        {
            photonView.RPC(nameof(RPC_RequestDrawToMaster), RpcTarget.MasterClient, myActorNumber);
        }
    }

    [PunRPC]
    private void RPC_RequestDrawToMaster(int requesterActorNumber)
    {
        ExecuteDrawAndSync(requesterActorNumber);
    }

    private void ExecuteDrawAndSync(int requesterActorNumber)
    {
        if (deck.Count == 0)
        {
            Debug.LogWarning("뽑을 카드가 없음");
            return;
        }

        var drawnCard = deck.DrawTop();
        
        var hash = new ExitGames.Client.Photon.Hashtable
        {
            { DECK_PROPERTY_KEY, deck.GetCurrentDeck().Select(c => c.ID).ToArray() }
        };
        
        PhotonNetwork.CurrentRoom.SetCustomProperties(hash);
        photonView.RPC(nameof(RPC_BroadcastDrawnCard), RpcTarget.All, requesterActorNumber, drawnCard.ID);

    }

    [PunRPC]
    private void RPC_BroadcastDrawnCard(int actorNumber, int drawnCardID)
    {
        GameEvents.RaiseDrawn(actorNumber, drawnCardID);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(DECK_PROPERTY_KEY))
        {
            var idArray = (int[])propertiesThatChanged[DECK_PROPERTY_KEY];
            var deckArray = idArray.Select(id => new Card(id)).ToList();

            deck.SyncDeck(deckArray);
        }
    }

    //public void OnValidate()
    //{
    //    if(deck != null)
    //    {
    //        deck.AutoAssignIDs();
    //    }
    //}
}
