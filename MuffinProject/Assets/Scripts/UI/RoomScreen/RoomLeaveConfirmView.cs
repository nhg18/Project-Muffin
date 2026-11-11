using System;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 나가기 확인 판 — 「방을 나갈까요?」 + (방장이면) 위임 안내 + 「나가기」 「취소」. 바탕을 눌러도 닫히지 않는다.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-8 · 8절, docs/systems/22-popup-ui.md 1절 #4
    /// </summary>
    public class RoomLeaveConfirmView : MonoBehaviour
    {
        [Tooltip("가림막 + 판 — 닫혀 있으면 꺼 둔다")]
        [SerializeField] private GameObject content;

        [Tooltip("「방장은 다음 사람에게 넘어가요.」 — 방장에게만")]
        [SerializeField] private GameObject hostNote;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button cancelButton;

        public bool IsOpen => content.activeSelf;

        /// <summary>확인 판에서 「나가기」 를 눌렀다.</summary>
        public event Action OnLeaveConfirmed;

        private void Awake()
        {
            if (content == null || hostNote == null || leaveButton == null || cancelButton == null)
                Debug.LogError($"[{nameof(RoomLeaveConfirmView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            leaveButton.onClick.AddListener(HandleLeaveClicked);
            cancelButton.onClick.AddListener(HandleCancelClicked);
        }

        private void OnDisable()
        {
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        public void Show(bool isHost)
        {
            hostNote.SetActive(isHost);
            content.SetActive(true);
        }

        public void Hide() => content.SetActive(false);

        private void HandleLeaveClicked()
        {
            Hide();
            OnLeaveConfirmed?.Invoke();
        }

        private void HandleCancelClicked() => Hide();
    }
}
