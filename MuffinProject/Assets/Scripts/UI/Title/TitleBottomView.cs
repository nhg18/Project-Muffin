using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 로고 아래 바닥 자리 — 「화면을 눌러 시작」 · 자동 로그인 알약 · 자동 로그인 실패 상자가 번갈아 쓴다.
    /// 「화면을 눌러 시작」 일 때만 화면 어디를 눌러도 OnTapped. 기준: 「통합 화면 UI v2」 타이틀 .tap · .cbox
    /// </summary>
    public class TitleBottomView : MonoBehaviour
    {
        [SerializeField] private Button tapCatcher;
        [SerializeField] private CanvasGroup tapPill;
        [SerializeField] private GameObject loadingChip;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private GameObject failBox;
        [SerializeField] private TMP_Text failTitle;
        [SerializeField] private TMP_Text failDescription;
        [SerializeField] private TMP_Text failCode;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button otherButton;

        [Tooltip("「화면을 눌러 시작」 깜빡임 한 번 (초) — 1 → 0.55 → 1")]
        [SerializeField] private float blinkSeconds = 1.6f;

        [Tooltip("깜빡임에서 가장 옅을 때의 불투명도")]
        [SerializeField] private float blinkLowAlpha = 0.55f;

        /// <summary>「화면을 눌러 시작」 을 보이는 중</summary>
        public bool IsWaitingForTap => tapPill.gameObject.activeSelf;

        /// <summary>터치 대기 중 화면을 눌렀다</summary>
        public event Action OnTapped;

        public event Action OnRetryRequested;

        public event Action OnOtherMethodRequested;

        private void Awake()
        {
            if (tapCatcher == null || tapPill == null)
                Debug.LogError($"[{nameof(TitleBottomView)}] 터치 대기 참조가 비어 있습니다.", this);

            if (loadingChip == null || loadingText == null)
                Debug.LogError($"[{nameof(TitleBottomView)}] 로딩 알약 참조가 비어 있습니다.", this);

            if (failBox == null || failTitle == null || failDescription == null || failCode == null || retryButton == null || otherButton == null)
                Debug.LogError($"[{nameof(TitleBottomView)}] 실패 상자 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            tapCatcher.onClick.AddListener(HandleTapClicked);
            retryButton.onClick.AddListener(HandleRetryClicked);
            otherButton.onClick.AddListener(HandleOtherClicked);
        }

        private void OnDisable()
        {
            tapCatcher.onClick.RemoveListener(HandleTapClicked);
            retryButton.onClick.RemoveListener(HandleRetryClicked);
            otherButton.onClick.RemoveListener(HandleOtherClicked);
        }

        private void Update()
        {
            if (!tapPill.gameObject.activeSelf)
                return;

            float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime / blinkSeconds * Mathf.PI * 2f);
            tapPill.alpha = Mathf.Lerp(blinkLowAlpha, 1f, wave);
        }

        public void ShowTap()
        {
            Show(tap: true, loading: false, fail: false);
        }

        public void ShowLoading(string message)
        {
            loadingText.text = message;
            Show(tap: false, loading: true, fail: false);
        }

        /// <summary>실패 사유 + 「다시 시도」. 로그인 실패면 「다른 방법으로」 도 (시안 autofail).</summary>
        public void ShowFail(string title, string description, string code, bool showOtherMethod)
        {
            failTitle.text = title;
            failDescription.text = description;
            failCode.text = code;
            otherButton.gameObject.SetActive(showOtherMethod);
            Show(tap: false, loading: false, fail: true);
        }

        public void Hide()
        {
            Show(tap: false, loading: false, fail: false);
        }

        private void Show(bool tap, bool loading, bool fail)
        {
            tapCatcher.gameObject.SetActive(tap);
            tapPill.gameObject.SetActive(tap);
            loadingChip.SetActive(loading);
            failBox.SetActive(fail);
        }

        private void HandleTapClicked() => OnTapped?.Invoke();

        private void HandleRetryClicked() => OnRetryRequested?.Invoke();

        private void HandleOtherClicked() => OnOtherMethodRequested?.Invoke();
    }
}
