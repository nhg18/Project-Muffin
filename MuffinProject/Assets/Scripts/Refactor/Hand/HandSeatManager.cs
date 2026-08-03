using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandSeatManager : Singleton<HandSeatManager>
{
    [Header("OtherHands")]
    [SerializeField] GameObject OtherHands;
    [SerializeField] List<Transform> OtherHandsPosition = new List<Transform>();
    // Start is called before the first frame update
    void Start()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int genCount = myActorNumber;
        for (int i = 0; i < (playerCount - 1); i++)
        {
            GameObject a = Instantiate(OtherHands, OtherHandsPosition[i]);
            OtherPlayerHandPresenter oph = a.GetComponentInChildren<OtherPlayerHandPresenter>();
            oph.OtherPlayerNumber = (genCount % playerCount + 1);
            genCount++;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
