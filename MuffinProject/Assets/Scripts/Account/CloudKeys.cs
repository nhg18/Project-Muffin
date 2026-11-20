namespace Chapchu.Account
{
    /// <summary>UGS 문자열 키 모음 (CODE_CONVENTION 5절) — Cloud Save · Remote Config. 기준 문서: docs/systems/23-login.md 7-4 · 7-6 · 8-1</summary>
    public static class CloudKeys
    {
        /// <summary>Cloud Save — 닉네임 (23 7-4)</summary>
        public const string Nickname = "nickname";

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

        /// <summary>
        /// Remote Config — 지금 쓸 카드 데이터, JSON { "version": 번호, "hash": 확인값, "url": 파일 주소 } (26-card-data 7-5).
        /// 키가 없거나 비어 있으면 원격 카드 데이터를 쓰지 않는다 — 빌드 기본본으로.
        /// </summary>
        public const string CardData = "cardData";
    }
}
