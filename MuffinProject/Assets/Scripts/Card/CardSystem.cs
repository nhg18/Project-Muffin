using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardSystem : MonoBehaviourPunCallbacks
{
    private List<ChainItem> chainList = new List<ChainItem>();

    public void RequestPlayCard(int cardID, List<int> targetPlayerNumber)
    {
        photonView.RPC("RPC_RequestPush", RpcTarget.MasterClient, cardID, targetPlayerNumber);
    
    }

    [PunRPC]
    private void RPC_RequestPush(int cardID, List<int> targetPlayerNumber, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int casterActorNum = info.Sender.ActorNumber;
        chainList.Add(new ChainItem(cardID, casterActorNum, targetPlayerNumber));

        //todo 추가 카운터 카드 받는 코드 실행
    }
}
