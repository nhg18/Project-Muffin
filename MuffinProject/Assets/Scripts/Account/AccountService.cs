using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;

namespace Chapchu.Account
{
    /// <summary>로그인 실패를 화면 문구로 나누는 갈래 (20-title-ui-full.md 8절)</summary>
    public enum SignInFailure
    {
        /// <summary>연결 문제 · 시간 초과 — 「로그인할 수 없음」</summary>
        Network,
        /// <summary>세션 만료 · 서버 거절 — 「로그인 실패」</summary>
        Rejected,
    }

    /// <summary>내 닉네임 · 변경 쿠폰 · 쿨다운 (23 3절 #5 · 7-8). 서버가 쓴 값을 읽은 것이다.</summary>
    public sealed class NicknameState
    {
        /// <summary>쿠폰 칸이 없을 때의 장수 — 계정 생성 때 1장 무료 (23 7-8)</summary>
        public const int FreeCoupons = 1;

        /// <summary>바꾼 뒤 다시 바꿀 수 없는 시간 — 7일 (23 4절)</summary>
        public static readonly TimeSpan Cooldown = TimeSpan.FromHours(168);

        public NicknameState(string nickname, int coupons, long changedAtMs, string legacyNickname)
        {
            Nickname = nickname;
            Coupons = coupons;
            ChangedAtMs = changedAtMs;
            LegacyNickname = legacyNickname;
        }

        /// <summary>서버에 정해진 닉네임. 아직 없으면 null — 처음 정하기 (쿠폰 · 쿨다운 없음)</summary>
        public string Nickname { get; }

        public int Coupons { get; }

        /// <summary>마지막으로 바꾼 때 (유닉스 ms). 한 번도 안 바꿨으면 0 — 처음 정하기는 세지 않는다</summary>
        public long ChangedAtMs { get; }

        /// <summary>2026-11-26 전에 클라이언트가 직접 쓴 옛 닉네임 — 처음 정하기 창에 미리 채운다. 없으면 null</summary>
        public string LegacyNickname { get; }

        /// <summary>다시 바꿀 수 있을 때까지 남은 시간. 바꿀 수 있으면 0 이하</summary>
        public TimeSpan CooldownLeft(DateTimeOffset now)
            => ChangedAtMs <= 0 ? TimeSpan.Zero : DateTimeOffset.FromUnixTimeMilliseconds(ChangedAtMs) + Cooldown - now;
    }

    /// <summary>Cloud Code 「닉네임 저장」 의 답 (server/cloud-code/SetNickname.js). Result 는 아래 상수 중 하나.</summary>
    public sealed class NicknameSave
    {
        public const string Ok = "ok";
        public const string Same = "same";
        public const string Invalid = "invalid";
        public const string Banned = "banned";
        public const string Taken = "taken";
        public const string CooldownLeft = "cooldown";
        public const string NoCoupon = "noCoupon";
        public const string Error = "error";

        public string Result { get; set; }
        public string Nickname { get; set; }
        public int Coupons { get; set; }
        public long ChangedAt { get; set; }

        /// <summary>Result 가 cooldown 일 때 남은 초</summary>
        public long RemainingSeconds { get; set; }
    }

    /// <summary>
    /// UGS 계정 창구 — 게스트(익명) 로그인 · 세션 복원 · 닉네임(Cloud Save 읽기 · Cloud Code 쓰기). 기준 문서: docs/systems/23-login.md 6절 · 7-4 · 7-8
    /// 상태는 UGS 쪽 싱글톤이 갖고, 여기는 부르는 길만 모은다. 동의 전에는 부르지 않는다 (23 3절 #9).
    /// </summary>
    public static class AccountService
    {
        /// <summary>로그인(익명 · 세션 복원) 시간 제한 (23 4절)</summary>
        public const float SignInTimeoutSeconds = 15f;

        /// <summary>닉네임 읽기 · 저장 요청 시간 제한 (20 4절)</summary>
        public const float RequestTimeoutSeconds = 15f;

        /// <summary>
        /// 요청 시간 제한을 셀 때 한 프레임에서 더하는 최대 초 — 앱이 뒤로 갔다 돌아온 프레임의 큰 시간은 세지 않는다.
        /// 시간 제한은 앱이 앞에 있는 동안만 센다 (20 10절). 로그인 · 닉네임 저장 · 방 요청이 같이 쓴다.
        /// </summary>
        public const float MaxCountedFrameSeconds = 0.1f;

        public static bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;

        public static bool IsSignedIn => IsInitialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>기기에 저장된 로그인(세션)이 있는지. 초기화 전에는 false.</summary>
        public static bool HasSavedSession => IsInitialized && AuthenticationService.Instance.SessionTokenExists;

        /// <summary>UGS 플레이어 ID — 네트워크 서버의 플레이어 식별자로도 쓴다 (23 3절 #10). 로그인 전에는 null.</summary>
        public static string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

        private static Task _signIn; // 돌고 있는 로그인 요청

        public static async Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();
        }

        /// <summary>저장된 세션이 있으면 그 계정으로, 없으면 새 게스트 계정으로 로그인한다.</summary>
        public static async Task SignInAsync()
        {
            await InitializeAsync();
            if (AuthenticationService.Instance.IsSignedIn)
                return;

            // 시간이 넘은 로그인 요청이 아직 돌고 있으면 새로 부르지 않고 이어서 기다린다 — 겹쳐 부르면 UGS 가 상태 오류로 바로 실패시킨다
            if (_signIn == null || _signIn.IsCompleted)
                _signIn = AuthenticationService.Instance.SignInAnonymouslyAsync();

            await _signIn;
        }

        /// <summary>
        /// 기기에 저장된 로그인을 버린다 — 다음 SignInAsync 는 새 게스트 계정이 된다.
        /// 계정 자체는 서버에 남지만 이 기기에서는 다시 들어갈 수 없다.
        /// </summary>
        public static void ForgetSession()
        {
            if (!IsInitialized)
                return;

            _signIn = null; // 돌고 있던 요청은 버린 계정의 것 — 다음 로그인은 새로 부른다
            Nickname = null;
            AuthenticationService.Instance.SignOut(true);
        }

        /// <summary>마지막으로 읽거나 저장한 내 닉네임 · 쿠폰 · 변경 시각. 로그인 전이거나 아직 못 읽었으면 null.</summary>
        public static NicknameState Nickname { get; private set; }

        /// <summary>
        /// 내 닉네임 · 변경 쿠폰 · 마지막 변경 시각을 보호 칸(서버만 쓴다)에서 읽는다 (23 7-8). 결과는 Nickname 에도 남는다.
        /// 보호 칸에 닉네임이 없으면 2026-11-26 전에 클라이언트가 직접 쓴 옛 칸을 LegacyNickname 으로 읽는다 — 처음 정하기 창에 미리 채울 값.
        /// </summary>
        public static async Task<NicknameState> LoadNicknameAsync()
        {
            var keys = new HashSet<string> { CloudKeys.Nickname, CloudKeys.NicknameCoupons, CloudKeys.NicknameChangedAt };
            Dictionary<string, Item> items = await CloudSaveService.Instance.Data.Player.LoadAsync(keys, new LoadOptions(new ProtectedReadAccessClassOptions()));

            string nickname = items.TryGetValue(CloudKeys.Nickname, out Item name) ? name.Value.GetAs<string>() : null;
            int coupons = items.TryGetValue(CloudKeys.NicknameCoupons, out Item count) ? count.Value.GetAs<int>() : NicknameState.FreeCoupons;
            long changedAt = items.TryGetValue(CloudKeys.NicknameChangedAt, out Item time) ? time.Value.GetAs<long>() : 0L;

            string legacy = null;
            if (string.IsNullOrEmpty(nickname))
            {
                Dictionary<string, Item> old = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKeys.Nickname });
                legacy = old.TryGetValue(CloudKeys.Nickname, out Item oldName) ? oldName.Value.GetAs<string>() : null;
            }

            Nickname = new NicknameState(string.IsNullOrEmpty(nickname) ? null : nickname, coupons, changedAt, string.IsNullOrEmpty(legacy) ? null : legacy);
            return Nickname;
        }

        /// <summary>
        /// 닉네임 정하기 · 바꾸기 — Cloud Code 「닉네임 저장」 만 쓴다 (23 7-8 · 8-3). 서버가 길이 · 문자 · 금칙어 · 중복 · 쿠폰 · 쿨다운을 다시 본다.
        /// 거절은 예외가 아니라 결과 값으로 온다. 성공하면 Nickname 도 바꾼다. 연결 문제는 예외.
        /// </summary>
        public static async Task<NicknameSave> SetNicknameAsync(string nickname)
        {
            var args = new Dictionary<string, object> { { "nickname", nickname } };
            NicknameSave save = await CloudCodeService.Instance.CallEndpointAsync<NicknameSave>(CloudKeys.SetNicknameScript, args);
            if (save.Result == NicknameSave.Ok)
                Nickname = new NicknameState(save.Nickname, save.Coupons, save.ChangedAt, null);
            else if (save.Result == NicknameSave.Same && Nickname != null) // 시간이 넘은 지난 요청이 이미 저장했다
                Nickname = new NicknameState(nickname, Nickname.Coupons, Nickname.ChangedAtMs, null);

            return save;
        }

        /// <summary>실패를 화면 갈래로 나눈다. 시간 초과(exception null)는 Network.</summary>
        public static SignInFailure Classify(Exception exception)
        {
            if (exception is RequestFailedException failed && failed.ErrorCode != CommonErrorCodes.TransportError && failed.ErrorCode != CommonErrorCodes.Timeout)
                return SignInFailure.Rejected;

            return SignInFailure.Network;
        }

        /// <summary>오류 코드 칸에 보일 이름 — UGS 오류 코드 상수 이름 그대로 (20 8절). 시간 초과(exception null)는 Timeout.</summary>
        public static string GetErrorCodeName(Exception exception)
        {
            if (exception == null)
                return nameof(CommonErrorCodes.Timeout);

            if (exception is not RequestFailedException failed)
                return exception.GetType().Name;

            return FindConstantName(typeof(AuthenticationErrorCodes), failed.ErrorCode)
                ?? FindConstantName(typeof(CommonErrorCodes), failed.ErrorCode)
                ?? failed.ErrorCode.ToString();
        }

        private static string FindConstantName(Type holder, int code)
        {
            foreach (FieldInfo field in holder.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // AuthenticationErrorCodes 는 const 가 아니라 static readonly 다. MinValue 는 범위 표시라 이름이 아니다 (ClientInvalidUserState 와 같은 값)
                if (field.FieldType == typeof(int) && field.Name != nameof(AuthenticationErrorCodes.MinValue) && (int)field.GetValue(null) == code)
                    return field.Name;
            }

            return null;
        }
    }
}
