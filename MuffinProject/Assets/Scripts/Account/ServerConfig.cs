using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Chapchu.Account
{
    /// <summary>서버가 정한 카드 데이터 — 확인값과 파일 주소 (26-card-data 7-5).</summary>
    public readonly struct CardDataTarget
    {
        private static readonly Regex HashPattern = new Regex(@"^[0-9a-f]{64}\z");

        /// <summary>파일 내용의 확인값 (CardSheet.HashOf). 이 값이 곧 카드 데이터의 신원이다 — 같은 값끼리 같은 방에 든다.</summary>
        public readonly string Hash;

        /// <summary>파일 주소 — 그 확인값의 파일이 바뀌지 않는 주소 (CCD 릴리스 주소, 26 7-4).</summary>
        public readonly string Url;

        /// <summary>사람이 부르는 번호 (패치 노트의 「카드 12」, versioning.md). 로그에만 쓴다 — 같은 데이터인지는 확인값으로 가른다.</summary>
        public readonly int Version;

        public CardDataTarget(string hash, string url, int version)
        {
            Hash = hash;
            Url = url;
            Version = version;
        }

        /// <summary>쓸 수 있는 값인지 — 확인값은 SHA-256 16진수 64자여야 한다 (파일 이름으로도 쓴다).</summary>
        public bool IsSet => !string.IsNullOrEmpty(Url) && Hash != null && HashPattern.IsMatch(Hash);
    }

    /// <summary>
    /// 운영 설정 값(UGS Remote Config) 읽기 — 최소 버전 · 점검 · 금칙어 목록 · 카드 데이터. 기준 문서: docs/systems/23-login.md 7-6 · 8-1 · 26-card-data.md 7-5
    /// 값은 Remote Config 가 갖고(지난 실행 값도 기기에 남긴다), 여기는 읽는 길만 모은다. 로그인 뒤에만 읽는다.
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>읽기 시간 제한 — 넘으면 마지막 값으로 진행 (23 4절)</summary>
        public const float FetchTimeoutSeconds = 10f;

        /// <summary>점검 중 다시 읽는 간격 (20 7-9)</summary>
        public const float MaintenanceRetrySeconds = 30f;

        // Remote Config 가 요구하는 속성 묶음 — 쓰는 속성이 없어 비워 둔다
        private struct UserAttributes { }
        private struct AppAttributes { }

        [Serializable]
        private class StringList
        {
            public string[] items;
        }

        [Serializable]
        private class CardDataValue
        {
            public string hash;
            public string url;
            public int version;
        }

        private static RuntimeConfig Config => RemoteConfigService.Instance.appConfig;

        /// <summary>값을 한 번이라도 받았는지 (이번 실행 또는 지난 실행에 받아 둔 값). 첫 실행은 로그인 전이라 없다 (20 5절).</summary>
        public static bool HasValues => Config.origin != ConfigOrigin.Default;

        public static bool IsMaintenance => Config.GetBool(CloudKeys.Maintenance);

        public static string MaintenanceMessage => Config.GetString(CloudKeys.MaintenanceMessage);

        /// <summary>내 버전이 minVersion 보다 낮은지 — x.y.z 세 자리를 숫자로 비교한다 (23 7-6)</summary>
        public static bool NeedsUpdate => IsOlder(Application.version, Config.GetString(CloudKeys.MinVersion));

        public static IReadOnlyList<string> BannedWords => ReadList(CloudKeys.BannedWords);

        public static IReadOnlyList<string> AllowedWords => ReadList(CloudKeys.AllowedWords);

        /// <summary>
        /// 지금 쓸 카드 데이터 (26-card-data 7-5). 값을 받은 적이 없거나 키가 비어 있으면 IsSet 이 거짓 — 원격 카드 데이터를 쓰지 않는다.
        /// </summary>
        public static CardDataTarget CardData
        {
            get
            {
                if (!HasValues)
                    return default;

                try
                {
                    CardDataValue value = JsonUtility.FromJson<CardDataValue>(Config.GetJson(CloudKeys.CardData, "{}"));
                    return value == null ? default : new CardDataTarget(value.hash, value.url, value.version);
                }
                catch (ArgumentException exception)
                {
                    // 대시보드에 JSON 객체가 아닌 값을 넣었다 — 원격 카드 데이터가 없는 것으로 본다
                    Debug.LogError($"[{nameof(ServerConfig)}] {CloudKeys.CardData} 값을 읽을 수 없다: {exception.Message}");
                    return default;
                }
            }
        }

        public static Task FetchAsync() => RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

        private static IReadOnlyList<string> ReadList(string key)
        {
            string json = Config.GetJson(key, "[]");
            StringList list = JsonUtility.FromJson<StringList>("{\"items\":" + json + "}");
            return list?.items ?? Array.Empty<string>();
        }

        private static bool IsOlder(string mine, string minimum)
        {
            if (string.IsNullOrEmpty(minimum))
                return false;

            string[] a = mine.Split('.');
            string[] b = minimum.Split('.');
            for (int i = 0; i < 3; i++)
            {
                int x = i < a.Length && int.TryParse(a[i], out int ax) ? ax : 0;
                int y = i < b.Length && int.TryParse(b[i], out int by) ? by : 0;
                if (x != y)
                    return x < y;
            }

            return false;
        }
    }
}
