using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 설정 창과 그 안의 목록 창 — 소리 · 초대 알림 스위치 · 차단 목록 · 화면(PC) · 계정(로그인 수단 · 계정 삭제) · 정보(버전 · 링크).
    /// 목록 창 하나를 차단 목록과 「화면 크기」 고르기가 함께 쓴다 — 머리 제목과 줄만 바뀐다.
    /// 로그아웃 버튼은 없다 (23-login 3절 #11). 버전 글자를 연달아 누르면 개발자 모드를 청한다 (14-lobby-ui 5절).
    /// 기준: 「통합 화면 UI v2」 로비 settingsBody · blockBody, docs/systems/17-lobby-ui-full.md 5-4
    /// </summary>
    public class LobbySettingsPopupView : MonoBehaviour
    {
        private const string BlockedCountFormat = "{0}명";
        private const string BlockListTitle = "차단 목록";
        private const string ResolutionListTitle = "화면 크기";
        private const int DevModeTaps = 5;          // 버전 글자를 이만큼 누르면 개발자 모드
        private const float DevModeTapGap = 0.6f;   // 누름 사이가 이보다 길면 처음부터 센다 (초)

        [Header("설정")]
        [SerializeField] private GameObject settingsContent;
        [SerializeField] private Button closeButton;
        [SerializeField] private LobbySwitchView sfxSwitch;
        [SerializeField] private LobbySwitchView bgmSwitch;
        [SerializeField] private LobbySwitchView inviteSwitch;
        [Tooltip("정보 「플레이 기록 보내기」 (17 7-4 · 27-analytics)")]
        [SerializeField] private LobbySwitchView playRecordSwitch;
        [SerializeField] private TMP_Text blockedCountText;
        [SerializeField] private Button manageButton;
        [SerializeField] private TMP_Text loginText;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button contactButton;

        [Tooltip("정보의 버전 글자 — 눌리는 모양 없이 누름만 받는다 (개발자 모드)")]
        [SerializeField] private Button versionButton;

        [Tooltip("「화면」 줄 (PC) — 「변경」 을 누르면 목록 창에 화면 크기를 연다")]
        [SerializeField] private LobbyResolutionView resolution;

        [Header("목록 창 (차단 목록 · 화면 크기)")]
        [SerializeField] private GameObject blockContent;
        [Tooltip("목록 창 머리 제목 — 「차단 목록」 / 「화면 크기」")]
        [SerializeField] private TMP_Text listTitleText;
        [SerializeField] private Button blockCloseButton;
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform blockList;
        [SerializeField] private LobbyPersonRowView rowTemplate;
        [SerializeField] private GameObject blockEmpty;

        private readonly List<LobbyPersonRowView> _rows = new List<LobbyPersonRowView>();
        private bool _isResolutionList; // 목록 창이 「화면 크기」 를 보이는 중
        private int _versionTaps;
        private float _versionTappedAt;

        public bool IsOpen => settingsContent.activeSelf || blockContent.activeSelf;

        /// <summary>목록 창(차단 목록 · 화면 크기)이 열려 있다 — 뒤로가기는 설정으로.</summary>
        public bool IsListOpen => blockContent.activeSelf;

        public bool IsBlockListOpen => blockContent.activeSelf && !_isResolutionList;

        public event Action OnCloseRequested;
        public event Action<bool> OnSfxChanged;
        public event Action<bool> OnBgmChanged;
        public event Action<bool> OnInviteChanged;
        public event Action<bool> OnPlayRecordChanged;
        public event Action OnBlockListRequested;
        public event Action OnBackRequested;
        public event Action<HubPerson> OnUnblockRequested;
        public event Action OnDeleteAccountRequested;
        public event Action OnPrivacyRequested;
        public event Action OnContactRequested;

        /// <summary>버전 글자를 5번 연달아 눌렀다 — 개발자 모드 켜기(암호) · 끄기.</summary>
        public event Action OnDevModeRequested;

        private void Awake()
        {
            if (settingsContent == null || closeButton == null || sfxSwitch == null || bgmSwitch == null || inviteSwitch == null || playRecordSwitch == null || blockedCountText == null
                || manageButton == null || loginText == null || deleteButton == null || privacyButton == null || contactButton == null || versionButton == null || resolution == null)
                Debug.LogError($"[{nameof(LobbySettingsPopupView)}] 설정 참조가 비어 있습니다.", this);

            if (blockContent == null || listTitleText == null || blockCloseButton == null || backButton == null || blockList == null || rowTemplate == null || blockEmpty == null)
                Debug.LogError($"[{nameof(LobbySettingsPopupView)}] 차단 목록 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            blockCloseButton.onClick.AddListener(HandleCloseClicked);
            manageButton.onClick.AddListener(HandleManageClicked);
            backButton.onClick.AddListener(HandleBackClicked);
            deleteButton.onClick.AddListener(HandleDeleteClicked);
            privacyButton.onClick.AddListener(HandlePrivacyClicked);
            contactButton.onClick.AddListener(HandleContactClicked);
            versionButton.onClick.AddListener(HandleVersionClicked);
            sfxSwitch.OnChanged += HandleSwitchChanged;
            bgmSwitch.OnChanged += HandleSwitchChanged;
            inviteSwitch.OnChanged += HandleSwitchChanged;
            playRecordSwitch.OnChanged += HandleSwitchChanged;
            resolution.OnListRequested += ShowResolutionList;
            foreach (LobbyPersonRowView row in _rows)
            {
                row.OnPrimaryClicked += HandleOptionClicked;
                row.OnSecondaryClicked += HandleUnblockClicked;
            }
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            blockCloseButton.onClick.RemoveListener(HandleCloseClicked);
            manageButton.onClick.RemoveListener(HandleManageClicked);
            backButton.onClick.RemoveListener(HandleBackClicked);
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
            privacyButton.onClick.RemoveListener(HandlePrivacyClicked);
            contactButton.onClick.RemoveListener(HandleContactClicked);
            versionButton.onClick.RemoveListener(HandleVersionClicked);
            sfxSwitch.OnChanged -= HandleSwitchChanged;
            bgmSwitch.OnChanged -= HandleSwitchChanged;
            inviteSwitch.OnChanged -= HandleSwitchChanged;
            playRecordSwitch.OnChanged -= HandleSwitchChanged;
            resolution.OnListRequested -= ShowResolutionList;
            foreach (LobbyPersonRowView row in _rows)
            {
                row.OnPrimaryClicked -= HandleOptionClicked;
                row.OnSecondaryClicked -= HandleUnblockClicked;
            }
        }

        public void ShowSettings(bool sfxOn, bool bgmOn, bool inviteOn, bool playRecordOn, int blockedCount, string loginName)
        {
            blockContent.SetActive(false);
            settingsContent.SetActive(true);
            sfxSwitch.Set(sfxOn);
            bgmSwitch.Set(bgmOn);
            inviteSwitch.Set(inviteOn);
            playRecordSwitch.Set(playRecordOn);
            blockedCountText.text = string.Format(BlockedCountFormat, blockedCount);
            loginText.text = loginName;
        }

        public void ShowBlockList(LobbyHubData data)
        {
            OpenList(BlockListTitle, false);
            blockEmpty.SetActive(data.Blocked.Count == 0);
            UseRows(data.Blocked.Count);
            for (int i = 0; i < data.Blocked.Count; i++)
                _rows[i].BindBlocked(data.Blocked[i]);
        }

        /// <summary>「화면 크기」 목록 — 지금 쓰는 것은 「사용 중」, 나머지는 「선택」. 고르면 바로 바꾸고 목록에 남는다.</summary>
        private void ShowResolutionList() => ShowResolutionList(resolution.CurrentIndex);

        // current = 「사용 중」 으로 보일 줄 — 고른 직후에는 화면 크기가 다음 프레임에 바뀌어서 고른 줄을 넘긴다
        private void ShowResolutionList(int current)
        {
            OpenList(ResolutionListTitle, true);
            blockEmpty.SetActive(false);
            IReadOnlyList<string> labels = resolution.Labels;
            UseRows(labels.Count);
            for (int i = 0; i < labels.Count; i++)
                _rows[i].BindOption(labels[i], i == current);
        }

        private void OpenList(string title, bool isResolution)
        {
            _isResolutionList = isResolution;
            listTitleText.text = title;
            settingsContent.SetActive(false);
            blockContent.SetActive(true);
        }

        // 줄을 count 개 켠다 — 모자라면 템플릿에서 만들고, 남는 줄은 끈다
        private void UseRows(int count)
        {
            while (_rows.Count < count)
            {
                LobbyPersonRowView created = Instantiate(rowTemplate, blockList);
                created.OnPrimaryClicked += HandleOptionClicked;
                created.OnSecondaryClicked += HandleUnblockClicked;
                _rows.Add(created);
            }

            for (int i = 0; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(i < count);
        }

        public void Hide()
        {
            settingsContent.SetActive(false);
            blockContent.SetActive(false);
        }

        private void HandleSwitchChanged(LobbySwitchView changed)
        {
            if (changed == sfxSwitch)
                OnSfxChanged?.Invoke(changed.IsOn);
            else if (changed == bgmSwitch)
                OnBgmChanged?.Invoke(changed.IsOn);
            else if (changed == inviteSwitch)
                OnInviteChanged?.Invoke(changed.IsOn);
            else
                OnPlayRecordChanged?.Invoke(changed.IsOn);
        }

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();

        private void HandleManageClicked() => OnBlockListRequested?.Invoke();

        private void HandleBackClicked() => OnBackRequested?.Invoke();

        private void HandleDeleteClicked() => OnDeleteAccountRequested?.Invoke();

        private void HandlePrivacyClicked() => OnPrivacyRequested?.Invoke();

        private void HandleContactClicked() => OnContactRequested?.Invoke();

        private void HandleVersionClicked()
        {
            float now = Time.unscaledTime;
            _versionTaps = now - _versionTappedAt > DevModeTapGap ? 1 : _versionTaps + 1;
            _versionTappedAt = now;
            if (_versionTaps < DevModeTaps)
                return;

            _versionTaps = 0;
            OnDevModeRequested?.Invoke();
        }

        private void HandleUnblockClicked(LobbyPersonRowView row) => OnUnblockRequested?.Invoke(row.Person);

        private void HandleOptionClicked(LobbyPersonRowView row)
        {
            if (!_isResolutionList)
                return;

            int index = _rows.IndexOf(row);
            resolution.Select(index);
            ShowResolutionList(index);
        }
    }
}
