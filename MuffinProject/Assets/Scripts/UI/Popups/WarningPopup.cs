using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 경고 · 에러 안내. 확인을 누르면 OnConfirmed 를 알린 뒤 스스로 닫힌다. 기준 문서: docs/systems/10-ui.md 8절
    /// </summary>
    public class WarningPopup : Popup
    {
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private TMP_Text subText;
        [SerializeField] private Button okButton;

        /// <summary>제목 (예: "방 참가 실패")</summary>
        public string MainText
        {
            set => mainText.text = value;
        }

        /// <summary>사유 (예: "방을 찾을 수 없습니다.")</summary>
        public string SubText
        {
            set => subText.text = value;
        }

        /// <summary>확인 버튼을 누름. 알린 뒤 팝업이 닫힌다.</summary>
        public event Action OnConfirmed;

        private void Awake()
        {
            if (mainText == null)
                Debug.LogError($"[{nameof(WarningPopup)}] {nameof(mainText)} 참조가 비어 있습니다.", this);

            if (subText == null)
                Debug.LogError($"[{nameof(WarningPopup)}] {nameof(subText)} 참조가 비어 있습니다.", this);

            if (okButton == null)
                Debug.LogError($"[{nameof(WarningPopup)}] {nameof(okButton)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            okButton.onClick.AddListener(HandleOkClicked);
        }

        private void OnDisable()
        {
            okButton.onClick.RemoveListener(HandleOkClicked);
        }

        private void HandleOkClicked()
        {
            OnConfirmed?.Invoke();
            Close();
        }
    }
}
