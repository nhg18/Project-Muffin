using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 누르고 있는 동안만 켜지는 버튼 — 방 코드 「보기」 · 프로필 친구 코드 · 고유 ID 의 눈 버튼. 떼거나 손가락이 버튼 밖으로 나가거나 창이 포커스를 잃으면 꺼진다.
    /// 기준 문서: docs/systems/16-room-ui-full.md 3절 #8 · 17-lobby-ui-full.md 5-2
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Button _button;

        public bool IsHeld { get; private set; }

        public event Action<bool> OnHeldChanged;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnDisable() => SetHeld(false);

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SetHeld(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _button.IsInteractable())
                SetHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData) => SetHeld(false);

        public void OnPointerExit(PointerEventData eventData) => SetHeld(false);

        /// <summary>누르고 있던 것을 놓은 것으로 친다 — 버튼을 잠글 때.</summary>
        public void Release() => SetHeld(false);

        private void SetHeld(bool held)
        {
            if (IsHeld == held)
                return;

            IsHeld = held;
            OnHeldChanged?.Invoke(held);
        }
    }
}
