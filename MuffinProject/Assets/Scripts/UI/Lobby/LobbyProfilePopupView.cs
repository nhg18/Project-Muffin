using System;
using Chapchu.UI.RoomScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 프로필 창 — 머리(아바타 + Lv 배지 · 닉네임 + 연필 · 경험치 · 다음 레벨 · 쿠폰) → 전적 칸 넷(승률 강조) → 친구 코드 · 고유 ID 줄(기본 가림) → 로그인 줄.
    /// 기준: 「통합 화면 UI v3」 로비 profileBody, docs/systems/17-lobby-ui-full.md 5-2 · 7-3
    /// </summary>
    public class LobbyProfilePopupView : MonoBehaviour
    {
        /// <summary>친구 코드 · 고유 ID 한 줄 — 값을 누르면 가려진 채로 복사, 눈 버튼을 누르고 있는 동안만 값이 보인다.</summary>
        [Serializable]
        private class IdRow
        {
            public Button copyButton;

            [Tooltip("값이 보이는 동안 흰 바탕")]
            public RoundBox copyBack;

            public TMP_Text valueText;
            public HoldButton peekButton;

            [Tooltip("눈 버튼 몸통 — 누르고 있는 동안 노랑")]
            public RoundBox peekBody;

            [NonSerialized] public string Value;
            [NonSerialized] public string Mask;
        }

        private const string CodeMask = "••••••";
        private const string IdMask = "••••••••••••";

        // 가린 점 사이 띄움 (TMP 글자 사이, 1 = 0.01em) — 시안 점 간격 22.4 (고정폭 .2em) 을 Pretendard Bold 로 맞춘 값
        private const float MaskSpacing = 36f;

        [SerializeField] private GameObject content;
        [SerializeField] private Button closeButton;

        [Header("머리")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private LayoutElement nicknameSize;
        [SerializeField] private Button changeButton;
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private TMP_Text nextText;
        [SerializeField] private TMP_Text couponText;

        [Tooltip("닉네임 글자 폭 한도 (캔버스 px) — 넘으면 말줄임. 연필이 닉네임 바로 옆에 붙는다")]
        [SerializeField] private float nicknameMaxWidth = 664f;

        [Header("전적")]
        [SerializeField] private TMP_Text winRateText;
        [SerializeField] private TMP_Text recordText;
        [SerializeField] private TMP_Text gamesText;
        [SerializeField] private TMP_Text killWinsText;
        [SerializeField] private TMP_Text chapchuWinsText;

        [Header("내 ID")]
        [SerializeField] private IdRow codeRow;
        [SerializeField] private IdRow idRow;
        [SerializeField] private TMP_Text loginText;

        public bool IsOpen => content.activeSelf;

        public event Action OnCloseRequested;
        public event Action OnChangeRequested;
        public event Action OnCopyCodeRequested;
        public event Action OnCopyIdRequested;

        private void Awake()
        {
            if (content == null || closeButton == null || levelText == null || nicknameText == null || nicknameSize == null || changeButton == null
                || xpFill == null || xpText == null || nextText == null || couponText == null)
                Debug.LogError($"[{nameof(LobbyProfilePopupView)}] 머리 참조가 비어 있습니다.", this);

            if (winRateText == null || recordText == null || gamesText == null || killWinsText == null || chapchuWinsText == null || loginText == null)
                Debug.LogError($"[{nameof(LobbyProfilePopupView)}] 전적 · 로그인 참조가 비어 있습니다.", this);

            if (!IsWired(codeRow) || !IsWired(idRow))
                Debug.LogError($"[{nameof(LobbyProfilePopupView)}] 친구 코드 · 고유 ID 줄 참조가 비어 있습니다.", this);

            codeRow.Mask = CodeMask;
            idRow.Mask = IdMask;
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            changeButton.onClick.AddListener(HandleChangeClicked);
            codeRow.copyButton.onClick.AddListener(HandleCopyCodeClicked);
            idRow.copyButton.onClick.AddListener(HandleCopyIdClicked);
            codeRow.peekButton.OnHeldChanged += HandleCodePeekChanged;
            idRow.peekButton.OnHeldChanged += HandleIdPeekChanged;
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            changeButton.onClick.RemoveListener(HandleChangeClicked);
            codeRow.copyButton.onClick.RemoveListener(HandleCopyCodeClicked);
            idRow.copyButton.onClick.RemoveListener(HandleCopyIdClicked);
            codeRow.peekButton.OnHeldChanged -= HandleCodePeekChanged;
            idRow.peekButton.OnHeldChanged -= HandleIdPeekChanged;
        }

        /// <param name="playerId">고유 ID — UGS 플레이어 ID. 로그인 전이면 null</param>
        public void Show(string nickname, LobbyHubData data, string playerId, string loginNote)
        {
            content.SetActive(true);
            SetNickname(nickname);

            levelText.text = $"Lv.{data.Level}";
            xpFill.anchorMax = new Vector2(data.XpNext > 0 ? Mathf.Clamp01((float)data.Xp / data.XpNext) : 0f, 1f);
            xpText.text = $"{data.Xp} / {data.XpNext}";
            nextText.text = $"다음 레벨까지 {Mathf.Max(0, data.XpNext - data.Xp)} XP";
            couponText.text = $"변경 쿠폰 {data.NicknameCoupons}장";

            // 승률 = 승 ÷ 판 수, 반올림 정수 · 판 0 이면 0 (17 7-3)
            int games = data.Wins + data.Losses;
            int rate = games > 0 ? Mathf.RoundToInt(data.Wins * 100f / games) : 0;
            winRateText.text = $"{rate}<space=2><size=34>%</size>";
            recordText.text = $"{data.Wins}승 {data.Losses}패";
            gamesText.text = games.ToString();
            killWinsText.text = data.KillWins.ToString();
            chapchuWinsText.text = data.ChapchuWins.ToString();

            codeRow.Value = data.MyCode;
            idRow.Value = playerId ?? string.Empty;
            SetPeek(codeRow, false);
            SetPeek(idRow, false);
            loginText.text = loginNote;
        }

        public void SetNickname(string nickname)
        {
            nicknameText.text = nickname;
            nicknameSize.preferredWidth = Mathf.Min(nicknameText.GetPreferredValues(nickname).x, nicknameMaxWidth);
        }

        public void Hide() => content.SetActive(false);

        private static bool IsWired(IdRow row) =>
            row != null && row.copyButton != null && row.copyBack != null && row.valueText != null && row.peekButton != null && row.peekBody != null;

        // 누르고 있는 줄만 보인다 — 두 줄은 따로 (17 5-2)
        private static void SetPeek(IdRow row, bool isPeeking)
        {
            row.valueText.text = isPeeking ? row.Value : row.Mask;
            row.valueText.color = isPeeking ? LobbyStyle.Ink : LobbyStyle.Ink2;
            row.valueText.characterSpacing = isPeeking ? 0f : MaskSpacing;
            row.copyBack.FillColor = isPeeking ? Color.white : Color.clear;
            row.peekBody.FillColor = isPeeking ? LobbyStyle.Turn : Color.white;
        }

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();

        private void HandleChangeClicked() => OnChangeRequested?.Invoke();

        private void HandleCopyCodeClicked() => OnCopyCodeRequested?.Invoke();

        private void HandleCopyIdClicked() => OnCopyIdRequested?.Invoke();

        private void HandleCodePeekChanged(bool isHeld) => SetPeek(codeRow, isHeld);

        private void HandleIdPeekChanged(bool isHeld) => SetPeek(idRow, isHeld);
    }
}
