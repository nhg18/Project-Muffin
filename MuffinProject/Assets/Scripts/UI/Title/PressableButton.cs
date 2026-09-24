using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 호버 시 위로 뜨고, 누르면 그림자 쪽으로 가라앉는 버튼 연출 (12-title-ui.md 10-1).
    /// Selectable 의 Color Tint 로는 위치를 못 옮겨서 포인터 이벤트로 직접 처리한다. 모바일에선 누름만 체감된다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PressableButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform body;
        [SerializeField] private RectTransform hardShadow;
        [SerializeField] private Graphic fill;
        [SerializeField] private Color normalColor = new Color32(0xB1, 0x8A, 0xE0, 0xFF);
        [SerializeField] private Color hoverColor = new Color32(0xBF, 0x9C, 0xE9, 0xFF);
        [SerializeField] private float hoverLift = 2f;
        // 누르면 몸체는 이만큼 내려가고 그림자는 이만큼 올라와 둘이 맞닿는다 (그림자 아래 7 → 3).
        [SerializeField] private float pressDepth = 4f;

        private Button _button;
        private Vector2 _bodyOrigin;
        private Vector2 _shadowOrigin;
        private bool _isHovered;
        private bool _isPressed;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _bodyOrigin = body.anchoredPosition;
            _shadowOrigin = hardShadow.anchoredPosition;
        }

        private void OnDisable()
        {
            _isHovered = false;
            _isPressed = false;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _isPressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _isPressed = false;
            Apply();
        }

        private void Apply()
        {
            bool isActive = _button.IsInteractable();
            bool isPressed = isActive && _isPressed;
            bool isHovered = isActive && _isHovered;

            float bodyOffset = isPressed ? -pressDepth : isHovered ? hoverLift : 0f;
            body.anchoredPosition = _bodyOrigin + new Vector2(0f, bodyOffset);
            hardShadow.anchoredPosition = _shadowOrigin + new Vector2(0f, isPressed ? pressDepth : 0f);
            fill.color = isPressed || isHovered ? hoverColor : normalColor;
        }
    }
}
