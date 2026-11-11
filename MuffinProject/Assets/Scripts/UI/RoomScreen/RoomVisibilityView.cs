using System;
using Chapchu.UI.Lobby;
using UnityEngine;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 「게임 시작」 위의 공개 여부 상자 — 「공개」 / 「비공개」 글자는 전원에게, 토글은 방장에게만 보인다.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-1, docs/systems/08-room.md 5절 #7
    /// </summary>
    public class RoomVisibilityView : MonoBehaviour
    {
        [SerializeField] private GameObject publicTag;
        [SerializeField] private GameObject privateTag;
        [SerializeField] private LobbySwitchView toggle;

        [Tooltip("토글을 흐리게 · 눌리지 않게 하는 묶음 — 시작 중")]
        [SerializeField] private CanvasGroup toggleGroup;

        [Tooltip("잠긴 토글의 알파 (시안 .sw:disabled)")]
        [SerializeField] private float lockedAlpha = 0.45f;

        /// <summary>방장이 토글을 눌렀다 — 바꾸려는 값 (true = 공개).</summary>
        public event Action<bool> OnChangeRequested;

        private void Awake()
        {
            if (publicTag == null)
                Debug.LogError($"[{nameof(RoomVisibilityView)}] {nameof(publicTag)} 참조가 비어 있습니다.", this);

            if (privateTag == null)
                Debug.LogError($"[{nameof(RoomVisibilityView)}] {nameof(privateTag)} 참조가 비어 있습니다.", this);

            if (toggle == null)
                Debug.LogError($"[{nameof(RoomVisibilityView)}] {nameof(toggle)} 참조가 비어 있습니다.", this);

            if (toggleGroup == null)
                Debug.LogError($"[{nameof(RoomVisibilityView)}] {nameof(toggleGroup)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => toggle.OnChanged += HandleToggleChanged;

        private void OnDisable() => toggle.OnChanged -= HandleToggleChanged;

        public void Set(bool isPublic, bool isHost, bool isLocked)
        {
            publicTag.SetActive(isPublic);
            privateTag.SetActive(!isPublic);

            toggle.gameObject.SetActive(isHost);

            // 방장이 방금 누른 토글은 이미 그 값이다 — 다시 맞추면 손잡이가 움직이다 멈춘다
            if (toggle.IsOn != isPublic)
                toggle.Set(isPublic);

            toggleGroup.alpha = isLocked ? lockedAlpha : 1f;
            toggleGroup.interactable = !isLocked;
            toggleGroup.blocksRaycasts = !isLocked;
        }

        private void HandleToggleChanged(LobbySwitchView changed) => OnChangeRequested?.Invoke(changed.IsOn);
    }
}
