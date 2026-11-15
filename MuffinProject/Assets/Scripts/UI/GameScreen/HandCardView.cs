using System;
using Chapchu.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 카드 한 장 (10-ui.md 2-1 「손패」). 종류별 틀 이미지는 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명만 입힌다.
    /// 포인터 진입 · 이탈 · 클릭 · 드래그를 이벤트로만 알린다 — 올리기 · 호버 · 드래그 판정은 HandView 가 한다 (10-ui.md 4절).
    /// 어느 장인지(InstanceId)와 무슨 카드인지(CardId)만 기억한다.
    /// CanvasGroup 은 잠금 중 레이캐스트를 끄는 데 쓴다 — 놓은 자리에 떠 있는 카드가 좌석 탭을 가리지 않게.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HandCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image artImage;
        [SerializeField] private TMP_Text descriptionText;

        private CanvasGroup _group;

        public int InstanceId { get; private set; }

        /// <summary>카드 종류 ID — 프레젠터가 덱 에셋에서 대상 타입 등을 찾는 데 쓴다.</summary>
        public int CardId { get; private set; }

        /// <summary>포인터가 이 카드 위에 들어왔다.</summary>
        public event Action<HandCardView> OnHoverEntered;
        /// <summary>포인터가 이 카드에서 나갔다.</summary>
        public event Action<HandCardView> OnHoverExited;
        /// <summary>이 카드를 탭(클릭)했다. 드래그가 시작되면 오지 않는다.</summary>
        public event Action<HandCardView> OnClicked;
        /// <summary>드래그 시작 (카드, 화면 좌표).</summary>
        public event Action<HandCardView, Vector2> OnDragStarted;
        /// <summary>드래그 중 (카드, 화면 좌표).</summary>
        public event Action<HandCardView, Vector2> OnDragged;
        /// <summary>드래그 끝 — 놓은 화면 좌표.</summary>
        public event Action<HandCardView, Vector2> OnDragEnded;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();

            if (nameText == null || artImage == null || descriptionText == null)
                Debug.LogError($"[{nameof(HandCardView)}] 이름 · 그림 · 설명 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>false 면 포인터 이벤트를 받지 않고 아래 오브젝트(좌석 등)로 넘긴다. 보이는 것은 그대로.</summary>
        public void SetRaycastBlocking(bool blocking) => _group.blocksRaycasts = blocking;

        public void Setup(int instanceId, CardData data)
        {
            InstanceId = instanceId;
            CardId = data.id;
            nameText.text = data.cardName;
            descriptionText.text = data.description;

            // 그림이 아직 없는 카드가 있다 — 그림 칸만 비워 둔다.
            artImage.sprite = data.cardImage;
            artImage.enabled = data.cardImage != null;

            gameObject.name = $"HandCard_{instanceId}_{data.id}";
        }

        public void OnPointerEnter(PointerEventData eventData) => OnHoverEntered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => OnHoverExited?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);

        public void OnBeginDrag(PointerEventData eventData) => OnDragStarted?.Invoke(this, eventData.position);

        public void OnDrag(PointerEventData eventData) => OnDragged?.Invoke(this, eventData.position);

        public void OnEndDrag(PointerEventData eventData) => OnDragEnded?.Invoke(this, eventData.position);
    }
}
