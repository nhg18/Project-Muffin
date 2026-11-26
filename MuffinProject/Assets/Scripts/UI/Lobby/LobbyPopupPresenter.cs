using Chapchu.Core;
using Chapchu.Account;
using Chapchu.Network;
using UnityEngine;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비의 창 — 친구 · 프로필 · 설정(차단 목록) · 퀘스트 · 확률. 한 번에 하나만 열리고, 열려 있는 동안 뒤가 어두워진다.
    /// 친구 · 퀘스트는 LobbyHubData 의 시안 값이고, 설정 스위치만 기기에 저장한다.
    /// 기준: 「통합 화면 UI v2」 로비(게시본 1794316285-7080) · docs/systems/17-lobby-ui-full.md 5-2 ~ 5-4 · 7-2 ~ 7-4
    /// </summary>
    public class LobbyPopupPresenter : MonoBehaviour
    {
        private const string UnfriendOkText = "삭제"; // 확인 창 오른쪽 버튼 = 하는 일 이름 (시안 v3.23)
        private const string BlockOkText = "차단";
        private const string UnblockOkText = "해제";
        private const string CancelText = "취소";
        private const string UnfriendTitle = "친구 삭제";
        private const string UnfriendMessageFormat = "{0}님을 친구에서 삭제할까요?";
        private const string BlockTitle = "차단";
        private const string BlockMessageFormat = "{0}님을 차단할까요? 요청 · 초대가 막히고 목록에서 사라져요.";
        private const string UnblockTitle = "차단 해제";
        private const string UnblockMessage = "차단을 해제할까요? 친구로 돌아오지는 않아요.";
        private const string GuestText = "게스트";
        private const string AccountText = "계정";
        private const string LoginNoteFormat = "{0} 계정으로 로그인";
        private const string LoginNameFormat = "{0} 로그인";
        private const string CopiedCodeToast = "친구 코드를 복사했어요.";
        private const string CopiedIdToast = "고유 ID를 복사했어요.";
        private const string ClaimedAllToast = "보상을 모두 받았어요.";

        [SerializeField] private LobbyPresenter lobby;

        [Header("창을 여는 버튼이 있는 곳")]
        [SerializeField] private LobbyHomeView home;
        [SerializeField] private LobbyTopBarView topBar;

        [Header("창")]
        [SerializeField] private GameObject popupDimmer;
        [SerializeField] private LobbyFriendsPopupView friends;
        [SerializeField] private LobbyProfilePopupView profile;
        [SerializeField] private LobbySettingsPopupView settings;
        [SerializeField] private LobbyQuestPopupView quests;
        [SerializeField] private LobbyRatesPopupView rates;

        [Header("창 위에 뜨는 것")]
        [SerializeField] private LobbyModalView modal;
        [SerializeField] private LobbyInviteBannerView invite;
        [SerializeField] private LobbyFxView fx;

        private AnalyticsRecorder.ScreenName _screenBelowPopup = AnalyticsRecorder.ScreenName.Lobby; // 창을 닫으면 돌아갈 화면 (27 5-5)
        private bool _inPopupScreen; // 플레이 기록에 창 화면으로 들어가 있는지 — EnterPopupScreen 에서 켜고 ClosePopups 에서 끈다

        /// <summary>설정의 「초대 알림」 스위치가 켜져 있는지 (기기 저장, 처음엔 켜짐).</summary>
        public static bool IsInviteNoticeOn => ReadSwitch(PlayerPrefsKeys.InviteNoticeOn);

        // 계정(Google · Apple)으로 들어왔는지 — 어느 수단인지는 아직 남기지 않아 「계정」 으로만 보인다
        private static bool IsAccountLogin => PlayerPrefs.GetInt(PlayerPrefsKeys.AccountLogin, 0) == 1;

        private LobbyHubData Data => lobby.Data;

        /// <summary>친구 · 프로필 · 설정 · 퀘스트 · 확률 창 중 하나라도 떠 있다.</summary>
        public bool IsOpen => friends.IsOpen || profile.IsOpen || settings.IsOpen || quests.IsOpen || rates.IsOpen;

        private void Awake()
        {
            if (lobby == null || home == null || topBar == null)
                Debug.LogError($"[{nameof(LobbyPopupPresenter)}] 로비 · 화면 참조가 비어 있습니다.", this);

            if (popupDimmer == null || friends == null || profile == null || settings == null || quests == null || rates == null
                || modal == null || invite == null || fx == null)
                Debug.LogError($"[{nameof(LobbyPopupPresenter)}] 창 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            home.OnProfileRequested += HandleProfileRequested;
            home.OnQuestsRequested += ShowQuests;
            topBar.OnFriendsRequested += HandleFriendsRequested;
            topBar.OnSettingsRequested += ShowSettings;

            friends.OnCloseRequested += ClosePopups;
            friends.OnCopyRequested += HandleCopyCodeRequested;
            friends.OnAcceptRequested += HandleFriendAccepted;
            friends.OnDeclineRequested += HandleFriendDeclined;
            friends.OnCancelRequested += HandleFriendRequestCanceled;
            friends.OnFriendRequestRequested += HandleFriendRequestRequested;
            friends.OnDeleteRequested += HandleUnfriendRequested;
            friends.OnBlockRequested += HandleBlockRequested;
            profile.OnCloseRequested += ClosePopups;
            profile.OnCopyCodeRequested += HandleCopyCodeRequested;
            profile.OnCopyIdRequested += HandleCopyIdRequested;
            settings.OnCloseRequested += ClosePopups;
            settings.OnSfxChanged += HandleSfxChanged;
            settings.OnBgmChanged += HandleBgmChanged;
            settings.OnInviteChanged += HandleInviteNoticeChanged;
            settings.OnPlayRecordChanged += HandlePlayRecordChanged;
            settings.OnBlockListRequested += HandleBlockListRequested;
            settings.OnBackRequested += ShowSettings;
            settings.OnUnblockRequested += HandleUnblockRequested;
            settings.OnPrivacyRequested += HandlePrivacyRequested;
            settings.OnContactRequested += HandleContactRequested;
            quests.OnCloseRequested += ClosePopups;
            quests.OnClaimRequested += HandleQuestClaimRequested;
            quests.OnBonusRequested += HandleQuestBonusRequested;
            quests.OnClaimAllRequested += HandleQuestClaimAllRequested;
            rates.OnCloseRequested += ClosePopups;
        }

        private void OnDisable()
        {
            home.OnProfileRequested -= HandleProfileRequested;
            home.OnQuestsRequested -= ShowQuests;
            topBar.OnFriendsRequested -= HandleFriendsRequested;
            topBar.OnSettingsRequested -= ShowSettings;

            friends.OnCloseRequested -= ClosePopups;
            friends.OnCopyRequested -= HandleCopyCodeRequested;
            friends.OnAcceptRequested -= HandleFriendAccepted;
            friends.OnDeclineRequested -= HandleFriendDeclined;
            friends.OnCancelRequested -= HandleFriendRequestCanceled;
            friends.OnFriendRequestRequested -= HandleFriendRequestRequested;
            friends.OnDeleteRequested -= HandleUnfriendRequested;
            friends.OnBlockRequested -= HandleBlockRequested;
            profile.OnCloseRequested -= ClosePopups;
            profile.OnCopyCodeRequested -= HandleCopyCodeRequested;
            profile.OnCopyIdRequested -= HandleCopyIdRequested;
            settings.OnCloseRequested -= ClosePopups;
            settings.OnSfxChanged -= HandleSfxChanged;
            settings.OnBgmChanged -= HandleBgmChanged;
            settings.OnInviteChanged -= HandleInviteNoticeChanged;
            settings.OnPlayRecordChanged -= HandlePlayRecordChanged;
            settings.OnBlockListRequested -= HandleBlockListRequested;
            settings.OnBackRequested -= ShowSettings;
            settings.OnUnblockRequested -= HandleUnblockRequested;
            settings.OnPrivacyRequested -= HandlePrivacyRequested;
            settings.OnContactRequested -= HandleContactRequested;
            quests.OnCloseRequested -= ClosePopups;
            quests.OnClaimRequested -= HandleQuestClaimRequested;
            quests.OnBonusRequested -= HandleQuestBonusRequested;
            quests.OnClaimAllRequested -= HandleQuestClaimAllRequested;
            rates.OnCloseRequested -= ClosePopups;
        }

        /// <summary>뒤로가기 한 겹 — 친구 줄 메뉴 → 차단 목록 → 열린 창. 닫은 것이 있으면 true.</summary>
        public bool Back()
        {
            if (friends.IsOpen && friends.CloseMenu())
                return true;

            if (settings.IsBlockListOpen)
            {
                ShowSettings();
                return true;
            }

            if (!IsOpen)
                return false;

            ClosePopups();
            return true;
        }

        /// <summary>퀘스트 창을 연다.</summary>
        public void ShowQuests()
        {
            HidePopups();
            EnterPopupScreen(AnalyticsRecorder.ScreenName.Quest);
            quests.Show(Data);
            popupDimmer.SetActive(true);
        }

        /// <summary>뽑기 확률 창을 연다.</summary>
        public void ShowRates()
        {
            HidePopups();
            rates.Show(Data);
            popupDimmer.SetActive(true);
        }

        /// <summary>친구 창이 열려 있으면 목록을 데이터에 맞춰 다시 그린다.</summary>
        public void RefreshFriends() => friends.Refresh();

        // 창을 닫고 그 아래 화면으로 돌아간다 — 플레이 기록에는 창 아래 화면에 다시 들어간 것으로 남긴다 (27 5-5)
        private void ClosePopups()
        {
            HidePopups();
            if (!_inPopupScreen)
                return;

            _inPopupScreen = false;
            AnalyticsRecorder.RecordScreenViewed(_screenBelowPopup);
        }

        // 다른 창을 열기 전에 닫기만 — 창에서 창으로 옮길 때 아래 화면을 끼워 남기지 않는다
        private void HidePopups()
        {
            friends.Hide();
            profile.Hide();
            settings.Hide();
            quests.Hide();
            rates.Hide();
            popupDimmer.SetActive(false);
        }

        // 창 하나에 들어감 — 창 위에서 창을 열면 맨 아래 화면을 그대로 둔다
        private void EnterPopupScreen(AnalyticsRecorder.ScreenName screen)
        {
            if (!_inPopupScreen)
                _screenBelowPopup = AnalyticsRecorder.CurrentScreen;

            _inPopupScreen = true;
            AnalyticsRecorder.RecordScreenViewed(screen);
        }

        #region 프로필 · 설정

        private void HandleProfileRequested()
        {
            HidePopups();
            EnterPopupScreen(AnalyticsRecorder.ScreenName.Profile);
            profile.Show(NetworkManager.Nickname, Data, AccountService.PlayerId, IsAccountLogin ? string.Format(LoginNoteFormat, AccountText) : GuestText);
            popupDimmer.SetActive(true);
        }

        private void ShowSettings()
        {
            HidePopups();
            EnterPopupScreen(AnalyticsRecorder.ScreenName.Settings);
            settings.ShowSettings(ReadSwitch(PlayerPrefsKeys.SfxOn), ReadSwitch(PlayerPrefsKeys.BgmOn), IsInviteNoticeOn, AnalyticsRecorder.IsAgreed,
                Data.Blocked.Count, IsAccountLogin ? string.Format(LoginNameFormat, AccountText) : GuestText);
            popupDimmer.SetActive(true);
        }

        private static bool ReadSwitch(string key) => PlayerPrefs.GetInt(key, 1) == 1;

        private static void SaveSwitch(string key, bool isOn)
        {
            PlayerPrefs.SetInt(key, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void HandleSfxChanged(bool isOn) => SaveSwitch(PlayerPrefsKeys.SfxOn, isOn);

        private void HandleBgmChanged(bool isOn) => SaveSwitch(PlayerPrefsKeys.BgmOn, isOn);

        private void HandleInviteNoticeChanged(bool isOn) => SaveSwitch(PlayerPrefsKeys.InviteNoticeOn, isOn);

        // 켬 → 수집 시작 / 끔 → 수집 중지 + 이미 보낸 기록 삭제 요청. 토스트 없음 (17 7-4 · 27 6절)
        private static void HandlePlayRecordChanged(bool isOn) => AnalyticsRecorder.SetAgreedFromSettings(isOn);

        private void HandleBlockListRequested()
        {
            HidePopups();
            settings.ShowBlockList(Data);
            popupDimmer.SetActive(true);
        }

        private void HandleUnblockRequested(HubPerson person)
        {
            modal.ShowWarn(UnblockTitle, UnblockMessage, UnblockOkText, CancelText, () =>
            {
                Data.Blocked.Remove(person);
                if (settings.IsBlockListOpen)
                    settings.ShowBlockList(Data);
            }, LobbyWarnKind.Ask);
        }

        // 주소가 아직 없다 (17 미정) — 정해지면 Application.OpenURL
        private void HandlePrivacyRequested() => Debug.LogWarning($"[{nameof(LobbyPopupPresenter)}] 개인정보처리방침 주소가 아직 정해지지 않아 열지 않는다.");

        private void HandleContactRequested() => Debug.LogWarning($"[{nameof(LobbyPopupPresenter)}] 문의 주소가 아직 정해지지 않아 열지 않는다.");

        // 가려 있어도 값을 복사한다 (17 5-2)
        private void HandleCopyCodeRequested()
        {
            GUIUtility.systemCopyBuffer = Data.MyCode;
            fx.Toast(CopiedCodeToast);
        }

        private void HandleCopyIdRequested()
        {
            string playerId = AccountService.PlayerId;
            if (string.IsNullOrEmpty(playerId))
                return;

            GUIUtility.systemCopyBuffer = playerId;
            fx.Toast(CopiedIdToast);
        }

        #endregion

        #region 친구

        private void HandleFriendsRequested()
        {
            HidePopups();
            EnterPopupScreen(AnalyticsRecorder.ScreenName.Friends);
            friends.Show(Data);
            popupDimmer.SetActive(true);
        }

        private void HandleFriendAccepted(HubPerson request)
        {
            Data.Accept(request);
            lobby.RefreshBadges();
            friends.ShowTab(LobbyFriendsTab.Friends);
            friends.Flash(request.Nickname);
        }

        private void HandleFriendDeclined(HubPerson request)
        {
            Data.RequestsIn.Remove(request);
            lobby.RefreshBadges();
            friends.Refresh();
        }

        private void HandleFriendRequestCanceled(HubPerson request)
        {
            Data.RequestsOut.Remove(request);
            friends.Refresh();
        }

        private void HandleFriendRequestRequested(HubSearchResult result)
        {
            Data.Request(result);
            lobby.RefreshBadges();
            friends.Refresh();
        }

        private void HandleUnfriendRequested(HubPerson friend)
        {
            modal.ShowWarn(UnfriendTitle, string.Format(UnfriendMessageFormat, friend.Nickname), UnfriendOkText, CancelText, () =>
            {
                Data.Friends.Remove(friend);
                friends.Refresh();
            }, LobbyWarnKind.Ask);
        }

        private void HandleBlockRequested(HubPerson friend)
        {
            modal.ShowWarn(BlockTitle, string.Format(BlockMessageFormat, friend.Nickname), BlockOkText, CancelText, () =>
            {
                Data.Block(friend);
                if (invite.Inviter == friend.Nickname)
                    invite.Hide(); // 차단한 친구의 초대 배너도 닫힌다

                lobby.RefreshBadges();
                friends.Refresh();
            }, LobbyWarnKind.Danger);
        }

        #endregion

        #region 퀘스트

        private void HandleQuestClaimRequested(HubQuest quest)
        {
            if (!Data.Claim(quest))
                return;

            AfterQuestReward(quest.RewardCurrency, quest.RewardAmount);
        }

        private void HandleQuestBonusRequested()
        {
            if (Data.ClaimDailyBonus())
                AfterQuestReward(HubCurrency.Churu, LobbyHubData.DailyBonusChuru);
        }

        private void HandleQuestClaimAllRequested()
        {
            (int bone, int churu) = Data.ClaimAll(quests.IsDaily);
            if (bone == 0 && churu == 0)
                return;

            lobby.RefreshWallet();
            fx.Gain(topBar.CoinRect(HubCurrency.Bone), bone);
            fx.Gain(topBar.CoinRect(HubCurrency.Churu), churu);
            fx.Toast(ClaimedAllToast);
            quests.Refresh();
            lobby.RefreshBadges();
        }

        private void AfterQuestReward(HubCurrency currency, int amount)
        {
            lobby.RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), amount);
            quests.Refresh();
            lobby.RefreshBadges();
        }

        #endregion
    }
}
