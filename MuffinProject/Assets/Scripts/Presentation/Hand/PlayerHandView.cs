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

        // 런타임에만 채운다. 순서는 PlayerHandPresenter 의 카드 목록과 같다 (드로우 순).
        [Header("Hands of Player")]
        [SerializeField] List<CardView> Hands = new List<CardView>();

        /// <summary>index 칸의 카드를 없애고 나머지를 재배치한다. 어느 칸인지는 Presenter 가 정한다.</summary>
        public void DiscardCard(int index)
        {
            Destroy(Hands[index].gameObject);
            Hands.RemoveAt(index);
            PutAwayMyCards();
        }

        /// <summary>카드 프리팹을 덱 위치에 만들어 손패 끝에 붙인다. 내용(Setup)은 Presenter 가 채운다.</summary>
        public CardView DrawCard()
        {
            GameObject drawedCard = Instantiate(presetCard, HandPosition);
            drawedCard.transform.position = drawPosition.position;

            CardView cardView = drawedCard.GetComponent<CardView>();
            Hands.Add(cardView);
            PutAwayMyCards();

            return cardView;
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

                GameObject card = Hands[i].gameObject;
                card.transform.DOKill();
                card.transform.DOLocalMove(localPos, duration).SetEase(Ease.OutQuart).SetLink(card);
                card.transform.DOLocalRotateQuaternion(localRot, duration).SetEase(Ease.OutQuart).SetLink(card);
            }
        }

        /// <summary>
        /// 카드 처리 잠금이 시작될 때 다른 카드의 드래그 · 호버를 되돌린다 (멀티터치 · 잠금 직전 입력 대비).
        /// </summary>
        public void CancelAllInteractions(CardView except)
        {
            foreach (CardView view in Hands)
            {
                if (view == null || view == except) continue;
                view.CancelInteraction();
            }
        }

        // 손패 올리기 · 내리기 연출만. HandMode 상태는 PlayerHandPresenter.SetHandMode 가 갖고 여기를 부른다.
        public void HandsUp()
        {
            HandPosition.DOMove(new Vector3(0, -3.8f, 0), 0.5f);
        }
        public void HandsDown()
        {
            HandPosition.DOMove(new Vector3(0, -6.5f, 0), 0.5f);
        }
    }
}
