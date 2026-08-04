using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OtherPlayerHandPresenter : MonoBehaviourPunCallbacks
{
    private int HandCount=0;
    public int OtherPlayerNumber=0;
    private static int curNum = 0; //자리 배치를 위한 로직넘버
    [SerializeField] private OtherPlayerHandView handView;
    private void Awake()
    {
        
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
        if (OtherPlayerNumber != actorNumber) return;
        HandCount++;
        handView.draw_A_Card();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // 내가 관찰 중인 상대방의 정보가 맞고, 변경된 속성 중 "HandCount"가 있다면
        if (targetPlayer.ActorNumber == OtherPlayerNumber && changedProps.ContainsKey("HandCount"))
        {
            int realCount = (int)changedProps["HandCount"];

            // 만약 네트워크 렉이나 씹힘으로 인해 내 화면의 카드 수(HandCount)와
            // 상대방 장부에 적힌 수(realCount)가 다르다면 강제로 맞춰줍니다.
            if (HandCount < realCount)
            {
                int needed = realCount - HandCount;
                for (int i = 0; i < needed; i++)
                {
                    HandCount++;
                    handView.draw_A_Card(); // 누락된 카드 보충
                }
                Debug.LogWarning($"[동기화 교정] 카드가 부족하여 {needed}장 강제 추가");
            }
        }
    }
}
