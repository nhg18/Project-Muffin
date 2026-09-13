using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Photon.Pun;

public class PlayerHandView : MonoBehaviour
{
    [Header("GameObjects")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] Transform drawPosition;
    [SerializeField] Transform HandPosition;
    [SerializeField] private GameObject presetCard;

    [Header("Hands of Player")]
    [SerializeField] List<GameObject> Hands = new List<GameObject>();

    public PlayerHandPresenter playerHandPresenter;


    public void draw_A_Card(CardData data)
    {
        GameObject drawedCard = Instantiate(presetCard, HandPosition);
        drawedCard.transform.position = drawPosition.position;

        CardPresenter cardPresenter = drawedCard.GetComponent<CardPresenter>();//카드 셋업
        cardPresenter.Setup(data);

        Hands.Add(drawedCard);
        PutAwayMyCards();
    }

    public void PutAwayMyCards()
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

            Vector3 localSplinePos = spline.EvaluatePosition(p);
            Vector3 worldSplinePos = splineContainer.transform.TransformPoint(localSplinePos);

            Vector3 localForward = spline.EvaluateTangent(p);
            Vector3 localUp = spline.EvaluateUpVector(p);
            Vector3 worldForward = splineContainer.transform.TransformDirection(localForward);
            Vector3 worldUp = splineContainer.transform.TransformDirection(localUp);

            quaternion rotation = Quaternion.LookRotation(-worldUp, Vector3.Cross(-worldUp, worldForward).normalized);

            Vector3 worldTarget = worldSplinePos
                            + 0.01f * i * Vector3.back
                            + new Vector3(0, 0, -i);

            Vector3 localPos = HandPosition.InverseTransformPoint(worldTarget);
            Quaternion localRot = Quaternion.Inverse(HandPosition.rotation) * rotation;

            Hands[i].transform.DOKill();
            Hands[i].transform.DOLocalMove(localPos, duration).SetEase(Ease.OutQuart).SetLink(Hands[i]);
            Hands[i].transform.DOLocalRotateQuaternion(localRot, duration).SetEase(Ease.OutQuart).SetLink(Hands[i]); ;
        }
    }

    public void HandsUp()//Presenter에서 CardEvent가 만들어지면 구독해서 이거 실행하기
    {
        Debug.Log("Up!");
        playerHandPresenter.setHandMod(true);
        HandPosition.DOMove(new Vector3(0, -3.8f, 0), 0.5f);
    }
    public void HandsDown()
    {
        Debug.Log("down!");
        playerHandPresenter.setHandMod(false);
        HandPosition.DOMove(new Vector3(0, -6.5f, 0), 0.5f);
    }
}
