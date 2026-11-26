using System.Collections;
using System.Threading.Tasks;
using Chapchu.Account;
using Chapchu.Network;
using Chapchu.UI.Title;
using UnityEngine;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 닉네임 변경 · 계정 삭제 — 프로필 · 설정 창에서 시작해 입력 창으로 받는다. 닉네임은 서버(AccountService)에 저장한다.
    /// 기준: docs/systems/17-lobby-ui-full.md 5-2 · 12-title-ui.md 4-2 · 23-login.md 6-4 · 8절
    /// </summary>
    public class LobbyAccountPresenter : MonoBehaviour
    {
        private const string DeleteOkText = "삭제"; // 확인 창 오른쪽 버튼 = 하는 일 이름 (시안 v3.23)
        private const string CancelText = "취소";
        private const string RenamingText = "바꾸는 중…";
        private const string RenameNoteFormat = "변경 쿠폰 1장을 써요 (남은 쿠폰 {0}장). 바꾸면 일주일 동안 다시 바꿀 수 없어요.";
        private const string RenamedToast = "닉네임을 바꿨어요.";
        private const string RenameFailedTitle = "닉네임 변경 실패";
        private const string DeleteTitle = "계정 삭제";
        private const string DeleteMessage = "계정을 삭제할까요? 친구 · 통계 · 보유 아이템이 모두 지워지고 되돌릴 수 없어요.";
        private const string DeleteWord = "삭제";
        private const string DeleteWordError = "「삭제」와 똑같이 입력해야 지워져요.";
        private const string RetryLaterText = "잠시 후 다시 시도해 주세요.";

        [SerializeField] private LobbyPresenter lobby;
        [SerializeField] private LobbyHomeView home;
        [SerializeField] private LobbyProfilePopupView profile;
        [SerializeField] private LobbySettingsPopupView settings;
        [SerializeField] private LobbyModalView modal;
        [SerializeField] private LobbyFxView fx;

        private int _nicknameRejectCount; // 이번 닉네임을 저장하기 전 금칙어로 거절된 횟수 (27 5-1 nicknameSaved)

        private void Awake()
        {
            if (lobby == null || home == null || profile == null || settings == null || modal == null || fx == null)
                Debug.LogError($"[{nameof(LobbyAccountPresenter)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            profile.OnChangeRequested += HandleNicknameChangeRequested;
            settings.OnDeleteAccountRequested += HandleDeleteAccountRequested;
            modal.OnInputSubmitted += HandleInputSubmitted;
        }

        private void OnDisable()
        {
            profile.OnChangeRequested -= HandleNicknameChangeRequested;
            settings.OnDeleteAccountRequested -= HandleDeleteAccountRequested;
            modal.OnInputSubmitted -= HandleInputSubmitted;
        }

        private void HandleNicknameChangeRequested()
        {
            _nicknameRejectCount = 0;
            modal.ShowInput(LobbyInputKind.Nickname, NetworkManager.Nickname, string.Format(RenameNoteFormat, lobby.Data.NicknameCoupons));
        }

        private void HandleDeleteAccountRequested()
            => modal.ShowWarn(DeleteTitle, DeleteMessage, DeleteOkText, CancelText, () => modal.ShowInput(LobbyInputKind.DeleteAccount), LobbyWarnKind.Danger);

        // 입력 창은 방 코드에도 쓰인다 — 닉네임 · 계정 삭제일 때만 받는다
        private void HandleInputSubmitted(LobbyInputKind kind, string value)
        {
            if (kind == LobbyInputKind.Nickname)
                SubmitNickname(value);
            else if (kind == LobbyInputKind.DeleteAccount)
                SubmitDeleteAccount(value);
        }

        // 타이틀과 같은 검사 (12-title-ui 4-2 · 금칙어 23 8절) → 통과하면 서버 저장 (17 5-2)
        private void SubmitNickname(string nickname)
        {
            NicknameValidationResult result = NicknameValidator.Validate(nickname);
            if (result == NicknameValidationResult.Valid && AccountService.IsInitialized && ServerConfig.HasValues
                && NicknameValidator.ContainsBannedWord(nickname, ServerConfig.BannedWords, ServerConfig.AllowedWords))
                result = NicknameValidationResult.BannedWord;

            if (result != NicknameValidationResult.Valid)
            {
                if (result == NicknameValidationResult.BannedWord)
                    _nicknameRejectCount++;

                modal.SetInputError(NicknameValidator.GetErrorMessage(result));
                return;
            }

            modal.HideInput();
            if (nickname != NetworkManager.Nickname)
                StartCoroutine(RenameRoutine(nickname));
        }

        private IEnumerator RenameRoutine(string nickname)
        {
            modal.ShowLoading(RenamingText);
            Task save = AccountService.IsSignedIn ? AccountService.SaveNicknameAsync(nickname) : Task.FromException(new System.InvalidOperationException("로그인하지 않음"));
            float waited = 0f;
            while (!save.IsCompleted && waited < AccountService.RequestTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, AccountService.MaxCountedFrameSeconds);
                yield return null;
            }

            modal.HideLoading();
            if (!save.IsCompleted || save.IsFaulted || save.IsCanceled)
            {
                // 시간이 넘은 요청이 나중에 실패해도 처리되지 않은 예외로 남지 않게
                save.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                Debug.LogWarning($"[{nameof(LobbyAccountPresenter)}] 닉네임 저장 실패: {save.Exception?.GetBaseException().Message ?? "시간 초과"}");
                modal.ShowWarn(RenameFailedTitle, RetryLaterText);
                yield break;
            }

            AnalyticsRecorder.RecordNicknameSaved(false, _nicknameRejectCount);
            _nicknameRejectCount = 0;
            NetworkManager.Instance.SetNickname(nickname);
            home.SetProfile(nickname);
            if (profile.IsOpen)
                profile.SetNickname(nickname);

            fx.Toast(RenamedToast);
        }

        // 「삭제」 를 똑같이 입력해야 넘어간다 (23 6-4). 서버의 계정 삭제 요청이 아직 없어, 문서의 실패 갈래(계정은 그대로)로 끝난다
        private void SubmitDeleteAccount(string typed)
        {
            if (typed != DeleteWord)
            {
                modal.SetInputError(DeleteWordError);
                return;
            }

            modal.HideInput();
            Debug.LogWarning($"[{nameof(LobbyAccountPresenter)}] 계정 삭제 요청(AccountService)이 아직 없어 삭제하지 않는다 — 23-login 6-4.");
            modal.ShowWarn(DeleteTitle, RetryLaterText);
        }
    }
}
