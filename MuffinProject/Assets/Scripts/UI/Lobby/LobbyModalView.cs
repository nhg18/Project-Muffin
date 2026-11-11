using System;
using Chapchu.Core;
using Chapchu.UI.Title;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>입력 창 종류 — 방 코드 · 새 닉네임 · 계정 삭제 확인(「삭제」 입력)</summary>
    public enum LobbyInputKind { RoomCode, Nickname, DeleteAccount }

    /// <summary>
    /// 로비의 위층 창 — 입력 창(방 코드 · 닉네임 · 계정 삭제) · 경고 / 확인 창(주황 머리 띠) · 요청 중 표시. 다른 창 위에 뜬다.
    /// 기준: 「통합 화면 UI v2」 로비 modalHTML · .loadbox, docs/systems/22-popup-ui.md
    /// </summary>
    public class LobbyModalView : MonoBehaviour
    {
        private const string CodeTitle = "방 코드 입력";
        private const string CodePlaceholder = "방 코드";
        private const string CodeSubmit = "참가";
        private const string NicknameTitle = "새 닉네임";
        private const string NicknamePlaceholder = "닉네임";
        private const string NicknameSubmit = "변경";
        private const string DeleteTitle = "계정 삭제";
        private const string DeletePlaceholder = "삭제";
        private const string DeleteSubmit = "삭제";
        private const string DeleteMessage = "'삭제' 를 입력하면 계정이 지워져요.";
        private const string CountFormat = "{0} / {1}";
        private const string DefaultOkText = "확인";

        [SerializeField] private GameObject dimmer;

        [Header("입력 창")]
        [SerializeField] private GameObject inputContent;
        [SerializeField] private Image inputBand;
        [SerializeField] private Image inputIcon;
        [SerializeField] private TMP_Text inputTitle;
        [SerializeField] private Button inputCloseButton;
        [SerializeField] private TMP_Text inputMessage;
        [SerializeField] private TMP_InputField field;
        [SerializeField] private TMP_Text fieldPlaceholder;
        [SerializeField] private RoundBox fieldBox;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button submitButton;
        [SerializeField] private RoundBox submitBox;
        [SerializeField] private TMP_Text submitLabel;
        [SerializeField] private Sprite keyIcon;
        [SerializeField] private Sprite userIcon;
        [SerializeField] private Sprite crossIcon;

        [Tooltip("방 코드 입력 글자 크기 · 글자 사이 (em × 100) — 닉네임 · 삭제 입력은 보통 크기")]
        [SerializeField] private float codeFontSize = 52f;
        [SerializeField] private float codeSpacing = 30f;
        [SerializeField] private float textFontSize = 34f;

        [Tooltip("닉네임 입력에서 글자 수 표시가 차지하는 오른쪽 자리 (캔버스 px)")]
        [SerializeField] private float countInset = 170f;

        [Header("경고 · 확인 창")]
        [SerializeField] private GameObject warnContent;
        [SerializeField] private TMP_Text warnTitle;
        [SerializeField] private TMP_Text warnMessage;
        [SerializeField] private Button warnCancelButton;
        [SerializeField] private TMP_Text warnCancelLabel;
        [SerializeField] private Button warnOkButton;
        [SerializeField] private TMP_Text warnOkLabel;

        [Header("요청 중")]
        [SerializeField] private GameObject loadingContent;
        [SerializeField] private TMP_Text loadingText;

        private Action _onWarnOk;

        public LobbyInputKind InputKind { get; private set; }

        public bool IsInputOpen => inputContent.activeSelf;

        public bool IsWarnOpen => warnContent.activeSelf;

        public bool IsLoading => loadingContent.activeSelf;

        public bool IsAnyOpen => IsInputOpen || IsWarnOpen || IsLoading;

        /// <summary>입력 창에서 「참가」 · 「변경」 · 「삭제」 를 눌렀다 — 앞뒤 공백을 자른 입력 값.</summary>
        public event Action<LobbyInputKind, string> OnInputSubmitted;

        private void Awake()
        {
            if (dimmer == null || inputContent == null || inputBand == null || inputIcon == null || inputTitle == null || inputCloseButton == null
                || inputMessage == null || field == null || fieldPlaceholder == null || fieldBox == null || countText == null || errorText == null
                || submitButton == null || submitBox == null || submitLabel == null || keyIcon == null || userIcon == null || crossIcon == null)
                Debug.LogError($"[{nameof(LobbyModalView)}] 입력 창 참조가 비어 있습니다.", this);

            if (warnContent == null || warnTitle == null || warnMessage == null || warnCancelButton == null || warnCancelLabel == null
                || warnOkButton == null || warnOkLabel == null || loadingContent == null || loadingText == null)
                Debug.LogError($"[{nameof(LobbyModalView)}] 경고 창 · 요청 중 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            inputCloseButton.onClick.AddListener(HandleInputCloseClicked);
            submitButton.onClick.AddListener(HandleSubmitClicked);
            field.onSubmit.AddListener(HandleFieldSubmitted);
            field.onValueChanged.AddListener(HandleFieldChanged);
            warnCancelButton.onClick.AddListener(HandleWarnCancelClicked);
            warnOkButton.onClick.AddListener(HandleWarnOkClicked);
        }

        private void OnDisable()
        {
            inputCloseButton.onClick.RemoveListener(HandleInputCloseClicked);
            submitButton.onClick.RemoveListener(HandleSubmitClicked);
            field.onSubmit.RemoveListener(HandleFieldSubmitted);
            field.onValueChanged.RemoveListener(HandleFieldChanged);
            warnCancelButton.onClick.RemoveListener(HandleWarnCancelClicked);
            warnOkButton.onClick.RemoveListener(HandleWarnOkClicked);
        }

        #region 입력 창

        public void ShowInput(LobbyInputKind kind, string value = "")
        {
            InputKind = kind;
            bool isCode = kind == LobbyInputKind.RoomCode;
            bool isDelete = kind == LobbyInputKind.DeleteAccount;

            inputContent.SetActive(true);
            inputBand.color = isDelete ? LobbyStyle.Hp : LobbyStyle.Band;
            inputIcon.sprite = isCode ? keyIcon : isDelete ? crossIcon : userIcon;
            inputTitle.text = isCode ? CodeTitle : isDelete ? DeleteTitle : NicknameTitle;
            inputMessage.gameObject.SetActive(isDelete);
            inputMessage.text = DeleteMessage;
            fieldPlaceholder.text = isCode ? CodePlaceholder : isDelete ? DeletePlaceholder : NicknamePlaceholder;
            submitLabel.text = isCode ? CodeSubmit : isDelete ? DeleteSubmit : NicknameSubmit;
            submitBox.FillColor = isDelete ? LobbyStyle.WarnFill : LobbyStyle.Turn;

            // 방 코드 — 가운데 · 대문자 · 글자 사이를 벌린다
            field.characterLimit = isCode ? RandomCode.Length : kind == LobbyInputKind.Nickname ? NicknameValidator.MaxLength : 0;
            field.textComponent.alignment = isCode ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            field.textComponent.fontSize = isCode ? codeFontSize : textFontSize;
            field.textComponent.characterSpacing = isCode ? codeSpacing : 0f;
            fieldPlaceholder.alignment = isCode ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            countText.gameObject.SetActive(kind == LobbyInputKind.Nickname);

            // 닉네임만 글자 수 표시 자리를 비운다 — 나머지는 왼쪽과 같은 여백 (방 코드가 한가운데 오게)
            RectTransform viewport = field.textViewport;
            viewport.offsetMax = new Vector2(kind == LobbyInputKind.Nickname ? -countInset : -viewport.offsetMin.x, viewport.offsetMax.y);

            field.SetTextWithoutNotify(value);
            UpdateCount();
            SetInputError(string.Empty);
            UpdateDimmer();
            field.ActivateInputField();
        }

        /// <summary>입력 칸 아래 빨간 한 줄 + 빨간 테. 빈 글이면 지운다.</summary>
        public void SetInputError(string message)
        {
            errorText.text = message;
            fieldBox.BorderColor = string.IsNullOrEmpty(message) ? LobbyStyle.Ink : LobbyStyle.Hp;
        }

        public void HideInput()
        {
            inputContent.SetActive(false);
            UpdateDimmer();
        }

        #endregion

        #region 경고 · 확인 · 요청 중

        /// <summary>
        /// 경고 / 확인 창. cancelLabel 이 null 이면 버튼 하나(확인). 「확인」 쪽을 누르면 창을 닫고 onOk 를 부른다 — 취소는 닫기만.
        /// </summary>
        public void ShowWarn(string title, string message, string okLabel = null, string cancelLabel = null, Action onOk = null)
        {
            _onWarnOk = onOk;
            warnContent.SetActive(true);
            warnTitle.text = title;
            warnMessage.text = KeepWords(message);
            warnOkLabel.text = okLabel ?? DefaultOkText;
            warnCancelButton.gameObject.SetActive(cancelLabel != null);
            warnCancelLabel.text = cancelLabel;
            UpdateDimmer();
        }

        // 줄을 띄어쓰기에서만 바꾼다 (시안 word-break: keep-all) — TMP 는 한글을 낱말 중간에서도 꺾는다
        private static string KeepWords(string text)
            => "<nobr>" + text.Replace(" ", "</nobr> <nobr>").Replace("\n", "</nobr>\n<nobr>") + "</nobr>";

        public void HideWarn()
        {
            _onWarnOk = null;
            warnContent.SetActive(false);
            UpdateDimmer();
        }

        public void ShowLoading(string text)
        {
            loadingContent.SetActive(true);
            loadingText.text = text;
            UpdateDimmer();
        }

        public void HideLoading()
        {
            loadingContent.SetActive(false);
            UpdateDimmer();
        }

        #endregion

        private void UpdateDimmer() => dimmer.SetActive(IsAnyOpen);

        private void UpdateCount()
            => countText.text = string.Format(CountFormat, field.text.Length, NicknameValidator.MaxLength);

        private void Submit()
        {
            string value = field.text.Trim();
            OnInputSubmitted?.Invoke(InputKind, InputKind == LobbyInputKind.RoomCode ? value.ToUpperInvariant() : value);
        }

        private void HandleSubmitClicked() => Submit();

        private void HandleFieldSubmitted(string text) => Submit();

        private void HandleFieldChanged(string text)
        {
            if (InputKind == LobbyInputKind.RoomCode && text != text.ToUpperInvariant())
                field.SetTextWithoutNotify(text.ToUpperInvariant());

            UpdateCount();
            SetInputError(string.Empty);
        }

        private void HandleInputCloseClicked() => HideInput();

        private void HandleWarnCancelClicked() => HideWarn();

        private void HandleWarnOkClicked()
        {
            Action onOk = _onWarnOk;
            HideWarn();
            onOk?.Invoke();
        }
    }
}
