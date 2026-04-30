using DG.Tweening;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.Splines;

public class OtherPlayerHands : MonoBehaviourPunCallbacks
{
    public int PlayerNumber=0;
    [SerializeField] private GameObject CardBack;

    [SerializeField] Transform handsPosition;

    [SerializeField] SplineContainer splineContainer;


    private Transform drawPosition;
    private int LocalCardsCount = 0;
    private List<GameObject> Hands = new List<GameObject>();
    private void Start()
    {
        drawPosition = GameObject.FindWithTag("DrawCards").transform;
        for(int i = 0; i < GameRule.Instance.startHands; i++)
        {
            DrawCards();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (targetPlayer.IsLocal)
        {
            Debug.Log("내 프로퍼티 변경");
            return;
        }

        if(targetPlayer.ActorNumber != PlayerNumber)
        {
            //나의 정보가 아님
            return;
        }

            if (changedProps.ContainsKey("CardsCount")) //누가뽑았는지 추가 필요 (다른 플레이어를 생성할때 번호를 부여하고 그번호가 맞는지 확인하고 해당 코드를 실행하는 식)
        {
            int CardsCount = (int)changedProps["CardsCount"];
            Debug.Log("상대가 " + CardsCount + "장 가지고 있음");
            if(CardsCount > LocalCardsCount)
            {
                while(CardsCount > LocalCardsCount)
                {
                    DrawCards();
                }
            }
            else if(CardsCount < LocalCardsCount)
            {
                //카드 버리기
            }
        }

    }

    private void DrawCards()
    {
        LocalCardsCount++;
        GameObject x = Instantiate(CardBack, handsPosition);
        x.transform.position = drawPosition.position;
        Hands.Add(x);
        PutAwayOtherCards();
    }

    public void PutAwayOtherCards()
    {
        float cardSpacing;
        if (Hands.Count == 0) return;
        else if (Hands.Count > 10)
        {
            cardSpacing = 1f / (Hands.Count + 1f);
        }
        else
        {
            cardSpacing = 1f / 10f;
        }
        float firstCardPosition = 0.5f - (Hands.Count - 1) * cardSpacing / 2;
        float duration = 1f;
        Spline spline = splineContainer.Spline;
        for (int i = 0; i < Hands.Count; i++)
        {
            float p = firstCardPosition + i * cardSpacing;
            Vector3 splinePosition = spline.EvaluatePosition(p);
            Vector3 forward = spline.EvaluateTangent(p);
            Vector3 up = spline.EvaluateUpVector(p);

            Vector3 worldSplinePos = splineContainer.transform.TransformPoint(splinePosition);
            Vector3 worldForward = splineContainer.transform.TransformDirection(forward);
            Vector3 worldUp = splineContainer.transform.TransformDirection(up);


            Quaternion rotation = Quaternion.LookRotation(-worldUp, Vector3.Cross(-worldUp, worldForward).normalized);
            Hands[i].transform.DOMove(worldSplinePos  + 0.01f * i * Vector3.back + new Vector3(0, 0, -i), duration);
            Hands[i].transform.DORotateQuaternion(rotation, duration);
        }
        return;

    }
}
