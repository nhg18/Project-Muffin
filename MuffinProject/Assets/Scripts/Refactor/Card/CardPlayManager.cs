using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardPlayManager : SingletonPun<CardPlayManager>
{
    [SerializeField] private GameObject dummyPresetCard;
    [SerializeField] private CardDatabase cardDatabase;
    [SerializeField] private float waitTimeforNextCard = 4f;
    [SerializeField] private float showingTime = 2.5f;

    private List<ChainItem> chainList = new List<ChainItem>();

    Vector3 centerScreen;
    Vector3 spawnPosition;

    private bool isResolutioning = false;


    private void Start()
    {
        centerScreen = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        spawnPosition = Camera.main.ScreenToWorldPoint(centerScreen);
        spawnPosition.z = 0f;
    }

    private CardData FindCard(int cardID) // 수정 필요
    {
        CardData data = cardDatabase.GetCard(cardID);
        if(data != null)
        {
            return data;
        }
        return null;
    }

    public void RequestCancelNext()
    {
        if (chainList.Count >= 2)
        {
            chainList[chainList.Count - 2].isCanceled = true;
            Debug.Log("취소 성공");
        }
        else
        {
            Debug.Log("오류! 체인리스트");
        }
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
        if (isResolutioning == true) return;

        int casterActorNum = info.Sender.ActorNumber;

        photonView.RPC("RPC_BroadcastPush", RpcTarget.All, cardID, casterActorNum, targetPlayerNumber);
    }

    [PunRPC]
    private void RPC_BroadcastPush(int cardID, int casterActorNum, int[] targetPlayerNumber)
    {
        CancelInvoke("StartChainResolution");
        chainList.Add(new ChainItem(cardID, casterActorNum, targetPlayerNumber));

        //todo 추가 카운터 카드 받는 코드 실행
        CardData data = FindCard(cardID);
        if (data != null)
        {
            GameObject a = Instantiate(dummyPresetCard, spawnPosition, Quaternion.identity);
            CardPresenter cardPresenter = a.GetComponent<CardPresenter>();
            cardPresenter.Setup(data);
            Destroy(a, waitTimeforNextCard);
        }
        else
        {
            Debug.LogWarning("일치하는 카드 없음");
        }
        Invoke("StartChainResolution", waitTimeforNextCard+0.5f);
    }



    public void StartChainResolution()
    {
        isResolutioning = true;

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("start resolution");
            photonView.RPC("RPC_ResolveChain", RpcTarget.All);
        }




        isResolutioning = false;
    }

    [PunRPC]
    private void RPC_ResolveChain()
    {
        StartCoroutine(ResolveChainRoutine());
    }
    private IEnumerator ResolveChainRoutine()
    {
        for (int i = chainList.Count - 1; i >= 0; i--)
        {
            CardData data = FindCard(chainList[i].cardID);
            Debug.Log("cardpop" + chainList[i].cardID);

            if (data != null)
            {
                if (!chainList[i].isCanceled)
                {
                    GameObject a = Instantiate(dummyPresetCard, spawnPosition, Quaternion.identity);
                    CardPresenter cardPresenter = a.GetComponent<CardPresenter>();
                    cardPresenter.Setup(data);
                    //효과 실행 코드

                    Destroy(a, showingTime);
                }
                else
                {
                    Debug.Log("카드 취소");
                }


            }
            else
            {
                Debug.LogWarning("일치하는 카드 없음");
            }
            chainList.RemoveAt(i);
            yield return new WaitForSeconds(showingTime+0.1f);
        }
        chainList.Clear();
    }
}
