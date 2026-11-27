using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>
    /// 개발자 모드 (14-lobby-ui 5절) — 켜면 로비의 「혼자 하기」 「방 만들기」 「코드로 참가」 가 풀린다. 이 기기에만 저장한다.
    /// 설정 창의 버전 글자를 연달아 누르면 암호를 묻는다.
    /// </summary>
    public static class DevMode
    {
        // TODO(중계 서버): 암호를 빌드 안에서 확인한다 — 디컴파일하면 해시가 보이고, 해시를 바꿔 치거나 암호를 대입해 볼 수 있다.
        // 시간이 없어 일단 빌드에 둔다 (PM 2026-11-26). 중계 서버가 암호를 확인하고 결과만 돌려주게 바꿔야 한다
        private const string PasswordSha256 = "48bf520a053fe68f37d2e6a317ed8cdb447981abfd5d65f2e52a8bf1eb0201da";

        public static bool IsOn => PlayerPrefs.GetInt(PlayerPrefsKeys.DevMode, 0) == 1;

        /// <summary>암호가 맞으면 켜고 true. 앞뒤 공백은 자른다.</summary>
        public static bool TryTurnOn(string password)
        {
            if (Sha256(password?.Trim() ?? string.Empty) != PasswordSha256)
                return false;

            PlayerPrefs.SetInt(PlayerPrefsKeys.DevMode, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void TurnOff()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKeys.DevMode);
            PlayerPrefs.Save();
        }

        private static string Sha256(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
