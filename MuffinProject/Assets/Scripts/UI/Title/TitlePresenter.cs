using System;
using System.Collections;
using System.Threading.Tasks;
using Chapchu.Account;
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
    /// 타이틀 흐름 — 약관 동의 → 로그인 고르기 → (게스트) 닉네임 → 로그인 → 설정 값 → 접속 → 닉네임 저장 → 로비.
    /// 저장된 로그인이 있으면 누르지 않아도 자동 로그인 → 로비. 뷰와 계정(UGS) · 네트워크 · 씬 전환을 잇는 유일한 파일이다.
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

        // 앱이 뒤로 갔다 돌아온 프레임의 큰 시간은 세지 않는다 — 시간 제한은 앱이 앞에 있는 동안만 센다 (20 10절)
        private const float MaxCountedFrameSeconds = 0.1f;

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
        private bool _forgetSession;    // 다음 로그인은 저장된 로그인을 버리고 새 게스트 계정으로
        private LoginMethod _method;
        private string _savedNickname;  // 서버에 저장된 닉네임 — 없으면 null
        private string _typedNickname;  // 이번에 입력해 「시작」 한 닉네임 — 없으면 null
        private FailKind _failKind;
        private Coroutine _flow;
        private bool _connecting;       // 네트워크 서버 접속 결과를 기다리는 중
        private bool _connected;        // 이 타이틀에서 접속을 마쳤다 — 그 뒤 끊기면 연결 실패
        private bool _disconnectingOnPurpose;
        private bool _leaving;

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

            ConnectionEvents.OnConnected -= HandleConnected;
            ConnectionEvents.OnDisconnected -= HandleDisconnected;
        }

        private void Start()
        {
            sheet.Hide();
            popup.Hide();

            // 동의 기록이 없으면 어디에도 접속하지 않고 「화면을 눌러 시작」 (23 3절 #9)
            _agreed = PlayerPrefs.GetInt(PlayerPrefsKeys.TermsAgreed, 0) == 1;
            if (!_agreed)
            {
                bottom.ShowTap();
                return;
            }

            bottom.Hide();
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

        // 저장된 로그인이 있으면 누르지 않아도 자동 로그인 (시안 enter)
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
            bottom.ShowLoading(AutoSignInText);
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

            _savedNickname = string.IsNullOrEmpty(load.Result) ? null : load.Result;
            Debug.Log($"[{nameof(TitlePresenter)}] 로그인 — {AccountService.PlayerId} · 닉네임 {(_savedNickname ?? "없음")}");

            yield return ConfigRoutine();
        }

        // 최소 버전 · 점검 — 네트워크 서버 접속 전에 읽는다. 읽지 못하면 마지막 값으로 진행 (23 7-6)
        private IEnumerator ConfigRoutine()
        {
            yield return FetchConfig();
            if (ServerConfig.NeedsUpdate)
            {
                popup.ShowUpdate();
                yield break;
            }

            while (ServerConfig.IsMaintenance)
            {
                popup.ShowMaintenance(ServerConfig.MaintenanceMessage);
                yield return new WaitForSecondsRealtime(ServerConfig.MaintenanceRetrySeconds);
                yield return FetchConfig();
            }

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
            if (_savedNickname != null)
            {
                GoToLobby();
                return;
            }

            if (_typedNickname != null)
            {
                Run(SaveRoutine());
                return;
            }

            // 저장된 로그인인데 서버에 닉네임이 없다 — 추천 이름을 넣어 두고 바로 「시작」 할 수 있게
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
                sheet.ReturnToNickname(SaveFailedText);
                yield break;
            }

            _savedNickname = _typedNickname;
            GoToLobby();
        }

        private void GoToLobby()
        {
            if (_leaving)
                return;

            _leaving = true;
            StopFlow();
            NetworkManager.Instance.SetNickname(_savedNickname);
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
            sheet.ShowLogin(IsApplePlatform);
        }

        // 게스트 — 서버에 아무것도 하지 않고 닉네임부터. 계정은 「시작」 을 눌러야 만든다 (시안 login)
        private void HandleGuestRequested()
        {
            _autoSignIn = false;
            _method = LoginMethod.Guest;
            _typedNickname = null;
            sheet.ShowNickname(NicknameValidator.Suggest());
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
                sheet.ShowNicknameError(NicknameValidator.GetErrorMessage(result));
                return;
            }

            _typedNickname = nickname;
            // 접속까지 끝났을 때만 바로 저장 — 로그인만 된 상태면 닉네임 읽기 → 설정 값 → 접속을 다시 탄다 (23 3절 #6 · 6-1)
            if (_connected && !_forgetSession)
            {
                Run(SaveRoutine());
                return;
            }

            // 첫 로그인 — 여기서 동의를 기기에 기록한다 (23 6-1)
            PlayerPrefs.SetInt(PlayerPrefsKeys.TermsAgreed, 1);
            PlayerPrefs.Save();
            sheet.ShowLoading(StartingText);
            Run(SignInRoutine());
        }

        // 닉네임 ← 뒤로 — 로그인 고르기로. 닉네임 없이 로그인한 계정은 쓸 수 없어 버린다 (시안 nickBack)
        private void HandleBackRequested()
        {
            _typedNickname = null;
            if (AccountService.IsSignedIn && _savedNickname == null)
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

        // 다른 방법으로 = 로그인 창 — 자동 로그인이 계속 실패해도 빠져나갈 길. 다음 로그인은 새 계정 (시안 other)
        private void HandleOtherMethodRequested()
        {
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

        private void HandleQuitCanceled() => popup.Hide();

        // 뒤로가기: 종료 확인은 닫고 · 닫을 수 없는 팝업은 무시 · 닉네임은 ← 뒤로 · 고르기 창은 닫기 · 터치 대기는 종료 확인
        private void HandleBackKey()
        {
            if (popup.Current == TitlePopup.Quit)
            {
                popup.Hide();
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
            if (_agreed)
                sheet.ShowLogin(IsApplePlatform);
            else
                sheet.ShowAgree();
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
        }

        private void ShowFail(FailKind kind, string title, string description, string code)
        {
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

            (string title, string description) = AccountService.Classify(exception) == SignInFailure.Rejected
                ? ("로그인 실패", "로그인이 만료됐어요. 다시 시도해 주세요.")
                : ("로그인할 수 없음", "연결을 확인하고 다시 시도해 주세요.");
            ShowFail(FailKind.Login, title, description, AccountService.GetErrorCodeName(exception));
        }

        // Google · Apple 로그인은 SDK 설정 전이라 연결하지 않았다 — 누르면 OS 창 실패와 같이 보인다
        private void FailExternalLogin(LoginMethod method)
        {
            _autoSignIn = false;
            _method = method;
            Debug.LogWarning($"[{nameof(TitlePresenter)}] {method} 로그인은 아직 연결하지 않았다 (SDK 설정 전)");
            ShowFail(FailKind.Login, $"{method} 로그인 실패", $"{method} 계정으로 로그인하지 못했어요. 다른 방법으로 시작해 주세요.", ExternalLoginFailedCode);
        }

        // 연결 실패 사유 (12-title-ui 10-5 — 문구는 시안의 해요체). 오류 코드 = DisconnectCause 이름 그대로
        private static (string title, string description) GetConnectionFailText(DisconnectCause cause) => cause switch
        {
            DisconnectCause.ExceptionOnConnect or
            DisconnectCause.DnsExceptionOnConnect or
            DisconnectCause.ServerAddressInvalid => ("인터넷 연결 없음", "Wi-Fi 또는 데이터 연결을 확인해 주세요."),
            DisconnectCause.ClientTimeout or
            DisconnectCause.ServerTimeout => ("연결 시간 초과", "서버가 15초 안에 응답하지 않았어요. 다시 시도해 주세요."),
            DisconnectCause.MaxCcuReached => ("서버 혼잡", "접속자가 많아요. 잠시 후 다시 시도해 주세요."),
            _ => ("연결 끊김", "연결이 끊어졌어요. 다시 시도해 주세요."),
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
                waited += Mathf.Min(Time.unscaledDeltaTime, MaxCountedFrameSeconds);
                yield return null;
            }

            // 시간이 넘은 요청이 나중에 실패해도 처리되지 않은 예외로 남지 않게
            if (!task.IsCompleted)
                task.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static bool Succeeded(Task task) => task.IsCompleted && !task.IsFaulted && !task.IsCanceled;
    }
}
