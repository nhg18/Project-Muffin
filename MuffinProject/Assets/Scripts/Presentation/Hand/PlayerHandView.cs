using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Photon.Pun;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{

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

        public void DiscardCard(int index)
        {
            for(int i=index+1;i<Hands.Count;i++)
            {
                Hands[i].GetComponent<CardPresenter>().DownIndex();
            }
            Destroy(Hands[index].gameObject);
            Hands.RemoveAt(index);
            PutAwayMyCards();
        }

        public CardPresenter DrawCard(CardData data)
        {
            GameObject drawedCard = Instantiate(presetCard, HandPosition);
            drawedCard.transform.position = drawPosition.position;

            CardPresenter cardPresenter = drawedCard.GetComponent<CardPresenter>();
            Hands.Add(drawedCard);
            PutAwayMyCards();

            return cardPresenter;
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

        /// <summary>
        /// 카드 처리 잠금이 시작될 때 다른 카드의 드래그 · 호버를 되돌린다 (멀티터치 · 잠금 직전 입력 대비).
        /// </summary>
        public void CancelAllInteractions(CardView except)
        {
            foreach (GameObject card in Hands)
            {
                if (card == null) continue;
                CardView view = card.GetComponent<CardView>();
                if (view == null || view == except) continue;
                view.CancelInteraction();
            }
        }

        public void HandsUp()//Presenter에서 CardEvent가 만들어지면 구독해서 이거 실행하기
        {
            Debug.Log("Up!");
            playerHandPresenter.SetHandMode(true);
            HandPosition.DOMove(new Vector3(0, -3.8f, 0), 0.5f);
        }
        public void HandsDown()
        {
            Debug.Log("down!");
            playerHandPresenter.SetHandMode(false);
            HandPosition.DOMove(new Vector3(0, -6.5f, 0), 0.5f);
        }
    }
}
