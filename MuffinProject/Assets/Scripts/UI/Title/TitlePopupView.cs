using System;
using Chapchu.UI.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>타이틀 팝업 종류 (22-popup-ui 3절 「타이틀」)</summary>
    public enum TitlePopup
    {
        None,
        Update,
        Maintenance,
        Quit,
        PlayRecord,
    }

    /// <summary>
    /// 타이틀 팝업 — 업데이트 필요 · 점검 중(닫을 수 없음) · 게임 종료 확인 · 플레이 기록 수집(첫 실행). 카드 한 장, 닫기 X 없음.
    /// 문구: docs/systems/22-popup-ui.md 3절 「타이틀」 · 20-title-ui-full.md 7-12. 모양: 「통합 화면 UI v2」 타이틀 .pop
    /// </summary>
    public class TitlePopupView : MonoBehaviour
    {
        private const string UpdateTitle = "업데이트 필요";
        private const string UpdateBody = "새 버전으로 업데이트해 주세요.";
        private const string MaintenanceTitle = "점검 중";
        private const string QuitTitle = "게임 종료";
        private const string QuitBody = "게임을 종료할까요?";
        private const string PlayRecordTitle = "플레이 기록 수집";
        private const string PlayRecordBody = "더 재미있는 게임을 만들려고 플레이 기록(판 결과 · 사용한 카드 · 화면 이동)을 모아요. 닉네임 · 친구 같은 정보는 모으지 않아요. 설정 → 정보에서 언제든 바꿀 수 있어요.";

        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform frame;
        [SerializeField] private Image band;
        [SerializeField] private Image bandIcon;
        [SerializeField] private TMP_Text bandTitle;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Button storeButton;
        [SerializeField] private Button okButton;
        [SerializeField] private GameObject quitRow;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button quitButton;

        [Tooltip("플레이 기록 수집 — 「동의 안 함」 · 「동의」 가 같은 모양인 줄 (20 7-12)")]
        [SerializeField] private GameObject choiceRow;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button agreeButton;

        [SerializeField] private Sprite alertIcon;
        [SerializeField] private Sprite clockIcon;
        [SerializeField] private Sprite doorIcon;

        public TitlePopup Current { get; private set; }

        public event Action OnStoreRequested;
        public event Action OnMaintenanceAcknowledged;
        public event Action OnQuitConfirmed;
        public event Action OnQuitCanceled;

        /// <summary>플레이 기록 수집에서 고름 — 참 = 동의 (20 7-12)</summary>
        public event Action<bool> OnPlayRecordChosen;

        private void Awake()
        {
            if (content == null || frame == null || band == null || bandIcon == null || bandTitle == null || body == null)
                Debug.LogError($"[{nameof(TitlePopupView)}] 팝업 참조가 비어 있습니다.", this);

            if (storeButton == null || okButton == null || quitRow == null || cancelButton == null || quitButton == null
                || choiceRow == null || declineButton == null || agreeButton == null)
                Debug.LogError($"[{nameof(TitlePopupView)}] 버튼 참조가 비어 있습니다.", this);

            if (alertIcon == null || clockIcon == null || doorIcon == null)
                Debug.LogError($"[{nameof(TitlePopupView)}] 아이콘 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            storeButton.onClick.AddListener(HandleStoreClicked);
            okButton.onClick.AddListener(HandleOkClicked);
            cancelButton.onClick.AddListener(HandleCancelClicked);
            quitButton.onClick.AddListener(HandleQuitClicked);
            declineButton.onClick.AddListener(HandleDeclineClicked);
            agreeButton.onClick.AddListener(HandleAgreeClicked);
        }

        private void OnDisable()
        {
            storeButton.onClick.RemoveListener(HandleStoreClicked);
            okButton.onClick.RemoveListener(HandleOkClicked);
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
            quitButton.onClick.RemoveListener(HandleQuitClicked);
            declineButton.onClick.RemoveListener(HandleDeclineClicked);
            agreeButton.onClick.RemoveListener(HandleAgreeClicked);
        }

        /// <summary>업데이트 필요 — 닫을 수 없다, 「스토어로」 (20 7-9)</summary>
        public void ShowUpdate() => Show(TitlePopup.Update, UpdateTitle, UpdateBody, alertIcon, LobbyStyle.Warn);

        /// <summary>점검 중 — 닫을 수 없다, 본문은 Remote Config 안내 문구 그대로 (23 7-6)</summary>
        public void ShowMaintenance(string message) => Show(TitlePopup.Maintenance, MaintenanceTitle, message, alertIcon, LobbyStyle.Warn);

        public void ShowQuit() => Show(TitlePopup.Quit, QuitTitle, QuitBody, doorIcon, LobbyStyle.Band);

        /// <summary>플레이 기록 수집 — 닫을 수 없다, 「동의 안 함」 · 「동의」 중 하나 (20 7-12)</summary>
        public void ShowPlayRecord() => Show(TitlePopup.PlayRecord, PlayRecordTitle, PlayRecordBody, alertIcon, LobbyStyle.Band);

        public void Hide()
        {
            Current = TitlePopup.None;
            content.SetActive(false);
        }

        private void Show(TitlePopup kind, string title, string text, Sprite icon, Color bandColor)
        {
            Current = kind;
            content.SetActive(true);
            band.color = bandColor;
            bandIcon.sprite = icon;
            bandTitle.text = title;
            body.text = text;
            storeButton.gameObject.SetActive(kind == TitlePopup.Update);
            okButton.gameObject.SetActive(kind == TitlePopup.Maintenance);
            quitRow.SetActive(kind == TitlePopup.Quit);
            choiceRow.SetActive(kind == TitlePopup.PlayRecord);
        }

        private void HandleStoreClicked() => OnStoreRequested?.Invoke();

        private void HandleOkClicked()
        {
            UIJuice.Pop(frame, 0.94f);
            OnMaintenanceAcknowledged?.Invoke();
        }

        private void HandleCancelClicked() => OnQuitCanceled?.Invoke();

        private void HandleQuitClicked() => OnQuitConfirmed?.Invoke();

        private void HandleDeclineClicked() => OnPlayRecordChosen?.Invoke(false);

        private void HandleAgreeClicked() => OnPlayRecordChosen?.Invoke(true);
    }
}
