using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckPresenter : MonoBehaviourPunCallbacks
{
    private DeckModel deckModel;
    [SerializeField] private DeckView deckView; // 인스펙터에서 할당
    private const string DECK_PROPERTY_KEY = "RoomDeck";

    private void Awake()
    {
        // 1. 모델 생성
        deckModel = new DeckModel();

        // 2. 임시 카드로 덱 초기화 (실제 게임에서는 별도의 데이터 매니저에서 받아옴) 수정 필요!!
        List<int> startingCards = new List<int>
        {
            1,2,3
        };
        deckModel.InitDeck(startingCards);

        // 3. View의 버튼 클릭 이벤트 구독
        if (deckView != null)
        {
            deckView.OnDrawButtonClicked += RequestDrawCard;
        }
    }

    public void RequestDrawCard()
    {
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        if (PhotonNetwork.IsMasterClient)
        {
            ExcuteDrawAndSync(myActorNumber);
        }
        else
        {
            photonView.RPC("RPC_RequestDrawToMaster", RpcTarget.MasterClient, myActorNumber);
        }
    }

    [PunRPC]
    private void RPC_RequestDrawToMaster(int requesterActorNumber)
    {
        ExcuteDrawAndSync(requesterActorNumber);
    }

    private void ExcuteDrawAndSync(int requesterActorNumber)
    {

        int drawnCard = deckModel.Draw();

        if (drawnCard == -1)
        {
            Debug.Log("뽑을 카드가 없음");
            return;
        }
        ExitGames.Client.Photon.Hashtable hash = new ExitGames.Client.Photon.Hashtable();
        hash.Add(DECK_PROPERTY_KEY, deckModel.getCurrentDeck().ToArray());
        PhotonNetwork.CurrentRoom.SetCustomProperties(hash);

        Debug.Log("드로우!");

        photonView.RPC("RPC_BroadcastDrawnCard", RpcTarget.All, requesterActorNumber, drawnCard );

    }

    [PunRPC]
    private void RPC_BroadcastDrawnCard(int actorNumber, int drawnCardID)
    {
        // 이제 모든 클라이언트가 이 RPC를 받고 이벤트를 실행합니다.
        // 향후 View 스크립트에서는 actorNumber를 확인하여 내 카드면 앞면으로, 남의 카드면 뒷면으로 생성하면 됩니다.
        DeckEvent.RaiseDrawn(actorNumber,drawnCardID);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(DECK_PROPERTY_KEY))
        {
            int[] deckArray = (int[])propertiesThatChanged[DECK_PROPERTY_KEY];

            deckModel.SyncDeck(deckArray);
        }
    }
}
