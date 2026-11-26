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

    /// <summary>경고 · 확인 창의 종류 — 머리 띠 색 · 띠 아이콘 · 주 버튼 색 (시안 v3.24 .pu)</summary>
    public enum LobbyWarnKind
    {
        /// <summary>알림 · 실패 — 주황 띠 「!」</summary>
        Warn,
        /// <summary>되묻기 — 민트 띠 「?」</summary>
        Ask,
        /// <summary>되돌릴 수 없는 일(차단 · 계정 삭제) — 빨강 띠 「!」 · 빨강 주 버튼</summary>
        Danger,
    }

    /// <summary>
    /// 로비의 위층 창 — 입력 창(방 코드 · 닉네임 · 계정 삭제) · 경고 / 확인 창(머리 띠 색 = 종류) · 요청 중 표시. 다른 창 위에 뜬다.
    /// 기준: 「통합 화면 UI v3」 로비 modalHTML(.pu) · .loadbox, docs/systems/22-popup-ui.md
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
        private const string DeleteMessage = "「삭제」를 입력하면 계정이 지워져요.";
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
        [SerializeField] private LayoutElement fieldSize;
        [SerializeField] private TMP_Text fieldPlaceholder;
        [SerializeField] private RoundBox fieldBox;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button submitButton;
        [SerializeField] private RoundBox submitBox;
        [SerializeField] private TMP_Text submitLabel;
        [SerializeField] private Sprite keyIcon;
        [SerializeField] private Sprite userIcon;

        [Tooltip("방 코드 입력칸 높이 (캔버스 px)")]
        [SerializeField] private float codeFieldHeight = 104f;

        [Tooltip("닉네임 · 삭제 입력칸 높이 (캔버스 px)")]
        [SerializeField] private float textFieldHeight = 88f;

        [Tooltip("방 코드 입력칸의 글자 크기")]
        [SerializeField] private float codeFontSize = 52f;

        [Tooltip("방 코드 입력칸의 글자 사이 (em × 100)")]
        [SerializeField] private float codeSpacing = 30f;

        [Tooltip("닉네임 · 삭제 입력칸의 글자 크기")]
        [SerializeField] private float textFontSize = 34f;

        [Tooltip("닉네임 입력에서 글자 수 표시가 차지하는 오른쪽 자리 (캔버스 px)")]
        [SerializeField] private float countInset = 170f;

        [Header("경고 · 확인 창")]
        [SerializeField] private GameObject warnContent;
        [Tooltip("머리 띠 — 색이 종류를 말한다 (경고 주황 · 확인 민트 · 위험 빨강).")]
        [SerializeField] private Image warnBand;
        [SerializeField] private Image warnIcon;
        [SerializeField] private TMP_Text warnTitle;
        [Tooltip("본문 첫 문장 — 굵게.")]
        [SerializeField] private TMP_Text warnMessage;
        [Tooltip("본문 나머지 — 흐린 둘째 줄. 없으면 숨긴다.")]
        [SerializeField] private TMP_Text warnDetail;
        [SerializeField] private Button warnCancelButton;
        [SerializeField] private TMP_Text warnCancelLabel;
        [SerializeField] private Button warnOkButton;
        [SerializeField] private RoundBox warnOkBox;
        [SerializeField] private TMP_Text warnOkLabel;
        [Tooltip("경고 · 위험 띠 아이콘 「!」.")]
        [SerializeField] private Sprite alertIcon;
        [Tooltip("확인 띠 아이콘 「?」.")]
        [SerializeField] private Sprite askIcon;

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
                || inputMessage == null || field == null || fieldSize == null || fieldPlaceholder == null || fieldBox == null || countText == null || errorText == null
                || submitButton == null || submitBox == null || submitLabel == null || keyIcon == null || userIcon == null)
                Debug.LogError($"[{nameof(LobbyModalView)}] 입력 창 참조가 비어 있습니다.", this);

            if (warnContent == null || warnBand == null || warnIcon == null || warnTitle == null || warnMessage == null || warnDetail == null
                || warnCancelButton == null || warnCancelLabel == null || warnOkButton == null || warnOkBox == null || warnOkLabel == null
                || alertIcon == null || askIcon == null || loadingContent == null || loadingText == null)
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

        /// <summary>입력 창. message 는 입력 칸 위 안내 줄 — 계정 삭제는 늘 정해진 줄, 그 밖은 주면 보인다 (닉네임 변경 쿠폰 줄, 17 5-2).</summary>
        public void ShowInput(LobbyInputKind kind, string value = "", string message = null)
        {
            InputKind = kind;
            bool isCode = kind == LobbyInputKind.RoomCode;
            bool isDelete = kind == LobbyInputKind.DeleteAccount;

            inputContent.SetActive(true);
            inputBand.color = isDelete ? LobbyStyle.Hp : LobbyStyle.Band;
            inputIcon.sprite = isCode ? keyIcon : isDelete ? alertIcon : userIcon; // 계정 삭제 = 위험 창과 같은 「!」
            inputTitle.text = isCode ? CodeTitle : isDelete ? DeleteTitle : NicknameTitle;
            string note = isDelete ? DeleteMessage : message;
            inputMessage.gameObject.SetActive(!string.IsNullOrEmpty(note));
            inputMessage.text = note;
            fieldPlaceholder.text = isCode ? CodePlaceholder : isDelete ? DeletePlaceholder : NicknamePlaceholder;
            submitLabel.text = isCode ? CodeSubmit : isDelete ? DeleteSubmit : NicknameSubmit;
            submitLabel.color = isDelete ? Color.white : LobbyStyle.Ink;
            submitBox.FillColor = isDelete ? LobbyStyle.Hp : LobbyStyle.Turn;

            // 방 코드 — 가운데 · 대문자 · 글자 사이를 벌린다 · 칸이 더 높다
            fieldSize.minHeight = fieldSize.preferredHeight = isCode ? codeFieldHeight : textFieldHeight;
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
        /// 경고 / 확인 창. cancelLabel 이 null 이면 버튼 하나(확인). 오른쪽 주 버튼을 누르면 창을 닫고 onOk 를 부른다 — 왼쪽(취소 · 닫기)은 닫기만.
        /// 본문은 첫 문장이 굵은 첫 줄, 나머지가 흐린 둘째 줄이 된다 (시안 .pu p.big + p).
        /// </summary>
        public void ShowWarn(string title, string message, string okLabel = null, string cancelLabel = null, Action onOk = null,
            LobbyWarnKind kind = LobbyWarnKind.Warn)
        {
            _onWarnOk = onOk;
            warnContent.SetActive(true);
            warnBand.color = kind switch
            {
                LobbyWarnKind.Ask => LobbyStyle.Band,
                LobbyWarnKind.Danger => LobbyStyle.Hp,
                _ => LobbyStyle.Warn,
            };
            warnIcon.sprite = kind == LobbyWarnKind.Ask ? askIcon : alertIcon;
            warnTitle.text = title;

            (string first, string rest) = LobbyStyle.SplitMessage(message);
            warnMessage.text = first;
            warnDetail.gameObject.SetActive(rest.Length > 0);
            warnDetail.text = rest;

            bool isDanger = kind == LobbyWarnKind.Danger;
            warnOkBox.FillColor = isDanger ? LobbyStyle.Hp : LobbyStyle.Turn;
            warnOkLabel.color = isDanger ? Color.white : LobbyStyle.Ink;
            warnOkLabel.text = okLabel ?? DefaultOkText;
            warnCancelButton.gameObject.SetActive(cancelLabel != null);
            warnCancelLabel.text = cancelLabel;
            UpdateDimmer();
        }

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
