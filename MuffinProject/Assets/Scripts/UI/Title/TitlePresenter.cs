using System;
using System.Collections;
using System.Threading.Tasks;
using Chapchu.Account;
using Chapchu.Cards;
using Chapchu.Core;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.Popups;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using DisconnectCause = Photon.Realtime.DisconnectCause; // Photon.Realtime 에도 AccountService 가 있어 통째로 가져오지 않는다

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 타이틀 흐름 — 약관 동의 → 플레이 기록 수집(처음만, 27-analytics) → 로그인 고르기 → (게스트) 닉네임 → 로그인 → 설정 값 → 카드 데이터(26-card-data) → 접속 → 닉네임 저장 → 로비.
    /// 자동 로그인(누르지 않아도 로비)은 계정(Google · Apple)으로 로그인해 둔 기기만. 게스트는 매번 「화면을 눌러 시작」 부터 —
    /// 계정(저장된 로그인)과 닉네임은 기기에 남아, 닉네임 창에 지난 닉네임이 미리 들어 있다 (PM 2026-11-11, 시안과 다른 점).
    /// 뷰와 계정(UGS) · 네트워크 · 씬 전환을 잇는 유일한 파일이다.
    /// 기준: 「통합 화면 UI v2」 타이틀 (게시본 1794204029-915a) · docs/systems/20-title-ui-full.md · 23-login.md 6절
    /// </summary>
    public class TitlePresenter : MonoBehaviour
    {
        private enum LoginMethod
        {
            Guest,
            Google,
            Apple,
        }

        private enum FailKind
        {
            Login,
            Connection,
        }

        private const string AutoSignInText = "자동 로그인 중…";
        private const string StartingText = "시작하는 중…";
        private const string ConnectingText = "서버에 연결 중…";
        private const string SavingText = "닉네임 저장 중…";
        private const string CheckingAccountFormat = "{0} 계정 확인 중…";
        private const string SaveFailedText = "잠시 후 다시 시도해 주세요.";
        private const string MaintenanceToastText = "점검이 끝나면 저절로 이어져요.";
        private const string ExternalLoginFailedCode = "ExternalLoginFailed";

        // 로그인 실패 — 거절(만료) · 그 밖 · Google · Apple ({0} = 방법 이름)
        private const string SignInRejectedTitle = "로그인 실패";
        private const string SignInRejectedText = "로그인이 만료됐어요. 다시 시도해 주세요.";
        private const string SignInUnavailableTitle = "로그인할 수 없음";
        private const string SignInUnavailableText = "연결을 확인하고 다시 시도해 주세요.";
        private const string ExternalLoginFailedTitleFormat = "{0} 로그인 실패";
        private const string ExternalLoginFailedTextFormat = "{0} 계정으로 로그인하지 못했어요. 다른 방법으로 시작해 주세요.";

        // 연결 실패 사유 (12-title-ui 10-5 — 문구는 시안의 해요체). {0} = 접속 제한 초 (PhotonConnection.ConnectTimeoutSeconds)
        private const string NoInternetTitle = "인터넷 연결 없음";
        private const string NoInternetText = "Wi-Fi 또는 데이터 연결을 확인해 주세요.";
        private const string ConnectTimeoutTitle = "연결 시간 초과";
        private const string ConnectTimeoutTextFormat = "서버가 {0}초 안에 응답하지 않았어요. 다시 시도해 주세요.";
        private const string ServerBusyTitle = "서버 혼잡";
        private const string ServerBusyText = "접속자가 많아요. 잠시 후 다시 시도해 주세요.";
        private const string DisconnectedTitle = "연결 끊김";
        private const string DisconnectedText = "연결이 끊어졌어요. 다시 시도해 주세요.";

        [SerializeField] private TitleBottomView bottom;
        [SerializeField] private LoginSheetView sheet;
        [SerializeField] private TitlePopupView popup;

        [Tooltip("로비로 넘어갈 때 사라지는 타이틀 화면 전체")]
        [SerializeField] private CanvasGroup screen;

        [Tooltip("타이틀 → 로비 페이드 (초) — 20 3절 #11")]
        [SerializeField] private float fadeSeconds = 0.3f;

        [Tooltip("「스토어로」 가 여는 주소 — 패키지 ID 가 정해지기 전이라 비어 있다 (systems README 전역 미정 #2)")]
        [SerializeField] private string storeUrl;

        private bool _agreed;           // 약관 동의 — 이번 실행에 했거나 기기에 기록이 있다
        private bool _autoSignIn;       // 저장된 로그인으로 들어가는 중
        private bool _forgetSession;    // 다음 로그인은 저장된 로그인을 버리고 새 계정으로
        private LoginMethod _method;
        private string _savedNickname;  // 서버에 저장된 닉네임 — 없으면 null
        private bool _nicknameLoaded;   // 이번 로그인에서 닉네임 읽기까지 성공했다 — 읽기 실패와 「서버에 없음」 을 가른다
        private string _typedNickname;  // 이번에 입력해 「시작」 한 닉네임 — 없으면 null
        private FailKind _failKind;
        private Coroutine _flow;
        private bool _connecting;       // 네트워크 서버 접속 결과를 기다리는 중
        private bool _connected;        // 이 타이틀에서 접속을 마쳤다 — 그 뒤 끊기면 연결 실패
        private bool _disconnectingOnPurpose;
        private bool _leaving;

        // 플레이 기록(27-analytics)
        private bool _firstRun;              // 약관 동의 기록이 없던 실행 — lobbyReached.isFirstRun
        private bool? _playRecordChoice;     // 이번 실행에 팝업에서 고른 값 — 첫 실행은 「시작」 때 약관과 함께 기록한다 (20 7-12)
        private bool _enterAfterChoice;      // 자동 로그인 기기인데 고른 기록이 없어 팝업부터 띄웠다
        private bool _quitFromPlayRecord;    // 플레이 기록 팝업에서 뒤로가기 → 종료 확인 — 취소하면 팝업으로 돌아간다
        private int _nicknameRejectCount;    // 이번 닉네임을 저장하기 전 금칙어로 거절된 횟수
        private float _loadSeconds;          // 로딩 묶음이 떠 있던 초의 합 — 고르는 시간 · 실패 · 점검 팝업을 보는 시간은 빼고 잰다
        private float _loadingSince = -1f;   // 지금 떠 있는 로딩 묶음이 뜬 시각 — 떠 있지 않으면 음수
        private bool _titleViewRecorded;
        private bool _maintenanceRecorded;

        private static bool IsApplePlatform => Application.platform == RuntimePlatform.IPhonePlayer;

        private void Awake()
        {
            if (bottom == null)
                Debug.LogError($"[{nameof(TitlePresenter)}] {nameof(bottom)} 참조가 비어 있습니다.", this);

            if (sheet == null)
                Debug.LogError($"[{nameof(TitlePresenter)}] {nameof(sheet)} 참조가 비어 있습니다.", this);

            if (popup == null)
                Debug.LogError($"[{nameof(TitlePresenter)}] {nameof(popup)} 참조가 비어 있습니다.", this);

            if (screen == null)
                Debug.LogError($"[{nameof(TitlePresenter)}] {nameof(screen)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            bottom.OnTapped += HandleTapped;
            bottom.OnRetryRequested += HandleRetryRequested;
            bottom.OnOtherMethodRequested += HandleOtherMethodRequested;

            sheet.OnCloseRequested += HandleCloseRequested;
            sheet.OnAgreed += HandleAgreed;
            sheet.OnGuestRequested += HandleGuestRequested;
            sheet.OnGoogleRequested += HandleGoogleRequested;
            sheet.OnAppleRequested += HandleAppleRequested;
            sheet.OnDiceRequested += HandleDiceRequested;
            sheet.OnStartRequested += HandleStartRequested;
            sheet.OnBackRequested += HandleBackRequested;
            sheet.OnRetryRequested += HandleRetryRequested;
            sheet.OnOtherMethodRequested += HandleOtherMethodRequested;

            popup.OnStoreRequested += HandleStoreRequested;
            popup.OnMaintenanceAcknowledged += HandleMaintenanceAcknowledged;
            popup.OnQuitConfirmed += HandleQuitConfirmed;
            popup.OnQuitCanceled += HandleQuitCanceled;
            popup.OnPlayRecordChosen += HandlePlayRecordChosen;

            ConnectionEvents.OnConnected += HandleConnected;
            ConnectionEvents.OnDisconnected += HandleDisconnected;
        }

        private void OnDisable()
        {
            bottom.OnTapped -= HandleTapped;
            bottom.OnRetryRequested -= HandleRetryRequested;
            bottom.OnOtherMethodRequested -= HandleOtherMethodRequested;

            sheet.OnCloseRequested -= HandleCloseRequested;
            sheet.OnAgreed -= HandleAgreed;
            sheet.OnGuestRequested -= HandleGuestRequested;
            sheet.OnGoogleRequested -= HandleGoogleRequested;
            sheet.OnAppleRequested -= HandleAppleRequested;
            sheet.OnDiceRequested -= HandleDiceRequested;
            sheet.OnStartRequested -= HandleStartRequested;
            sheet.OnBackRequested -= HandleBackRequested;
            sheet.OnRetryRequested -= HandleRetryRequested;
            sheet.OnOtherMethodRequested -= HandleOtherMethodRequested;

            popup.OnStoreRequested -= HandleStoreRequested;
            popup.OnMaintenanceAcknowledged -= HandleMaintenanceAcknowledged;
            popup.OnQuitConfirmed -= HandleQuitConfirmed;
            popup.OnQuitCanceled -= HandleQuitCanceled;
            popup.OnPlayRecordChosen -= HandlePlayRecordChosen;

            ConnectionEvents.OnConnected -= HandleConnected;
            ConnectionEvents.OnDisconnected -= HandleDisconnected;
        }

        private void Start()
        {
            sheet.Hide();
            popup.Hide();

            // 동의 기록이 없으면 어디에도 접속하지 않고 「화면을 눌러 시작」 (23 3절 #9).
            // 게스트로 쓰던 기기도 「화면을 눌러 시작」 부터 — 자동 로그인은 계정 로그인만
            _agreed = PlayerPrefs.GetInt(PlayerPrefsKeys.TermsAgreed, 0) == 1;
            _firstRun = !_agreed;
            if (!_agreed || PlayerPrefs.GetInt(PlayerPrefsKeys.AccountLogin, 0) != 1)
            {
                bottom.ShowTap();
                return;
            }

            bottom.Hide();

            // 플레이 기록 팝업이 생기기 전에 동의한 기기 — 로그인 전에 한 번 묻는다 (27 8절)
            if (!AnalyticsRecorder.HasChoice)
            {
                _enterAfterChoice = true;
                popup.ShowPlayRecord();
                return;
            }

            Run(EnterRoutine());
        }

        // Android 뒤로가기 · Esc (시안 back)
        private void Update()
        {
            if (_leaving || !Input.GetKeyDown(KeyCode.Escape))
                return;

            HandleBackKey();
        }

        #region 흐름

        // 계정(Google · Apple)으로 로그인해 둔 기기 — 누르지 않아도 자동 로그인 (시안 enter)
        private IEnumerator EnterRoutine()
        {
            Task initialize = AccountService.InitializeAsync();
            yield return Wait(initialize, AccountService.SignInTimeoutSeconds);
            if (!Succeeded(initialize))
                Debug.LogWarning($"[{nameof(TitlePresenter)}] UGS 초기화 실패 — 로그인할 때 다시 시도한다: {initialize.Exception?.GetBaseException().Message}");

            if (!AccountService.HasSavedSession)
            {
                bottom.ShowTap();
                yield break;
            }

            _autoSignIn = true;
            ShowLoading(AutoSignInText);
            yield return SignInRoutine();
        }

        // 로그인 → 서버 닉네임 읽기 → 설정 값 → 접속 (시안 authed)
        private IEnumerator SignInRoutine()
        {
            if (_forgetSession)
            {
                AccountService.ForgetSession();
                _forgetSession = false;
            }

            _nicknameLoaded = false;

            // 동의했으면 로그인 전에 수집을 켠다 — 로그인 실패(signInFailed)도 남기려고 (27 6절, PM 2026-11-20)
            Task initialize = AccountService.InitializeAsync();
            yield return Wait(initialize, AccountService.SignInTimeoutSeconds);
            EnsurePlayRecordStarted();

            Task signIn = AccountService.SignInAsync();
            yield return Wait(signIn, AccountService.SignInTimeoutSeconds);
            if (!Succeeded(signIn))
            {
                FailSignIn(signIn);
                yield break;
            }

            Task<string> load = AccountService.LoadNicknameAsync();
            yield return Wait(load, AccountService.RequestTimeoutSeconds);
            if (!Succeeded(load))
            {
                FailSignIn(load);
                yield break;
            }

            EnsurePlayRecordStarted(); // 위에서 초기화가 덜 끝나 못 켰으면 여기서

            _savedNickname = string.IsNullOrEmpty(load.Result) ? null : load.Result;
            _nicknameLoaded = true;
            Debug.Log($"[{nameof(TitlePresenter)}] 로그인 — {AccountService.PlayerId} · 닉네임 {(_savedNickname ?? "없음")}");

            yield return ConfigRoutine();
        }

        // 최소 버전 · 점검 — 네트워크 서버 접속 전에 읽는다. 읽지 못하면 마지막 값으로 진행 (23 7-6)
        private IEnumerator ConfigRoutine()
        {
            yield return FetchConfig();
            if (ServerConfig.NeedsUpdate)
            {
                AnalyticsRecorder.RecordTitleBlocked(AnalyticsRecorder.TitleBlockReason.Update);
                EndLoading();
                popup.ShowUpdate();
                yield break;
            }

            while (ServerConfig.IsMaintenance)
            {
                if (!_maintenanceRecorded)
                {
                    _maintenanceRecorded = true;
                    AnalyticsRecorder.RecordTitleBlocked(AnalyticsRecorder.TitleBlockReason.Maintenance);
                }

                EndLoading();
                popup.ShowMaintenance(ServerConfig.MaintenanceMessage);
                yield return new WaitForSecondsRealtime(ServerConfig.MaintenanceRetrySeconds);
                yield return FetchConfig();
            }

            // 접속 전에 카드 데이터를 맞춘다 — 로비에서 바로 같은 데이터의 방을 찾게 (26-card-data 6-1). 못 맞춰도 저장본 · 빌드 기본본으로 간다
            yield return CardDataService.Instance.SyncRoutine();

            popup.Hide();
            Connect();
        }

        private static IEnumerator FetchConfig()
        {
            Task fetch = ServerConfig.FetchAsync();
            yield return Wait(fetch, ServerConfig.FetchTimeoutSeconds);
            if (!Succeeded(fetch))
                Debug.LogWarning($"[{nameof(TitlePresenter)}] 설정 값 읽기 실패 — 마지막 값으로 진행: {fetch.Exception?.GetBaseException().Message ?? "시간 초과"}");
        }

        // 결과는 ConnectionEvents 로 온다 (시안 connect)
        private void Connect()
        {
            ShowLoading(ConnectingText);
            if (NetworkManager.IsReady)
            {
                _connected = true;
                AfterConnected();
                return;
            }

            NetworkManager.Instance.SetUserId(AccountService.PlayerId);
            if (_savedNickname != null)
                NetworkManager.Instance.SetNickname(_savedNickname);

            _connecting = true;
            NetworkManager.Instance.Connect();
        }

        private void AfterConnected()
        {
            // 닉네임 창에서 정한 이름이 서버 것과 다르면 저장 — 새 계정이거나, 게스트가 지난 닉네임을 고쳤다
            if (_typedNickname != null && _typedNickname != _savedNickname)
            {
                // 「시작」 때 목록이 없어 건너뛴 금칙어 검사 — 설정 값을 받은 지금 저장 전에 한 번 더 (23 6-1 · 8-3)
                if (ServerConfig.HasValues
                    && NicknameValidator.ContainsBannedWord(_typedNickname, ServerConfig.BannedWords, ServerConfig.AllowedWords))
                {
                    StopFlow();
                    _nicknameRejectCount++;
                    EndLoading();
                    sheet.ReturnToNickname(NicknameValidator.GetErrorMessage(NicknameValidationResult.BannedWord));
                    return;
                }

                Run(SaveRoutine());
                return;
            }

            if (_savedNickname != null)
            {
                GoToLobby();
                return;
            }

            // 저장된 로그인인데 서버에 닉네임이 없다 — 추천 이름을 넣어 두고 바로 「시작」 할 수 있게
            EndLoading();
            bottom.Hide();
            sheet.ShowNickname(NicknameValidator.Suggest());
        }

        // 닉네임 저장은 접속 뒤 (23 6-1). 실패하면 닉네임 단계로 — 계정 · 접속은 그대로
        private IEnumerator SaveRoutine()
        {
            ShowLoading(SavingText);
            Task save = AccountService.SaveNicknameAsync(_typedNickname);
            yield return Wait(save, AccountService.RequestTimeoutSeconds);
            if (!Succeeded(save))
            {
                Debug.LogWarning($"[{nameof(TitlePresenter)}] 닉네임 저장 실패: {save.Exception?.GetBaseException().Message ?? "시간 초과"}");
                EndLoading();
                sheet.ReturnToNickname(SaveFailedText);
                yield break;
            }

            AnalyticsRecorder.RecordNicknameSaved(_savedNickname == null, _nicknameRejectCount);
            _nicknameRejectCount = 0;
            _savedNickname = _typedNickname;
            GoToLobby();
        }

        private void GoToLobby()
        {
            if (_leaving)
                return;

            _leaving = true;
            StopFlow();
            NetworkManager.Instance.SetNickname(_savedNickname); // 기기에도 남는다 — 게스트의 다음 닉네임 창에 미리 채울 값

            // 다음 실행에 자동 로그인할지 — 계정(Google · Apple)으로 들어왔을 때만. 자동 로그인으로 들어왔으면 그대로 둔다
            if (!_autoSignIn)
                PlayerPrefs.SetInt(PlayerPrefsKeys.AccountLogin, _method == LoginMethod.Guest ? 0 : 1);

            PlayerPrefs.Save();

            // 카드 데이터 받기(26-card-data)가 아직 없어 늘 거짓이다 — 붙으면 그 결과로
            EndLoading();
            AnalyticsRecorder.RecordLobbyReached(_loadSeconds, _firstRun, false);
            StartCoroutine(LeaveForLobbyRoutine());
        }

        // 타이틀 → 로비 0.3초 페이드 — 로비를 아래에 깔고 타이틀이 위에서 사라진다 (20 3절 #11)
        private IEnumerator LeaveForLobbyRoutine()
        {
            Scene title = gameObject.scene;
            screen.blocksRaycasts = false;
            DisableSceneSingletons(title);

            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePaths.Lobby, LoadSceneMode.Additive);
            yield return load;
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(ScenePaths.Lobby));

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                screen.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }

            SceneManager.UnloadSceneAsync(title);
        }

        // 두 씬이 겹치는 동안 EventSystem · AudioListener 가 둘이 되지 않게 타이틀 것을 끈다
        private static void DisableSceneSingletons(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EventSystem eventSystem in root.GetComponentsInChildren<EventSystem>())
                    eventSystem.gameObject.SetActive(false);

                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>())
                    listener.enabled = false;
            }
        }

        #endregion

        #region 입력

        private void HandleTapped() => OpenStartSheet();

        private void HandleCloseRequested() => CloseSheet();

        // 동의는 이 실행 안에서만 기억한다 — 기기 기록은 처음 로그인할 때 (20 7-11 「기록」)
        private void HandleAgreed()
        {
            _agreed = true;
            if (NeedsPlayRecordChoice)
            {
                popup.ShowPlayRecord();
                return;
            }

            sheet.ShowLogin(IsApplePlatform);
        }

        // 「동의」 · 「동의 안 함」 — 어느 쪽이든 로그인 고르기로. 약관 기록이 이미 있는 기기는 바로 기록한다 (20 7-12)
        private void HandlePlayRecordChosen(bool agreed)
        {
            _playRecordChoice = agreed;
            if (PlayerPrefs.GetInt(PlayerPrefsKeys.TermsAgreed, 0) == 1)
                AnalyticsRecorder.SaveChoice(agreed);

            popup.Hide();
            if (_enterAfterChoice)
            {
                _enterAfterChoice = false;
                Run(EnterRoutine());
                return;
            }

            sheet.ShowLogin(IsApplePlatform);
        }

        // 게스트 — 서버에 아무것도 하지 않고 닉네임부터. 로그인은 「시작」 을 눌러야 한다 (시안 login).
        // 이 기기에서 쓰던 닉네임이 있으면 미리 채운다 — 없으면 추천 이름
        private void HandleGuestRequested()
        {
            _autoSignIn = false;
            _method = LoginMethod.Guest;
            _typedNickname = null;
            _nicknameRejectCount = 0;
            string lastNickname = PlayerPrefs.GetString(PlayerPrefsKeys.PlayerName, string.Empty);
            sheet.ShowNickname(string.IsNullOrEmpty(lastNickname) ? NicknameValidator.Suggest() : lastNickname);
        }

        private void HandleGoogleRequested() => FailExternalLogin(LoginMethod.Google);

        private void HandleAppleRequested() => FailExternalLogin(LoginMethod.Apple);

        private void HandleDiceRequested() => sheet.SetNickname(NicknameValidator.Suggest());

        // 검사(길이 · 문자 · 받아 둔 금칙어 목록)를 통과해야만 로딩으로 넘어간다
        private void HandleStartRequested()
        {
            string nickname = sheet.NicknameText.Trim();
            NicknameValidationResult result = NicknameValidator.Validate(nickname);
            if (result == NicknameValidationResult.Valid && AccountService.IsInitialized && ServerConfig.HasValues
                && NicknameValidator.ContainsBannedWord(nickname, ServerConfig.BannedWords, ServerConfig.AllowedWords))
                result = NicknameValidationResult.BannedWord;

            if (result != NicknameValidationResult.Valid)
            {
                if (result == NicknameValidationResult.BannedWord)
                    _nicknameRejectCount++;

                sheet.ShowNicknameError(NicknameValidator.GetErrorMessage(result));
                return;
            }

            _typedNickname = nickname;
            // 접속까지 끝났을 때만 바로 저장(서버 것과 같으면 바로 로비) — 로그인만 된 상태면 닉네임 읽기 → 설정 값 → 접속을 다시 탄다 (23 3절 #6 · 6-1)
            if (_connected && !_forgetSession)
            {
                AfterConnected();
                return;
            }

            // 첫 로그인 — 여기서 동의를 기기에 기록한다 (23 6-1)
            PlayerPrefs.SetInt(PlayerPrefsKeys.TermsAgreed, 1);
            if (_playRecordChoice.HasValue)
                AnalyticsRecorder.SaveChoice(_playRecordChoice.Value);

            PlayerPrefs.Save();
            ShowLoading(StartingText);
            Run(SignInRoutine());
        }

        // 닉네임 ← 뒤로 — 로그인 고르기로. 닉네임 없이 로그인한 계정은 쓸 수 없어 버린다 (시안 nickBack).
        // 닉네임을 읽지 못한 계정은 버리지 않는다 — 닉네임 · 기록이 있는 기존 게스트 계정일 수 있다
        private void HandleBackRequested()
        {
            _typedNickname = null;
            if (AccountService.IsSignedIn && _nicknameLoaded && _savedNickname == null)
                AbandonAccount();

            sheet.ShowLogin(IsApplePlatform);
        }

        // 다시 시도 = 같은 방법 · 자동 로그인 다시. 연결 실패면 접속만 다시
        private void HandleRetryRequested()
        {
            if (_failKind == FailKind.Connection)
            {
                Connect();
                return;
            }

            if (!_autoSignIn && _method != LoginMethod.Guest)
            {
                FailExternalLogin(_method);
                return;
            }

            ShowLoading(_autoSignIn ? AutoSignInText : StartingText);
            Run(SignInRoutine());
        }

        // 다른 방법으로 = 로그인 창 — 자동 로그인(계정)이 계속 실패해도 빠져나갈 길이라, 그때만 저장된 로그인을 버린다 (시안 other).
        // 게스트의 저장된 로그인은 그 기기의 기록이라 버리지 않는다 (만료된 것은 UGS 가 스스로 지운다)
        private void HandleOtherMethodRequested()
        {
            if (_autoSignIn)
                _forgetSession = true;

            _autoSignIn = false;
            bottom.Hide();
            sheet.ShowLogin(IsApplePlatform);
        }

        private void HandleStoreRequested()
        {
            if (string.IsNullOrEmpty(storeUrl))
            {
                Debug.LogWarning($"[{nameof(TitlePresenter)}] 스토어 주소가 비어 있다 — 패키지 ID 확정 뒤 넣는다");
                return;
            }

            Application.OpenURL(storeUrl);
        }

        // 점검 「확인」 — 팝업은 그대로, 30초마다 다시 읽는 흐름이 풀리면 이어 간다
        private void HandleMaintenanceAcknowledged()
        {
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowToast(MaintenanceToastText);
        }

        private void HandleQuitConfirmed()
        {
            Debug.Log($"[{nameof(TitlePresenter)}] 게임 종료");
            Application.Quit();
        }

        private void HandleQuitCanceled()
        {
            if (_quitFromPlayRecord)
            {
                _quitFromPlayRecord = false;
                popup.ShowPlayRecord();
                return;
            }

            popup.Hide();
        }

        // 뒤로가기: 종료 확인은 닫고 · 닫을 수 없는 팝업은 무시 · 닉네임은 ← 뒤로 · 고르기 창은 닫기 · 터치 대기는 종료 확인
        private void HandleBackKey()
        {
            if (popup.Current == TitlePopup.Quit)
            {
                HandleQuitCanceled();
                return;
            }

            // 플레이 기록 팝업은 닫히지 않는다 — 뒤로가기는 종료 확인 (20 7-12)
            if (popup.Current == TitlePopup.PlayRecord)
            {
                _quitFromPlayRecord = true;
                popup.ShowQuit();
                return;
            }

            if (popup.Current != TitlePopup.None)
                return;

            if (sheet.IsOpen)
            {
                if (sheet.Step == LoginSheetStep.Nickname)
                    HandleBackRequested();
                else if (sheet.Step == LoginSheetStep.Agree || sheet.Step == LoginSheetStep.Login || (sheet.Step == LoginSheetStep.Fail && sheet.IsLoginFailure))
                    CloseSheet();

                return;
            }

            if (bottom.IsWaitingForTap)
                popup.ShowQuit();
        }

        #endregion

        #region 네트워크 이벤트

        private void HandleConnected()
        {
            if (!_connecting)
                return;

            _connecting = false;
            _connected = true;
            AfterConnected();
        }

        private void HandleDisconnected(DisconnectCause cause)
        {
            if (_disconnectingOnPurpose)
            {
                _disconnectingOnPurpose = false;
                return;
            }

            if (_leaving || (!_connecting && !_connected))
                return;

            _connecting = false;
            _connected = false;
            StopFlow();
            (string title, string description) = GetConnectionFailText(cause);
            ShowFail(FailKind.Connection, title, description, cause.ToString());
        }

        #endregion

        #region 화면

        private void OpenStartSheet()
        {
            bottom.Hide();
            if (!_agreed)
            {
                sheet.ShowAgree();
                return;
            }

            sheet.ShowLogin(IsApplePlatform);
            if (NeedsPlayRecordChoice)
                popup.ShowPlayRecord(); // 플레이 기록 팝업이 생기기 전에 동의한 기기 (27 8절)
        }

        private void CloseSheet()
        {
            sheet.Hide();
            bottom.ShowTap();
        }

        // 진행 표시 — 로그인 창에서 시작했으면 창 안, 자동 로그인이면 로고 아래 알약 (시안 setLoad)
        private void ShowLoading(string message)
        {
            if (sheet.IsOpen)
                sheet.ShowLoading(message);
            else
                bottom.ShowLoading(message);

            BeginLoading();
        }

        // lobbyReached.loadSeconds — 로딩 묶음이 뜰 때부터 다른 것으로 바뀔 때까지를 더한다 (27 5-5)
        private void BeginLoading()
        {
            if (_loadingSince < 0f)
                _loadingSince = Time.realtimeSinceStartup;
        }

        private void EndLoading()
        {
            if (_loadingSince < 0f)
                return;

            _loadSeconds += Time.realtimeSinceStartup - _loadingSince;
            _loadingSince = -1f;
        }

        private void ShowFail(FailKind kind, string title, string description, string code)
        {
            EndLoading();
            _failKind = kind;
            if (sheet.IsOpen)
                sheet.ShowFail(title, description, code, kind == FailKind.Login);
            else
                bottom.ShowFail(title, description, code, kind == FailKind.Login);
        }

        private void FailSignIn(Task task)
        {
            Exception exception = task.Exception?.GetBaseException();
            Debug.LogWarning($"[{nameof(TitlePresenter)}] 로그인 실패: {exception?.Message ?? "시간 초과"}");

            bool rejected = AccountService.Classify(exception) == SignInFailure.Rejected;
            (string title, string description) = rejected
                ? (SignInRejectedTitle, SignInRejectedText)
                : (SignInUnavailableTitle, SignInUnavailableText);
            string code = AccountService.GetErrorCodeName(exception);
            AnalyticsRecorder.SignInFailKind kind = rejected
                ? AnalyticsRecorder.SignInFailKind.Rejected
                : AnalyticsRecorder.SignInFailKind.Network;
            EnsurePlayRecordStarted(); // 초기화가 늦게 끝난 실패 — 켜고 나서 남긴다
            AnalyticsRecorder.RecordSignInFailed(kind, code);
            ShowFail(FailKind.Login, title, description, code);
        }

        // Google · Apple 로그인은 SDK 설정 전이라 연결하지 않았다 — 누르면 OS 창 실패와 같이 보인다
        private void FailExternalLogin(LoginMethod method)
        {
            _autoSignIn = false;
            _method = method;
            Debug.LogWarning($"[{nameof(TitlePresenter)}] {method} 로그인은 아직 연결하지 않았다 (SDK 설정 전)");
            ShowFail(FailKind.Login, string.Format(ExternalLoginFailedTitleFormat, method), string.Format(ExternalLoginFailedTextFormat, method), ExternalLoginFailedCode);
        }

        // 연결 실패 사유 (12-title-ui 10-5). 오류 코드 = DisconnectCause 이름 그대로
        private static (string title, string description) GetConnectionFailText(DisconnectCause cause) => cause switch
        {
            DisconnectCause.ExceptionOnConnect or
            DisconnectCause.DnsExceptionOnConnect or
            DisconnectCause.ServerAddressInvalid => (NoInternetTitle, NoInternetText),
            DisconnectCause.ClientTimeout or
            DisconnectCause.ServerTimeout => (ConnectTimeoutTitle, string.Format(ConnectTimeoutTextFormat, PhotonConnection.ConnectTimeoutSeconds)),
            DisconnectCause.MaxCcuReached => (ServerBusyTitle, ServerBusyText),
            _ => (DisconnectedTitle, DisconnectedText),
        };

        #endregion

        // 계정을 버린다 — 다음 로그인은 새 게스트 계정. 접속돼 있으면 끊는다 (식별자가 그 계정의 것이라)
        private void AbandonAccount()
        {
            _forgetSession = true;
            _autoSignIn = false;
            _connecting = false;
            if (!NetworkManager.IsConnected)
                return;

            _connected = false;
            _disconnectingOnPurpose = true;
            NetworkManager.Instance.Disconnect();
        }

        private bool NeedsPlayRecordChoice => !AnalyticsRecorder.HasChoice && !_playRecordChoice.HasValue;

        // 수집 켜기(동의했을 때만) · 타이틀 화면 기록 — UGS 가 준비된 첫 때 한 번. 로그인 전이다 (27 5-5 · 6절, PM 2026-11-20).
        // 앞선 초기화가 아직 도는 중이면 InitializeAsync 가 바로 끝나므로, 준비됐는지는 상태로 본다 — 못 켰으면 다음 자리(로그인 실패 · 닉네임 읽기 뒤)에서 다시
        private void EnsurePlayRecordStarted()
        {
            if (_titleViewRecorded || !AccountService.IsInitialized)
                return;

            AnalyticsRecorder.StartIfAgreed(_playRecordChoice == true);
            _playRecordChoice = null; // consentAccepted 는 한 번만
            _titleViewRecorded = true;
            AnalyticsRecorder.RecordScreenViewed(AnalyticsRecorder.ScreenName.Title);
        }

        private void Run(IEnumerator routine)
        {
            StopFlow();
            _flow = StartCoroutine(routine);
        }

        private void StopFlow()
        {
            if (_flow == null)
                return;

            StopCoroutine(_flow);
            _flow = null;
        }

        private static IEnumerator Wait(Task task, float timeoutSeconds)
        {
            float waited = 0f;
            while (!task.IsCompleted && waited < timeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, AccountService.MaxCountedFrameSeconds);
                yield return null;
            }

            // 시간이 넘은 요청이 나중에 실패해도 처리되지 않은 예외로 남지 않게
            if (!task.IsCompleted)
                task.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static bool Succeeded(Task task) => task.IsCompleted && !task.IsFaulted && !task.IsCanceled;
    }
}
