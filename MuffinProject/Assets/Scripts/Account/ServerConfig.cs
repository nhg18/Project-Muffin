using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Chapchu.Account
{
    /// <summary>
    /// 운영 설정 값(UGS Remote Config) 읽기 — 최소 버전 · 점검 · 금칙어 목록. 기준 문서: docs/systems/23-login.md 7-6 · 8-1
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

        private static RuntimeConfig Config => RemoteConfigService.Instance.appConfig;

        /// <summary>값을 한 번이라도 받았는지 (이번 실행 또는 지난 실행에 받아 둔 값). 첫 실행은 로그인 전이라 없다 (20 5절).</summary>
        public static bool HasValues => Config.origin != ConfigOrigin.Default;

        public static bool IsMaintenance => Config.GetBool(CloudKeys.Maintenance);

        public static string MaintenanceMessage => Config.GetString(CloudKeys.MaintenanceMessage);

        /// <summary>내 버전이 minVersion 보다 낮은지 — x.y.z 세 자리를 숫자로 비교한다 (23 7-6)</summary>
        public static bool NeedsUpdate => IsOlder(Application.version, Config.GetString(CloudKeys.MinVersion));

        public static IReadOnlyList<string> BannedWords => ReadList(CloudKeys.BannedWords);

        public static IReadOnlyList<string> AllowedWords => ReadList(CloudKeys.AllowedWords);

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
