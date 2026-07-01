using DG.Tweening;
using System.Collections.Generic; 
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;
using System.Linq;


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
    public List<GameObject> Cards = new List<GameObject>();

    [Header("Hands of Player")]
    [SerializeField] Dictionary<int, GameObject> Hands = new Dictionary<int, GameObject>();
    private int nextId = 0;

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
            int id = nextId++;
            x.GetComponent<CardScript>().setId(id);
            Hands[id] = x;
            GameRule.Instance.MyCardsCount++;
            PutAwayMyCards();
        }
        GameRule.Instance.RefreshMyInfo();
    }

    public void draw_A_Card()
    {
        GameObject x = Instantiate(Cards[0], HandPosition);
        x.transform.position = drawPosition.position;
        int id = nextId++;
        x.GetComponent<CardScript>().setId(id);
        Hands[id] = x;
        GameRule.Instance.MyCardsCount++;
        PutAwayMyCards();
        GameRule.Instance.RefreshMyInfo();
    }
    public void destoryCards(int id)
    {
        if (Hands.TryGetValue(id, out GameObject obj))
        {
            Destroy(obj);
            Hands.Remove(id); // 다른 ID에 영향 없음
            GameRule.Instance.MyCardsCount--;
            PutAwayMyCards();
            GameRule.Instance.RefreshMyInfo();
        }
        else
        {
            Debug.LogError("없는 카드 ID");
        }
    }

    public void debugThrowCard()
    {
        int firstKey = Hands.Keys.Min();
        Debug.Log(firstKey);
        destoryCards(firstKey);
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
        //for (int i = 0; i < Hands.Count; i++)
        int i = 0;
        foreach(int id in Hands.Keys.OrderBy(x=>x))
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

            Hands[id].transform.DOKill();
            Hands[id].transform.DOLocalMove(localPos, duration).SetEase(Ease.OutQuart).SetLink(Hands[id]);

            Hands[id].transform.DOLocalRotateQuaternion(localRot, duration).SetEase(Ease.OutQuart).SetLink(Hands[id]); ;
            i++;
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
