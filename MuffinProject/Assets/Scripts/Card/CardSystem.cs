using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardSystem : SingletonPun<CardSystem>
{
    private List<ChainItem> chainList = new List<ChainItem>();

    Vector3 centerScreen;
    Vector3 spawnPosition;
    

    private void Start()
    {
        centerScreen = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        spawnPosition = Camera.main.ScreenToWorldPoint(centerScreen);
        spawnPosition.z = 0f;
    }

    public void RequestPlayCard(int cardID, List<int> targetPlayerNumber)
    {
        photonView.RPC("RPC_RequestPush", RpcTarget.MasterClient, cardID, targetPlayerNumber.ToArray());
    
    }

    [PunRPC]
    private void RPC_RequestPush(int cardID, int[] targetPlayerNumber, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        //유효한지 검사

        int casterActorNum = info.Sender.ActorNumber;

        photonView.RPC("RPC_BroadcastPush", RpcTarget.All, cardID, casterActorNum, targetPlayerNumber);
    }

    [PunRPC]
    private void RPC_BroadcastPush(int cardID, int casterActorNum ,int[] targetPlayerNumber)
    {
        chainList.Add(new ChainItem(cardID, casterActorNum, targetPlayerNumber));

        //todo 추가 카운터 카드 받는 코드 실행

        List<GameObject> Cards =  PlayerHandsScripts.Instance.Cards;
        foreach(GameObject prefab in Cards)
        {
            CardScript data = prefab.GetComponent<CardScript>();
            if(data.cardID == cardID)
            {
                GameObject a = Instantiate(prefab,spawnPosition, Quaternion.identity);
                Destroy(a, 3f);
                return;
            }
        }
        Debug.LogWarning("일치하는 카드 없음");


    }
}
