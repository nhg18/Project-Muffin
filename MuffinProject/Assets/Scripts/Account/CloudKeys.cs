namespace Chapchu.Account
{
    /// <summary>UGS 문자열 키 모음 (CODE_CONVENTION 5절) — Cloud Save · Remote Config · Cloud Code. 기준 문서: docs/systems/23-login.md 7-4 · 7-6 · 7-8 · 8-1</summary>
    public static class CloudKeys
    {
        /// <summary>Cloud Save 보호 칸 — 닉네임 (23 7-8). 서버만 쓴다. 같은 이름의 기본 칸은 2026-11-26 전에 클라이언트가 직접 쓰던 옛 값</summary>
        public const string Nickname = "nickname";

        /// <summary>Cloud Save 보호 칸 — 닉네임 변경 쿠폰 수 (23 7-8). 서버만 쓴다. 없으면 1장</summary>
        public const string NicknameCoupons = "nicknameCoupons";

        /// <summary>Cloud Save 보호 칸 — 마지막 닉네임 변경 시각, 유닉스 ms (23 7-8). 서버만 쓴다</summary>
        public const string NicknameChangedAt = "nicknameChangedAt";

        /// <summary>Cloud Code 스크립트 — 닉네임 저장 (server/cloud-code/SetNickname.js)</summary>
        public const string SetNicknameScript = "SetNickname";

        /// <summary>Remote Config — 이보다 낮은 앱은 접속하지 않는다. 문자열 x.y.z (23 7-6)</summary>
        public const string MinVersion = "minVersion";

        /// <summary>Remote Config — 참이면 점검 (23 7-6)</summary>
        public const string Maintenance = "maintenance";

        /// <summary>Remote Config — 점검 팝업 설명 한 줄 (23 7-6)</summary>
        public const string MaintenanceMessage = "maintenanceMessage";

        /// <summary>Remote Config — 금칙어 목록, 문자열 배열 JSON (23 8-1)</summary>
        public const string BannedWords = "bannedWords";

        /// <summary>Remote Config — 금칙어를 포함하지만 정상인 단어, 문자열 배열 JSON (23 8-1)</summary>
        public const string AllowedWords = "allowedWords";
    }
}
