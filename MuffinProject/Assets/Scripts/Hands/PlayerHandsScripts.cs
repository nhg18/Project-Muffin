
using DG.Tweening;
using System.Collections.Generic; 
using UnityEngine;

using Unity.Mathematics;
using UnityEngine.Splines;


public class PlayerHandsScripts : MonoBehaviour
{

    public static PlayerHandsScripts Instance { get; private set; }
    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [Header("Cards of this game")]
    [SerializeField] List<GameObject> Cards = new List<GameObject>();

    [Header("Hands of Player")]
    [SerializeField] List<GameObject> Hands = new List<GameObject>();

    [Header("GameObjects")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] Transform drawPosition;
    [SerializeField] Transform HandPosition;

    public void HandOut_Cards()
    {
        for (int i = 0; i < GameRule.Instance.startHands; i++)
        {
            GameObject x = Instantiate(Cards[0], HandPosition);
            x.transform.position = drawPosition.position;
            Hands.Add(x);
            GameRule.Instance.MyCardsCount++;
            PutAwayMyCards();
        }
        GameRule.Instance.RefreshMyInfo();
    }

    public void draw_A_Card()
    {
        GameObject x = Instantiate(Cards[0], HandPosition);
        x.transform.position = drawPosition.position;
        Hands.Add(x);
        GameRule.Instance.MyCardsCount++;
        PutAwayMyCards();
        GameRule.Instance.RefreshMyInfo();
    }
    public void destoryCards(int number)
    {
        Destroy(Hands[number]);
        Hands.RemoveAt(number);
        GameRule.Instance.MyCardsCount--;
        PutAwayMyCards();
        GameRule.Instance.RefreshMyInfo();
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
        return;

    }

    #region HandFunc
    public void HandsUp()
    {
        Debug.Log("Up!");
        GameRule.Instance.isHandMod = true;
        HandPosition.DOMove(new Vector3(0, -3.8f, 0), 0.5f);
    }
    public void HandsDown()
    {
        Debug.Log("down!");
        GameRule.Instance.isHandMod = false;
        HandPosition.DOMove(new Vector3(0, -6.5f, 0), 0.5f);
    }
    #endregion
}
