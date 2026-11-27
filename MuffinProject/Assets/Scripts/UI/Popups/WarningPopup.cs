using System;
using Chapchu.UI.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 경고 · 에러 안내. 확인을 누르면 OnConfirmed 를 알린 뒤 스스로 닫힌다. 기준 문서: docs/systems/10-ui.md 8절
    /// 모양은 작은 창 한 규격 (통합 v3.24) — 주황 띠 「!」 + 제목, 본문 첫 문장 굵게 · 나머지 흐리게, 「확인」 판 폭 가득.
    /// </summary>
    public class WarningPopup : Popup
    {
        [Tooltip("띠 제목")]
        [SerializeField] private TMP_Text mainText;
        [Tooltip("본문 첫 문장 — 굵은 줄")]
        [SerializeField] private TMP_Text subText;
        [Tooltip("본문 나머지 — 흐린 줄, 없으면 숨긴다")]
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button okButton;

        /// <summary>제목 (예: "방 참가 실패")</summary>
        public string MainText
        {
            set => mainText.text = value;
        }

        /// <summary>사유 (예: "방을 찾을 수 없어요.") — 첫 문장은 굵은 줄, 나머지는 흐린 줄</summary>
        public string SubText
        {
            set
            {
                (string first, string rest) = LobbyStyle.SplitMessage(value);
                subText.text = first;
                detailText.gameObject.SetActive(rest.Length > 0);
                detailText.text = rest;
            }
        }

        /// <summary>확인 버튼을 누름. 알린 뒤 팝업이 닫힌다.</summary>
        public event Action OnConfirmed;

        private void Awake()
        {
            if (mainText == null)
                Debug.LogError($"[{nameof(WarningPopup)}] {nameof(mainText)} 참조가 비어 있습니다.", this);

            if (subText == null)
                Debug.LogError($"[{nameof(WarningPopup)}] {nameof(subText)} 참조가 비어 있습니다.", this);

            if (detailText == null)
                Debug.LogError($"[{nameof(WarningPopup)}] {nameof(detailText)} 참조가 비어 있습니다.", this);

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
