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
    public class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private static readonly Color OffColor = new Color32(0xB8, 0xAE, 0xA4, 0xFF);

        [SerializeField] private RectTransform body;

        [Tooltip("누를 때 몸체가 내려가는 거리 (캔버스 px). 시안 .btn 2 × 2 · 큰 버튼 3 × 2")]
        [SerializeField] private float pressDepth = 4f;

        // 꺼진 모양에서 회색으로 바뀌는 것들 — 꺼진 모양을 쓰는 버튼에만 연결한다
        [SerializeField] private Image fill;
        [SerializeField] private Graphic[] inkParts;

        private Vector2 _bodyOrigin;
        private Color _fillColor;
        private Color[] _inkColors;
        private bool _isPressed;

        private void Awake()
        {
            if (body == null)
                Debug.LogError($"[{nameof(PressableButton)}] {nameof(body)} 참조가 비어 있습니다.", this);

            _bodyOrigin = body.anchoredPosition;
            if (fill != null)
                _fillColor = fill.color;

            _inkColors = new Color[inkParts?.Length ?? 0];
            for (int i = 0; i < _inkColors.Length; i++)
                _inkColors[i] = inkParts[i].color;
        }

        private void OnDisable()
        {
            _isPressed = false;
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
            Apply();
        }

        /// <summary>꺼진 모양(흰 채움 · 회색 테 · 회색 글자) ↔ 원래 모양. 눌리는 것은 그대로다.</summary>
        public void SetDimmed(bool dimmed)
        {
            if (fill != null)
                fill.color = dimmed ? Color.white : _fillColor;

            for (int i = 0; i < _inkColors.Length; i++)
                inkParts[i].color = dimmed ? OffColor : _inkColors[i];
        }

        private void Apply()
        {
            body.anchoredPosition = _bodyOrigin + new Vector2(0f, _isPressed ? -pressDepth : 0f);
        }
    }
}
