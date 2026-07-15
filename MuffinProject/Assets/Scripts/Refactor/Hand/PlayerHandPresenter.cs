using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHandPresenter : MonoBehaviour
{
    private void OnEnable()
    {
        DeckEvent.OnDrawn += StartDrawEvent;
    }

    private void StartDrawEvent(int actorNumber, Card card)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber) return;

    }
}
