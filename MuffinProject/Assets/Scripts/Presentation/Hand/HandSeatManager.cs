using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chapchu.Core;

namespace Chapchu.Presentation
{

    public class HandSeatManager : Singleton<HandSeatManager>
    {
        [Header("OtherHands")]
        [SerializeField] GameObject OtherHands;
        [SerializeField] List<Transform> OtherHandsPosition = new List<Transform>();
        // Start is called before the first frame update
        void Start()
        {
            int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        
            var seatAssignments = SeatManager.Instance.GetSeatAssignments(myActorNumber); // ActorNumber, SeatIndex
            foreach (var (actorNumber, seatIndex) in seatAssignments)
            {
                if (actorNumber == myActorNumber) continue;
            
                GameObject a = Instantiate(OtherHands, OtherHandsPosition[seatIndex]);
                OtherPlayerHandPresenter oph = a.GetComponentInChildren<OtherPlayerHandPresenter>();
                oph.OtherPlayerNumber = actorNumber;
            }
        
        }

    }
}
