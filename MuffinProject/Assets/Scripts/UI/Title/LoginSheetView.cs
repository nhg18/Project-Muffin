using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>로그인 창 안의 단계 (시안 T.step)</summary>
    public enum LoginSheetStep
    {
        Agree,
        Login,
        Loading,
        Nickname,
        Fail,
    }

    /// <summary>
    /// 로그인 창 — 가림판 + 카드 한 장(머리 띠). 약관 동의 · 로그인 고르기 · 로딩 · 닉네임 · 실패가 같은 창을 번갈아 쓴다.
    /// 바탕을 눌러도 닫히지 않는다 — 닫기 X · 뒤로가기만 (22-popup-ui 1절). 기준: 「통합 화면 UI v2」 타이틀 .lsheet
    /// </summary>
    public class LoginSheetView : MonoBehaviour
    {
        private const string StartTitle = "시작하기";
        private const string AgreeTitle = "약관 동의";
        private const string NicknameTitle = "닉네임 정하기";
        private const string LoginFailTitle = "로그인 실패";
        private const string ConnectionFailTitle = "연결 실패";

        private static readonly Color BandColor = new Color32(0xCF, 0xEE, 0xDD, 0xFF);
        private static readonly Color BadBandColor = new Color32(0xEC, 0x3B, 0x46, 0xFF);

        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform frame;
        [SerializeField] private Image band;
        [SerializeField] private Image bandIcon;
        [SerializeField] private TMP_Text bandTitle;
        [SerializeField] private Button closeButton;

        [Header("단계")]
        [SerializeField] private TermsAgreeView agreeBody;
        [SerializeField] private GameObject loginBody;
        [SerializeField] private GameObject loadingBody;
        [SerializeField] private GameObject nicknameBody;
        [SerializeField] private GameObject failBody;

        [Header("로그인 고르기")]
        [SerializeField] private Button guestButton;
        [SerializeField] private Button googleButton;
        [SerializeField] private Button appleButton;

        [Header("로딩 · 실패")]
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private TMP_Text failTitle;
        [SerializeField] private TMP_Text failDescription;
        [SerializeField] private TMP_Text failCode;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button otherButton;

        [Header("닉네임")]
        [SerializeField] private NicknameFieldView nicknameField;
        [SerializeField] private Button diceButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button startButton;

        [Header("머리 띠 아이콘")]
        [SerializeField] private Sprite keyIcon;
        [SerializeField] private Sprite listIcon;
        [SerializeField] private Sprite userIcon;
        [SerializeField] private Sprite alertIcon;

        [Header("창 자리 (가운데 1920 × 1080 묶음 위에서 아래로)")]
        [Tooltip("약관 동의 창 위쪽 · 폭 (시안 40 · 640 × 2)")]
        [SerializeField] private Vector2 agreeTopAndWidth = new Vector2(80f, 1280f);
        [Tooltip("로그인 고르기 창 위쪽 · 폭 (시안 140 · 420 × 2)")]
        [SerializeField] private Vector2 loginTopAndWidth = new Vector2(280f, 840f);
        [Tooltip("그 밖의 창 위쪽 · 폭 (시안 150 · 420 × 2)")]
        [SerializeField] private Vector2 otherTopAndWidth = new Vector2(300f, 840f);

        public bool IsOpen => content.activeSelf;

        public LoginSheetStep Step { get; private set; }

        /// <summary>실패 단계가 로그인 실패인지 (아니면 연결 실패)</summary>
        public bool IsLoginFailure { get; private set; }

        /// <summary>닉네임 입력값 (앞뒤 공백 포함)</summary>
        public string NicknameText => nicknameField.Text;

        public event Action OnCloseRequested;
        public event Action OnAgreed;
        public event Action OnGuestRequested;
        public event Action OnGoogleRequested;
        public event Action OnAppleRequested;
        public event Action OnDiceRequested;
        public event Action OnStartRequested;
        public event Action OnBackRequested;
        public event Action OnRetryRequested;
        public event Action OnOtherMethodRequested;

        private void Awake()
        {
            if (content == null || frame == null || band == null || bandIcon == null || bandTitle == null || closeButton == null)
                Debug.LogError($"[{nameof(LoginSheetView)}] 창 참조가 비어 있습니다.", this);

            if (agreeBody == null || loginBody == null || loadingBody == null || nicknameBody == null || failBody == null)
                Debug.LogError($"[{nameof(LoginSheetView)}] 단계 참조가 비어 있습니다.", this);

            if (guestButton == null || googleButton == null || appleButton == null)
                Debug.LogError($"[{nameof(LoginSheetView)}] 로그인 버튼 참조가 비어 있습니다.", this);

            if (loadingText == null || failTitle == null || failDescription == null || failCode == null || retryButton == null || otherButton == null)
                Debug.LogError($"[{nameof(LoginSheetView)}] 로딩 · 실패 참조가 비어 있습니다.", this);

            if (nicknameField == null || diceButton == null || backButton == null || startButton == null)
                Debug.LogError($"[{nameof(LoginSheetView)}] 닉네임 참조가 비어 있습니다.", this);

            if (keyIcon == null || listIcon == null || userIcon == null || alertIcon == null)
                Debug.LogError($"[{nameof(LoginSheetView)}] 머리 띠 아이콘 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            agreeBody.OnAgreed += HandleAgreed;
            guestButton.onClick.AddListener(HandleGuestClicked);
            googleButton.onClick.AddListener(HandleGoogleClicked);
            appleButton.onClick.AddListener(HandleAppleClicked);
            diceButton.onClick.AddListener(HandleDiceClicked);
            backButton.onClick.AddListener(HandleBackClicked);
            startButton.onClick.AddListener(HandleStartClicked);
            nicknameField.OnSubmitted += HandleStartClicked;
            retryButton.onClick.AddListener(HandleRetryClicked);
            otherButton.onClick.AddListener(HandleOtherClicked);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            agreeBody.OnAgreed -= HandleAgreed;
            guestButton.onClick.RemoveListener(HandleGuestClicked);
            googleButton.onClick.RemoveListener(HandleGoogleClicked);
            appleButton.onClick.RemoveListener(HandleAppleClicked);
            diceButton.onClick.RemoveListener(HandleDiceClicked);
            backButton.onClick.RemoveListener(HandleBackClicked);
            startButton.onClick.RemoveListener(HandleStartClicked);
            nicknameField.OnSubmitted -= HandleStartClicked;
            retryButton.onClick.RemoveListener(HandleRetryClicked);
            otherButton.onClick.RemoveListener(HandleOtherClicked);
        }

        public void ShowAgree()
        {
            Open(LoginSheetStep.Agree, AgreeTitle, listIcon, BandColor, closable: true, agreeTopAndWidth);
            agreeBody.HideNeed(); // 판이 켜진 뒤에 — 꺼져 있으면 버튼 · 판의 Awake 가 아직 안 돌았다
        }

        /// <summary>로그인 고르기 — Apple 은 iOS 에만 (23-login 7-2)</summary>
        public void ShowLogin(bool showApple)
        {
            appleButton.gameObject.SetActive(showApple);
            Open(LoginSheetStep.Login, StartTitle, keyIcon, BandColor, closable: true, loginTopAndWidth);
        }

        public void ShowLoading(string message)
        {
            loadingText.text = message;
            Switch(LoginSheetStep.Loading, StartTitle, keyIcon, BandColor, closable: false, otherTopAndWidth);
        }

        /// <summary>닉네임 단계를 새로 연다 — 추천 이름을 미리 넣는다</summary>
        public void ShowNickname(string suggestion)
        {
            nicknameField.SetText(suggestion);
            Open(LoginSheetStep.Nickname, NicknameTitle, userIcon, BandColor, closable: false, otherTopAndWidth);
        }

        /// <summary>닉네임 단계로 돌아와 에러를 보인다 — 입력값은 그대로 (저장 실패 · 금칙어 거절)</summary>
        public void ReturnToNickname(string error)
        {
            Switch(LoginSheetStep.Nickname, NicknameTitle, userIcon, BandColor, closable: false, otherTopAndWidth);
            nicknameField.ShowError(error);
        }

        /// <summary>지금 닉네임 단계에서 에러를 보인다</summary>
        public void ShowNicknameError(string error) => nicknameField.ShowError(error);

        public void SetNickname(string nickname) => nicknameField.SetText(nickname);

        /// <summary>실패 — 로그인 실패면 닫기 X 와 「다른 방법으로」 도 (시안 step fail)</summary>
        public void ShowFail(string title, string description, string code, bool loginFailure)
        {
            IsLoginFailure = loginFailure;
            failTitle.text = title;
            failDescription.text = description;
            failCode.text = code;
            otherButton.gameObject.SetActive(loginFailure);
            Switch(LoginSheetStep.Fail, loginFailure ? LoginFailTitle : ConnectionFailTitle, alertIcon, BadBandColor, closable: loginFailure, otherTopAndWidth);
        }

        public void Hide()
        {
            content.SetActive(false);
        }

        // 창을 연다 — 단계가 바뀌어도 튀어나온다 (시안 openSheet)
        private void Open(LoginSheetStep step, string title, Sprite icon, Color bandColor, bool closable, Vector2 topAndWidth)
        {
            Switch(step, title, icon, bandColor, closable, topAndWidth);
            UIJuice.Pop(frame, 0.92f);
        }

        private void Switch(LoginSheetStep step, string title, Sprite icon, Color bandColor, bool closable, Vector2 topAndWidth)
        {
            Step = step;
            content.SetActive(true);
            band.color = bandColor;
            bandIcon.sprite = icon;
            bandTitle.text = title;
            closeButton.gameObject.SetActive(closable);
            frame.anchoredPosition = new Vector2(0f, -topAndWidth.x);
            frame.sizeDelta = new Vector2(topAndWidth.y, frame.sizeDelta.y);

            agreeBody.gameObject.SetActive(step == LoginSheetStep.Agree);
            loginBody.SetActive(step == LoginSheetStep.Login);
            loadingBody.SetActive(step == LoginSheetStep.Loading);
            nicknameBody.SetActive(step == LoginSheetStep.Nickname);
            failBody.SetActive(step == LoginSheetStep.Fail);
        }

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();

        private void HandleAgreed() => OnAgreed?.Invoke();

        // 누른 버튼이 톡 튄다 (시안 fxPop — 로그인 · 시작 0.92, 주사위 0.85)
        private void HandleGuestClicked()
        {
            UIJuice.Pop((RectTransform)guestButton.transform, 0.92f);
            OnGuestRequested?.Invoke();
        }

        private void HandleGoogleClicked()
        {
            UIJuice.Pop((RectTransform)googleButton.transform, 0.92f);
            OnGoogleRequested?.Invoke();
        }

        private void HandleAppleClicked()
        {
            UIJuice.Pop((RectTransform)appleButton.transform, 0.92f);
            OnAppleRequested?.Invoke();
        }

        private void HandleDiceClicked()
        {
            UIJuice.Pop((RectTransform)diceButton.transform, 0.85f);
            OnDiceRequested?.Invoke();
        }

        private void HandleBackClicked() => OnBackRequested?.Invoke();

        private void HandleStartClicked() => OnStartRequested?.Invoke();

        private void HandleRetryClicked() => OnRetryRequested?.Invoke();

        private void HandleOtherClicked() => OnOtherMethodRequested?.Invoke();
    }
}
