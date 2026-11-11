using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 누르는 버튼의 손맛 — 누르면 몸체가 아래 두께(그림자) 쪽으로 가라앉는다 (시안 v2 .btn:active).
    /// 「꺼진 모양」 은 비활성과 달리 눌린다 — 「동의하고 계속」 처럼 눌러야 빠진 것을 알려 주는 버튼 (20-title-ui-full.md 7-11).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PressableButton : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private static readonly Color OffColor = new Color32(0xB8, 0xAE, 0xA4, 0xFF);

        [SerializeField] private RectTransform body;

        [Tooltip("누를 때 몸체가 내려가는 거리 (캔버스 px). 시안 .btn 2 × 2 · 큰 버튼 3 × 2")]
        [SerializeField] private float pressDepth = 4f;

        // PillButton(로비 · 룸) 의 호버 · 단단한 그림자 (12-title-ui.md 10-1) — 타이틀 v2 버튼은 비워 둔다
        [Tooltip("몸체 안의 단단한 그림자 — 누를 때 제자리에 남는다. 없으면 비운다")]
        [SerializeField] private RectTransform hardShadow;

        [Tooltip("마우스를 올렸을 때 채움 색 — 투명이면 색을 바꾸지 않는다")]
        [SerializeField] private Color hoverColor = Color.clear;

        [Tooltip("마우스를 올렸을 때 몸체가 올라가는 거리 (캔버스 px)")]
        [SerializeField] private float hoverLift;

        // 꺼진 모양에서 회색으로 바뀌는 것들 — 꺼진 모양을 쓰는 버튼에만 연결한다
        [SerializeField] private Image fill;
        [SerializeField] private Graphic[] inkParts;

        private Vector2 _bodyOrigin;
        private Color _fillColor;
        private Color[] _inkColors;
        private bool _isPressed;
        private Button _button;
        private Vector2 _shadowOrigin;
        private bool _isHovered;
        private bool _isDimmed;

        private void Awake()
        {
            if (body == null)
                Debug.LogError($"[{nameof(PressableButton)}] {nameof(body)} 참조가 비어 있습니다.", this);

            _button = GetComponent<Button>();
            _bodyOrigin = body.anchoredPosition;
            if (hardShadow != null)
                _shadowOrigin = hardShadow.anchoredPosition;

            if (fill != null)
                _fillColor = fill.color;

            _inkColors = new Color[inkParts?.Length ?? 0];
            for (int i = 0; i < _inkColors.Length; i++)
                _inkColors[i] = inkParts[i].color;
        }

        private void OnDisable()
        {
            _isPressed = false;
            _isHovered = false;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            Apply();
        }

        /// <summary>누름 — 왼쪽 버튼(터치)만.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _isPressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPressed = false;
            _isHovered = false;
            Apply();
        }

        /// <summary>꺼진 모양(흰 채움 · 회색 테 · 회색 글자) ↔ 원래 모양. 눌리는 것은 그대로다.</summary>
        public void SetDimmed(bool dimmed)
        {
            _isDimmed = dimmed;
            if (fill != null)
                fill.color = dimmed ? Color.white : _fillColor;

            for (int i = 0; i < _inkColors.Length; i++)
                inkParts[i].color = dimmed ? OffColor : _inkColors[i];
        }

        // 비활성(interactable 꺼짐) 버튼은 호버 · 누름에 반응하지 않는다
        private void Apply()
        {
            bool isActive = _button.IsInteractable();
            bool isPressed = _isPressed && isActive;
            bool isHovered = _isHovered && isActive;

            body.anchoredPosition = _bodyOrigin + new Vector2(0f, isPressed ? -pressDepth : isHovered ? hoverLift : 0f);

            // 그림자는 몸체의 자식 — 누를 때 몸체가 내려간 만큼 올려 제자리에 둔다 (아래 7 → 아래 3)
            if (hardShadow != null)
                hardShadow.anchoredPosition = _shadowOrigin + new Vector2(0f, isPressed ? pressDepth : 0f);

            if (fill != null && hoverColor.a > 0f && !_isDimmed)
                fill.color = isPressed || isHovered ? hoverColor : _fillColor;
        }
    }
}
