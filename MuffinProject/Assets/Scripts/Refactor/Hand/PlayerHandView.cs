using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class PlayerHandView : MonoBehaviour
{
    [Header("Hands Setting")]
    [SerializeField] int startHands = 7;

    [Header("GameObjects")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] Transform drawPosition;
    [SerializeField] Transform HandPosition;

    [Header("Hands of Player")]
    [SerializeField] List<GameObject> Hands = new List<GameObject>();



    public void draw_A_Card()
    {
        //GameObject x = Instantiate(Cards[UnityEngine.Random.Range(0, 2)], HandPosition);
        //x.transform.position = drawPosition.position;
        //Hands.Add(x);
        //PutAwayMyCards();
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
            Vector3 splinePosition = spline.EvaluatePosition(p);
            Vector3 forward = spline.EvaluateTangent(p);
            Vector3 up = spline.EvaluateUpVector(p);
            quaternion rotation = Quaternion.LookRotation(-up, Vector3.Cross(-up, forward).normalized);

            Vector3 worldTarget = splinePosition
                            + HandPosition.position
                            + 0.01f * i * Vector3.back
                            + new Vector3(0, 0, -i);
            Vector3 localPos = HandPosition.InverseTransformPoint(worldTarget);
            Quaternion localRot = Quaternion.Inverse(HandPosition.rotation) * rotation;

            Hands[i].transform.DOKill();
            Hands[i].transform.DOLocalMove(localPos, duration).SetEase(Ease.OutQuart).SetLink(Hands[i]);
            Hands[i].transform.DOLocalRotateQuaternion(localRot, duration).SetEase(Ease.OutQuart).SetLink(Hands[i]); ;
        }
    }
}
