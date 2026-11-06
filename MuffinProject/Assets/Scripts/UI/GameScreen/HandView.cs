using System;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패를 스플라인 부채꼴로 놓는다 (10-ui.md 4절 · 2-1 「손패」). 순서는 드로우 순 — 정렬하지 않는다 (05-deck.md 6절).
    /// 카드는 이 오브젝트의 자식이고, 위치 · 회전은 스플라인의 로컬 좌표를 그대로 쓴다.
    /// 올리기 · 내리기(HandMode)는 이 오브젝트 전체를 위아래로 움직인다 — 카드 배치와 독립이다. 호버 확대 · 드래그는 올라온 상태에서만.
    /// 드래그를 놓으면 어디에 놓았는지(화면 좌표)만 알린다 — 드롭 영역 · 카드 사용 판정은 프레젠터가 한다. 이 뷰는 서버를 모른다.
    /// 잠금(SetLocked)은 대상 선택 중 손패 입력 전부를 막는다 (10-ui.md 6절).
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class HandView : MonoBehaviour
    {
        // 10장까지는 스플라인 길이의 1/10 간격, 넘으면 1/(장수+1) 로 좁힌다 (10-ui.md 4절 「10장 초과 시 간격 축소」).
        private const int SpacingBaseCount = 10;

        [Header("종류별 카드 프리팹")]
        [SerializeField] private HandCardView actionCardPrefab;
        [SerializeField] private HandCardView counterCardPrefab;
        [SerializeField] private HandCardView trapCardPrefab;

        [Header("배치 연출")]
        [Tooltip("새 카드가 자리로 날아가고, 빠진 뒤 나머지가 다시 놓이는 초.")]
        [SerializeField] private float moveDuration = 1f;

        [Header("올리기 · 내리기 (HandMode)")]
        [Tooltip("내려간 상태에서 화면에 보이는 카드 높이. 카드 높이에서 이만큼을 뺀 만큼 아래로 내린다.")]
        [SerializeField] private float loweredVisibleHeight = 100f;
        [Tooltip("손패 전체가 올라가고 내려가는 초.")]
        [SerializeField] private float raiseDuration = 0.3f;

        [Header("호버 · 드래그 (올라온 상태에서만)")]
        [Tooltip("포인터를 올린 카드의 확대 배율. 드래그 중에도 유지한다.")]
        [SerializeField] private float hoverScale = 1.3f;
        [Tooltip("포인터를 올린 카드가 자기 자리에서 위로 올라가는 거리.")]
        [SerializeField] private float hoverLift = 40f;
        [Tooltip("호버 확대 · 복귀 초. 드롭 실패 · 요청 뒤 손패로 돌아가는 시간도 같다.")]
        [SerializeField] private float hoverDuration = 0.12f;

        private readonly List<HandCardView> _cards = new List<HandCardView>();
        private SplineContainer _spline;
        private RectTransform _rect;
        private HandCardView _hovered;
        private HandCardView _dragging;
        private Vector2 _dragOffset; // 잡은 지점을 유지하려고 — 카드 로컬 위치 − 포인터 로컬 위치

        public int Count => _cards.Count;

        /// <summary>손패가 올라와 있는가 (HandMode ON).</summary>
        public bool IsRaised { get; private set; }

        /// <summary>잠김 — 호버 · 드래그 · 올리기 · 내리기를 모두 무시한다 (대상 선택 중, 10-ui.md 6절).</summary>
        public bool IsLocked { get; private set; }

        /// <summary>드래그하던 카드를 놓았다 (카드, 화면 좌표). 자리로 돌릴지는 받는 쪽이 ReturnCard 로 정한다. 아무도 안 받으면 바로 돌아간다.</summary>
        public event Action<HandCardView, Vector2> OnCardDropped;

        private float CardHeight => ((RectTransform)actionCardPrefab.transform).sizeDelta.y;
        private float LoweredY => -(CardHeight - loweredVisibleHeight);

        private void Awake()
        {
            _spline = GetComponent<SplineContainer>();
            _rect = (RectTransform)transform;

            if (actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null)
            {
                Debug.LogError($"[{nameof(HandView)}] 종류별 카드 프리팹 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            // 기본은 내려간 상태 — 화면을 가리지 않는다 (10-ui.md 4절).
            _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, LoweredY);
        }

        // 올라온 상태에서 손패 카드 밖을 탭하면 내린다 (10-ui.md 4절 「HandMode OFF」). 덱 등 다른 버튼 탭도 "손패 외 영역"이다.
        // 터치는 구 Input Manager 가 마우스로 흉내 내므로(simulateMouseWithTouches) 마우스 버튼만 본다.
        private void Update()
        {
            if (!IsRaised || IsLocked || _dragging != null || !Input.GetMouseButtonDown(0)) return;
            if (IsPointerOverAnyCard(Input.mousePosition)) return;

            Lower();
        }

        /// <summary>카드 종류에 맞는 프리팹을 spawnWorldPosition(덱 자리)에 만들어 손패 끝에 붙이고 다시 놓는다.</summary>
        public HandCardView AddCard(int instanceId, CardData data, Vector3 spawnWorldPosition)
        {
            HandCardView card = Instantiate(PrefabOf(data.type), transform);
            card.transform.position = spawnWorldPosition;
            card.Setup(instanceId, data);
            card.SetRaycastBlocking(!IsLocked);
            card.OnHoverEntered += HandleHoverEntered;
            card.OnHoverExited += HandleHoverExited;
            card.OnClicked += HandleClicked;
            card.OnDragStarted += HandleDragStarted;
            card.OnDragged += HandleDragged;
            card.OnDragEnded += HandleDragEnded;

            _cards.Add(card);
            Layout();
            return card;
        }

        /// <summary>해당 장을 없애고 나머지를 다시 놓는다. 없으면 false.</summary>
        public bool RemoveCard(int instanceId)
        {
            int index = _cards.FindIndex(c => c.InstanceId == instanceId);
            if (index < 0) return false;

            HandCardView card = _cards[index];
            _cards.RemoveAt(index);
            if (_hovered == card)
                _hovered = null;
            if (_dragging == card)
                _dragging = null;
            card.OnHoverEntered -= HandleHoverEntered;
            card.OnHoverExited -= HandleHoverExited;
            card.OnClicked -= HandleClicked;
            card.OnDragStarted -= HandleDragStarted;
            card.OnDragged -= HandleDragged;
            card.OnDragEnded -= HandleDragEnded;
            card.transform.DOKill();
            Destroy(card.gameObject);

            Layout();
            return true;
        }

        /// <summary>손패 전체를 위로 올린다 (HandMode ON). 잠겨 있으면 무시.</summary>
        public void Raise()
        {
            if (IsRaised || IsLocked) return;

            IsRaised = true;
            MoveRootTo(0f);
        }

        /// <summary>손패 전체를 아래로 내린다 (HandMode OFF). 호버 중인 카드는 자리로 돌려 놓는다. 잠겨 있으면 무시.</summary>
        public void Lower()
        {
            if (!IsRaised || IsLocked) return;

            IsRaised = false;
            ReleaseHovered();
            MoveRootTo(LoweredY);
        }

        /// <summary>
        /// 잠그면 호버 · 드래그 · 올리기 · 내리기를 무시하고, 카드 전부가 레이캐스트를 받지 않는다 — 떠 있는 카드가 좌석 탭을 가리지 않게.
        /// 드래그 중이면 끊는다 (카드는 그 자리에 둔다 — 돌릴지는 ReturnCard 로). 풀어도 카드를 움직이지 않는다 — 떠 있던 카드는 받는 쪽이 ReturnCard 로 돌린다.
        /// </summary>
        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            if (locked)
                _dragging = null;
            foreach (HandCardView card in _cards)
                card.SetRaycastBlocking(!locked);
        }

        /// <summary>카드를 자기 자리(위치 · 기울기 · 형제 순서 · 원래 크기)로 돌린다. 호버 · 드래그 상태도 푼다. 손패에 없으면 무시.</summary>
        public void ReturnCard(HandCardView card)
        {
            int index = _cards.IndexOf(card);
            if (index < 0) return;

            if (_hovered == card)
                _hovered = null;
            if (_dragging == card)
                _dragging = null;
            PlaceCard(index, hoverDuration);
        }

        /// <summary>가운데(0.5)를 기준으로 좌우 대칭으로 놓는다. 오른쪽 카드가 위에 그려진다. 호버 · 드래그 중이던 카드도 자리로 돌아간다.</summary>
        public void Layout()
        {
            _hovered = null;
            _dragging = null;
            for (int i = 0; i < _cards.Count; i++)
                PlaceCard(i, moveDuration);
        }

        // i 번째 카드를 자기 자리(위치 · 기울기 · 형제 순서 · 원래 크기)로 보낸다.
        private void PlaceCard(int index, float duration)
        {
            Slot(index, out Vector3 position, out float angle);

            Transform card = _cards[index].transform;
            card.SetSiblingIndex(index);
            card.DOKill();
            card.DOLocalMove(position, duration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            card.DOLocalRotateQuaternion(Quaternion.Euler(0f, 0f, angle), duration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            card.DOScale(1f, duration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
        }

        // i 번째 카드의 부채꼴 자리 — 스플라인 위 위치와 접선 기울기.
        private void Slot(int index, out Vector3 position, out float angle)
        {
            float spacing = _cards.Count > SpacingBaseCount ? 1f / (_cards.Count + 1f) : 1f / SpacingBaseCount;
            float first = 0.5f - (_cards.Count - 1) * spacing / 2f;
            float t = first + index * spacing;

            Spline spline = _spline.Spline;
            float3 point = spline.EvaluatePosition(t);
            float3 tangent = spline.EvaluateTangent(t);
            position = new Vector3(point.x, point.y, 0f);
            angle = math.degrees(math.atan2(tangent.y, tangent.x));
        }

        private void MoveRootTo(float y)
        {
            _rect.DOKill();
            DOTween.To(() => _rect.anchoredPosition, v => _rect.anchoredPosition = v, new Vector2(_rect.anchoredPosition.x, y), raiseDuration)
                .SetEase(Ease.OutQuart).SetTarget(_rect).SetLink(gameObject);
        }

        // 카드를 확대 · 세우고 최상단으로 — 호버와 드래그 시작이 같이 쓴다.
        private void Enlarge(HandCardView card, Vector3 position)
        {
            Transform t = card.transform;
            t.SetAsLastSibling();
            t.DOKill();
            t.DOLocalMove(position, hoverDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DOLocalRotateQuaternion(Quaternion.identity, hoverDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DOScale(hoverScale, hoverDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
        }

        // 내려간 상태에서 카드를 탭하면 올린다 (10-ui.md 4절 「HandMode ON」). 올라온 상태의 카드 탭은 아무것도 하지 않는다 (모바일 「선택 + 확대」는 이후 작업).
        private void HandleClicked(HandCardView card)
        {
            if (!IsRaised)
                Raise();
        }

        // 올라온 상태에서만 확대 + 세우기 + 최상단 (10-ui.md 4절 「카드 호버」).
        private void HandleHoverEntered(HandCardView card)
        {
            if (!IsRaised || IsLocked || _dragging != null) return;
            if (_hovered != null && _hovered != card)
                ReleaseHovered();

            int index = _cards.IndexOf(card);
            if (index < 0) return;

            _hovered = card;
            Slot(index, out Vector3 position, out float _);
            Enlarge(card, position + Vector3.up * hoverLift);
        }

        // 드래그 중에는 포인터가 카드를 잠깐 벗어나도 무시한다 — 자리로 튀지 않게. 잠겨 있을 때도 떠 있는 카드를 건드리지 않는다.
        private void HandleHoverExited(HandCardView card)
        {
            if (_hovered != card || _dragging == card || IsLocked) return;

            ReleaseHovered();
        }

        // 드래그 시작 — 확대 · 세움 · 최상단을 유지한 채 잡은 지점을 기준으로 따라온다 (10-ui.md 4절 「카드 드래그」).
        private void HandleDragStarted(HandCardView card, Vector2 screenPosition)
        {
            if (!IsRaised || IsLocked) return;
            if (!_cards.Contains(card)) return;

            if (_hovered != null && _hovered != card)
                ReleaseHovered();
            _hovered = card;
            _dragging = card;

            Vector2 pointer = ToLocal(screenPosition);
            _dragOffset = (Vector2)card.transform.localPosition - pointer;
            Enlarge(card, card.transform.localPosition);
        }

        private void HandleDragged(HandCardView card, Vector2 screenPosition)
        {
            if (_dragging != card) return;

            Vector2 pointer = ToLocal(screenPosition);
            card.transform.DOKill(); // 확대 트윈이 끝나기 전에 움직이면 이동 트윈과 싸운다
            card.transform.localScale = Vector3.one * hoverScale;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localPosition = pointer + _dragOffset;
        }

        private void HandleDragEnded(HandCardView card, Vector2 screenPosition)
        {
            if (_dragging != card) return;

            _dragging = null;
            if (OnCardDropped == null)
            {
                ReturnCard(card);
                return;
            }

            OnCardDropped.Invoke(card, screenPosition);
        }

        // 호버 중인 카드를 자기 자리로 돌려 놓는다.
        private void ReleaseHovered()
        {
            if (_hovered == null) return;

            int index = _cards.IndexOf(_hovered);
            _hovered = null;
            if (index >= 0)
                PlaceCard(index, hoverDuration);
        }

        // 화면 좌표 → 이 손패 루트의 로컬 좌표. Screen Space Overlay 캔버스라 카메라는 null.
        private Vector2 ToLocal(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPosition, null, out Vector2 local);
            return local;
        }

        private bool IsPointerOverAnyCard(Vector2 screenPosition)
        {
            // 회전 · 확대된 카드도 RectTransform 기준으로 맞춘다.
            for (int i = 0; i < _cards.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_cards[i].transform, screenPosition, null))
                    return true;
            }

            return false;
        }

        private HandCardView PrefabOf(CardType type)
        {
            switch (type)
            {
                case CardType.Counter: return counterCardPrefab;
                case CardType.Trap:    return trapCardPrefab;
                default:               return actionCardPrefab;
            }
        }
    }
}
