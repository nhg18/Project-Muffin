using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Chapchu.Game;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{

    public class CardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI연결")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text description;
        [SerializeField] private SpriteRenderer cardImage; 
        [SerializeField] private  CardType type;

        [Header("Hover Settings")]
        [SerializeField] private float basicScaleX = 2f;
        [SerializeField] private float basicScaleY = 3f;

        [SerializeField] private float UpScale = 2f;
        [SerializeField] private int basicLayerOrder = 2;

        [Header("Drag Settings")]
        [SerializeField] private float dragScale = 1.1f;
        [SerializeField] private int dragSortingOrder = 10;
        [SerializeField] private float returnSpeed = 0.15f;

        public CardPresenter cardPresenter;


        private Vector3 originalPosition;

        private SpriteRenderer spriteRenderer;
        private bool isDragging = false;
        private float zDepth; 

        private bool isHandMode = false;
        private bool isHovered = false;

        private SpriteRenderer[] childRenderers;

        /// <summary>
        /// 지금 이 카드를 만질 수 있는가. HandMode 이고, 손패가 다른 카드를 처리 중이 아니어야 한다.
        /// 손패에 속하지 않은 카드(Hand == null)는 HandMode 만 본다.
        /// </summary>
        private bool CanInteract
        {
            get
            {
                if (!isHandMode) return false;
                if (cardPresenter != null && cardPresenter.Hand != null && cardPresenter.Hand.IsCardPlayInProgress) return false;
                return true;
            }
        }

        public void Setup(CardData data)
        {
            if (data == null)
            {
                Debug.Log("null null null");
                return;
            }

            Debug.Log("data : "+ data.cardName+" "+ data.description);
            nameText.text = data.cardName;
            description.text = data.description;
            cardImage.sprite = data.cardImage;
            type = data.type;

            gameObject.name = $"Card_{data.id}_{data.cardName}";
        }

        private void OnEnable()
        {
            GameEvents.OnHandModeChanged += SetHandMode;
        }

        private void OnDisable()
        {
            GameEvents.OnHandModeChanged -= SetHandMode;
        }

        private void Start()
        {
            childRenderers = GetComponentsInChildren<SpriteRenderer>();//order in layer controls
        }

        private void SetHandMode(bool handmod)
        {
            this.isHandMode = handmod;
        }


        public void BringToFront()
        {
            foreach (SpriteRenderer sr in childRenderers)
            {
                sr.sortingOrder = basicLayerOrder + 1;
            }
        }

        public void BringToOriginal()
        {
            foreach (SpriteRenderer sr in childRenderers)
            {
                sr.sortingOrder = basicLayerOrder;
            }
        }

        private void HoverCard()
        {
            isHovered = true;
            BringToFront();
            transform.DOScaleX(basicScaleX * UpScale, 0.1f);
            transform.DOScaleY(basicScaleY * UpScale, 0.1f);
        }
        private void UnHoverCard()
        {
            isHovered = false;
            BringToOriginal();
            transform.DOScaleX(basicScaleX, 0.1f);
            transform.DOScaleY(basicScaleY, 0.1f);
        }

        /// <summary>
        /// 진행 중인 드래그 · 호버를 되돌린다. 손패가 카드 처리 잠금을 걸 때 호출한다.
        /// </summary>
        public void CancelInteraction()
        {
            if (isDragging)
            {
                isDragging = false;
                StartCoroutine(ReturnToOriginRoutine());
            }
            if (isHovered)
            {
                UnHoverCard();
            }
        }


        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("hover!");
            if (CanInteract)
            {
                HoverCard();
            }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            // 잠금 중이라도 이미 커진 카드는 되돌린다. CanInteract 로 막으면 확대된 채 남는다.
            if (isHovered)
            {
                UnHoverCard();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract)
                return;

            originalPosition = transform.localPosition;

            zDepth = Camera.main.WorldToScreenPoint(transform.localPosition).z;

            isDragging = true;
            Debug.Log("클릭");
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!CanInteract)
                return;

            if (!isDragging) return;

            Vector3 screenPos = new Vector3(
                eventData.position.x,
                eventData.position.y,
                zDepth
            );
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = originalPosition.z;
            transform.position = worldPos;
        }


        private System.Collections.IEnumerator ReturnToOriginRoutine()
        {
            Vector3 startPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < returnSpeed)
            {
                float t = elapsed / returnSpeed;
                t = t * t * (3f - 2f * t); // Smoothstep 보간
                transform.localPosition = Vector3.Lerp(startPos, originalPosition, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPosition;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;

            // 드래그 도중 잠금이 걸렸으면(다른 카드가 먼저 드롭됨) 드롭하지 않고 되돌린다.
            if (CanInteract && isDropArea(transform.position) && cardPresenter.LocalConditionCheck())
            {
                cardPresenter.OnCardDropped();
            }
            else
            {
                StartCoroutine(ReturnToOriginRoutine());
            }
        }

        public void ReturnToOrigin()
        {
            StartCoroutine(ReturnToOriginRoutine());

        }

        private bool isDropArea(Vector2 position)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(position);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (hit.gameObject.tag == "CardDropArea")
                {
                    return true;
                }
            }
            return false;
        }




    }
}
