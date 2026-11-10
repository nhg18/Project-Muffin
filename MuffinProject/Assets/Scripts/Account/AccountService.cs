using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
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

    /// <summary>
    /// UGS 계정 창구 — 게스트(익명) 로그인 · 세션 복원 · 닉네임(Cloud Save). 기준 문서: docs/systems/23-login.md 6절 · 7-4
    /// 상태는 UGS 쪽 싱글톤이 갖고, 여기는 부르는 길만 모은다. 동의 전에는 부르지 않는다 (23 3절 #9).
    /// </summary>
    public static class AccountService
    {
        /// <summary>로그인(익명 · 세션 복원) 시간 제한 (23 4절)</summary>
        public const float SignInTimeoutSeconds = 15f;

        /// <summary>닉네임 읽기 · 저장 요청 시간 제한 (20 4절)</summary>
        public const float RequestTimeoutSeconds = 15f;

        public static bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;

        public static bool IsSignedIn => IsInitialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>기기에 저장된 로그인(세션)이 있는지. 초기화 전에는 false.</summary>
        public static bool HasSavedSession => IsInitialized && AuthenticationService.Instance.SessionTokenExists;

        /// <summary>UGS 플레이어 ID — 네트워크 서버의 플레이어 식별자로도 쓴다 (23 3절 #10). 로그인 전에는 null.</summary>
        public static string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

        public static async Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();
        }

        /// <summary>저장된 세션이 있으면 그 계정으로, 없으면 새 게스트 계정으로 로그인한다.</summary>
        public static async Task SignInAsync()
        {
            await InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// 기기에 저장된 로그인을 버린다 — 다음 SignInAsync 는 새 게스트 계정이 된다.
        /// 계정 자체는 서버에 남지만 이 기기에서는 다시 들어갈 수 없다.
        /// </summary>
        public static void ForgetSession()
        {
            if (!IsInitialized)
                return;

            AuthenticationService.Instance.SignOut(true);
        }

        /// <summary>서버에 저장된 닉네임. 없으면 null.</summary>
        public static async Task<string> LoadNicknameAsync()
        {
            Dictionary<string, Item> items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKeys.Nickname });
            return items.TryGetValue(CloudKeys.Nickname, out Item item) ? item.Value.GetAs<string>() : null;
        }

        /// <summary>
        /// 닉네임 저장. 23 8-3 은 Cloud Code 가 금칙어를 다시 검사한 뒤 쓰도록 정했지만, Cloud Code 가 아직 없어 클라이언트가 직접 쓴다.
        /// </summary>
        public static Task SaveNicknameAsync(string nickname)
            => CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { CloudKeys.Nickname, nickname } });

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
                if (field.IsLiteral && field.FieldType == typeof(int) && (int)field.GetRawConstantValue() == code)
                    return field.Name;
            }

            return null;
        }
    }
}
