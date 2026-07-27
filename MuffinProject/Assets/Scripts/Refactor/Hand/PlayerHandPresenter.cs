using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHandPresenter : MonoBehaviour
{
    [Header("Hands Setting")]
    [SerializeField] int startHands = 7;
    private int HandCount;

    [SerializeField] private PlayerHandView handView;

    [SerializeField] private CardDatabase cardDatabase;

    private void Awake()
    {
        HandCount = startHands;
    }
    private void OnEnable()
    {
        DeckEvent.OnDrawn += StartDrawEvent;
    }
    private void OnDisable()
    {
        DeckEvent.OnDrawn -= StartDrawEvent;
    }

    private void StartDrawEvent(int actorNumber, int cardid)
    {

        if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber) return;

        Debug.Log("card : " + cardid);

        CardData data = cardDatabase.GetCard(cardid);

        handView.draw_A_Card(data);


    }
}
