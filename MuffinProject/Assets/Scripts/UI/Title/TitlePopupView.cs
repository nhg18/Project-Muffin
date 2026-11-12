using System;
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
    }

    /// <summary>
    /// 타이틀 팝업 — 업데이트 필요 · 점검 중(닫을 수 없음) · 게임 종료 확인. 카드 한 장, 닫기 X 없음.
    /// 문구: docs/systems/22-popup-ui.md 3절 「타이틀」. 모양: 「통합 화면 UI v2」 타이틀 .pop
    /// </summary>
    public class TitlePopupView : MonoBehaviour
    {
        private const string UpdateTitle = "업데이트 필요";
        private const string UpdateBody = "새 버전으로 업데이트해 주세요.";
        private const string MaintenanceTitle = "점검 중";
        private const string QuitTitle = "게임 종료";
        private const string QuitBody = "게임을 종료할까요?";

        private static readonly Color BandColor = new Color32(0xCF, 0xEE, 0xDD, 0xFF);
        private static readonly Color WarnBandColor = new Color32(0xFF, 0x9F, 0x1A, 0xFF);

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

        [SerializeField] private Sprite alertIcon;
        [SerializeField] private Sprite clockIcon;
        [SerializeField] private Sprite doorIcon;

        public TitlePopup Current { get; private set; }

        public event Action OnStoreRequested;
        public event Action OnMaintenanceAcknowledged;
        public event Action OnQuitConfirmed;
        public event Action OnQuitCanceled;

        private void Awake()
        {
            if (content == null || frame == null || band == null || bandIcon == null || bandTitle == null || body == null)
                Debug.LogError($"[{nameof(TitlePopupView)}] 팝업 참조가 비어 있습니다.", this);

            if (storeButton == null || okButton == null || quitRow == null || cancelButton == null || quitButton == null)
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
        }

        private void OnDisable()
        {
            storeButton.onClick.RemoveListener(HandleStoreClicked);
            okButton.onClick.RemoveListener(HandleOkClicked);
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
            quitButton.onClick.RemoveListener(HandleQuitClicked);
        }

        /// <summary>업데이트 필요 — 닫을 수 없다, 「스토어로」 (20 7-9)</summary>
        public void ShowUpdate() => Show(TitlePopup.Update, UpdateTitle, UpdateBody, alertIcon, WarnBandColor);

        /// <summary>점검 중 — 닫을 수 없다, 본문은 Remote Config 안내 문구 그대로 (23 7-6)</summary>
        public void ShowMaintenance(string message) => Show(TitlePopup.Maintenance, MaintenanceTitle, message, clockIcon, WarnBandColor);

        public void ShowQuit() => Show(TitlePopup.Quit, QuitTitle, QuitBody, doorIcon, BandColor);

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
        }

        private void HandleStoreClicked() => OnStoreRequested?.Invoke();

        private void HandleOkClicked()
        {
            UIJuice.Pop(frame, 0.94f);
            OnMaintenanceAcknowledged?.Invoke();
        }

        private void HandleCancelClicked() => OnQuitCanceled?.Invoke();

        private void HandleQuitClicked() => OnQuitConfirmed?.Invoke();
    }
}
