using System;
using System.Collections;
using System.Threading.Tasks;
using Chapchu.Account;
using Chapchu.Network;
using Chapchu.UI.Title;
using UnityEngine;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 닉네임 변경 · 계정 삭제 — 프로필 · 설정 창에서 시작해 입력 창으로 받는다. 닉네임은 서버(Cloud Code, AccountService)가 검사해 저장한다.
    /// 기준: docs/systems/17-lobby-ui-full.md 5-2 · 12-title-ui.md 4-2 · 23-login.md 3절 #5 · 6-4 · 7-8 · 8절
    /// </summary>
    public class LobbyAccountPresenter : MonoBehaviour
    {
        private const string OkText = "확인";
        private const string CancelText = "취소";
        private const string RenamingText = "바꾸는 중…";
        private const string RenamedToast = "닉네임을 바꿨어요.";
        private const string RenamedLateToast = "닉네임이 바뀌었어요.";
        private const string CloseText = "닫기";
        private const string CooldownTitle = "아직 바꿀 수 없어요";
        private const string CooldownMessageFormat = "닉네임은 일주일에 한 번 바꿀 수 있어요. {0} 뒤에 다시 바꿀 수 있어요.";
        private const string DaysHoursFormat = "{0}일 {1}시간";
        private const string DaysFormat = "{0}일";
        private const string HoursFormat = "{0}시간";
        private const string NoCouponTitle = "변경 쿠폰이 없어요";
        private const string NoCouponMessage = "닉네임을 바꾸려면 변경 쿠폰 1장이 필요해요. 상점에서 살 수 있어요.";
        private const string CouponNoteFormat = "변경 쿠폰 1장을 써요 (남은 쿠폰 {0}장). 바꾸면 일주일 동안 다시 바꿀 수 없어요.";
        private const string RenameFailedTitle = "닉네임 변경 실패";
        private const string DeleteTitle = "계정 삭제";
        private const string DeleteMessage = "계정을 삭제할까요? 친구 · 통계 · 보유 아이템이 모두 지워지고 되돌릴 수 없어요.";
        private const string DeleteWord = "삭제";
        private const string DeleteWordError = "'삭제'라고 똑같이 입력해야 지워져요.";
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

        // 쿨다운 → 쿠폰 순으로 먼저 본다 — 서버가 다시 본다 (17 5-2). 아직 못 읽었으면 바로 입력 창, 판단은 서버에
        private void HandleNicknameChangeRequested()
        {
            _nicknameRejectCount = 0;
            NicknameState state = AccountService.Nickname;
            if (state?.Nickname != null)
            {
                TimeSpan left = state.CooldownLeft(DateTimeOffset.UtcNow);
                if (left > TimeSpan.Zero)
                {
                    ShowCooldown(left);
                    return;
                }

                if (state.Coupons < 1)
                {
                    ShowNoCoupon();
                    return;
                }
            }

            ShowNicknameInput(NetworkManager.Nickname);
        }

        private void ShowNicknameInput(string value)
        {
            NicknameState state = AccountService.Nickname;
            string note = state?.Nickname != null ? string.Format(CouponNoteFormat, state.Coupons) : null;
            modal.ShowInput(LobbyInputKind.Nickname, value, note);
        }

        private void ShowCooldown(TimeSpan left) => modal.ShowWarn(CooldownTitle, string.Format(CooldownMessageFormat, FormatLeft(left)));

        // 「상점으로」 는 상점에 쿠폰 칸(17 7-7 「기타」)이 생기면 붙인다 — 지금은 닫기만
        private void ShowNoCoupon() => modal.ShowWarn(NoCouponTitle, NoCouponMessage, CloseText);

        // 올림 시간 단위 — 0시간이면 「n일」 만, 하루가 안 되면 「n시간」 만 (17 8절)
        private static string FormatLeft(TimeSpan left)
        {
            int hours = (int)Math.Ceiling(left.TotalHours);
            int days = hours / 24;
            hours %= 24;
            if (days == 0)
                return string.Format(HoursFormat, hours);

            return hours == 0 ? string.Format(DaysFormat, days) : string.Format(DaysHoursFormat, days, hours);
        }

        private void HandleDeleteAccountRequested()
            => modal.ShowWarn(DeleteTitle, DeleteMessage, OkText, CancelText, () => modal.ShowInput(LobbyInputKind.DeleteAccount));

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
            Task<NicknameSave> save = AccountService.IsSignedIn
                ? AccountService.SetNicknameAsync(nickname)
                : Task.FromException<NicknameSave>(new InvalidOperationException("로그인하지 않음"));
            float waited = 0f;
            while (!save.IsCompleted && waited < AccountService.RequestTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, AccountService.MaxCountedFrameSeconds);
                yield return null;
            }

            modal.HideLoading();
            if (!save.IsCompleted)
            {
                // 실패로 안내하되, 저장이 늦게 성공하면 서버 값대로 바꾼다 (17 5-2)
                Debug.LogWarning($"[{nameof(LobbyAccountPresenter)}] 닉네임 저장 시간 초과");
                modal.ShowWarn(RenameFailedTitle, RetryLaterText);
                StartCoroutine(LateRenameRoutine(save));
                yield break;
            }

            if (save.IsFaulted || save.IsCanceled)
            {
                Debug.LogWarning($"[{nameof(LobbyAccountPresenter)}] 닉네임 저장 실패: {save.Exception?.GetBaseException().Message}");
                modal.ShowWarn(RenameFailedTitle, RetryLaterText);
                yield break;
            }

            NicknameSave result = save.Result;
            switch (result.Result)
            {
                case NicknameSave.Ok:
                case NicknameSave.Same:
                    ApplyRenamed(nickname, RenamedToast);
                    break;
                case NicknameSave.CooldownLeft:
                    ShowCooldown(TimeSpan.FromSeconds(result.RemainingSeconds));
                    break;
                case NicknameSave.NoCoupon:
                    ShowNoCoupon();
                    break;
                default:
                    // 입력 때문에 거절(문자 · 금칙어 · 중복)이면 입력 창으로 돌아가 에러 줄 — 쿠폰은 그대로
                    NicknameValidationResult rejected = NicknameValidator.FromServer(result.Result);
                    if (rejected == NicknameValidationResult.Valid)
                    {
                        Debug.LogWarning($"[{nameof(LobbyAccountPresenter)}] 닉네임 저장 거절: {result.Result}");
                        modal.ShowWarn(RenameFailedTitle, RetryLaterText);
                        break;
                    }

                    if (rejected == NicknameValidationResult.BannedWord)
                        _nicknameRejectCount++;

                    ShowNicknameInput(nickname);
                    modal.SetInputError(NicknameValidator.GetErrorMessage(rejected));
                    break;
            }
        }

        // 시간이 넘은 요청이 늦게 끝나면 — 성공이면 서버 값대로 바꾸고 알린다. 실패는 이미 안내했다
        private IEnumerator LateRenameRoutine(Task<NicknameSave> save)
        {
            while (!save.IsCompleted)
                yield return null;

            if (save.IsFaulted || save.IsCanceled)
            {
                save.Exception?.Handle(_ => true); // 처리되지 않은 예외로 남지 않게
                yield break;
            }

            if (save.Result.Result == NicknameSave.Ok)
                ApplyRenamed(save.Result.Nickname, RenamedLateToast);
        }

        private void ApplyRenamed(string nickname, string toast)
        {
            AnalyticsRecorder.RecordNicknameSaved(false, _nicknameRejectCount);
            _nicknameRejectCount = 0;
            NetworkManager.Instance.SetNickname(nickname);
            home.SetProfile(nickname, lobby.Data.MyCode);
            if (profile.IsOpen)
                profile.SetNickname(nickname);

            fx.Toast(toast);
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
