using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 설정 창과 그 안의 차단 목록 창 — 소리 · 초대 알림 스위치 · 차단 목록 · 계정(로그인 수단 · 계정 삭제) · 정보(버전 · 링크).
    /// 로그아웃 버튼은 없다 (23-login 3절 #11). 버전 글자를 연달아 누르면 개발자 모드를 청한다 (14-lobby-ui 5절).
    /// 기준: 「통합 화면 UI v2」 로비 settingsBody · blockBody, docs/systems/17-lobby-ui-full.md 5-4
    /// </summary>
    public class LobbySettingsPopupView : MonoBehaviour
    {
        private const string BlockedCountFormat = "{0}명";
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

        [Header("차단 목록")]
        [SerializeField] private GameObject blockContent;
        [SerializeField] private Button blockCloseButton;
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform blockList;
        [SerializeField] private LobbyPersonRowView rowTemplate;
        [SerializeField] private GameObject blockEmpty;

        private readonly List<LobbyPersonRowView> _rows = new List<LobbyPersonRowView>();
        private int _versionTaps;
        private float _versionTappedAt;

        public bool IsOpen => settingsContent.activeSelf || blockContent.activeSelf;

        public bool IsBlockListOpen => blockContent.activeSelf;

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
                || manageButton == null || loginText == null || deleteButton == null || privacyButton == null || contactButton == null || versionButton == null)
                Debug.LogError($"[{nameof(LobbySettingsPopupView)}] 설정 참조가 비어 있습니다.", this);

            if (blockContent == null || blockCloseButton == null || backButton == null || blockList == null || rowTemplate == null || blockEmpty == null)
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
            foreach (LobbyPersonRowView row in _rows)
                row.OnSecondaryClicked += HandleUnblockClicked;
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
            foreach (LobbyPersonRowView row in _rows)
                row.OnSecondaryClicked -= HandleUnblockClicked;
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
            settingsContent.SetActive(false);
            blockContent.SetActive(true);
            blockEmpty.SetActive(data.Blocked.Count == 0);
            for (int i = 0; i < data.Blocked.Count; i++)
            {
                if (i == _rows.Count)
                {
                    LobbyPersonRowView created = Instantiate(rowTemplate, blockList);
                    created.OnSecondaryClicked += HandleUnblockClicked;
                    _rows.Add(created);
                }

                _rows[i].gameObject.SetActive(true);
                _rows[i].BindBlocked(data.Blocked[i]);
            }

            for (int i = data.Blocked.Count; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);
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
    }
}
