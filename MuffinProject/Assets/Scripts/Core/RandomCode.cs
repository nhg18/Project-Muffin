using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>
    /// 방 코드 생성. 자릿수 · 규칙은 08-room.md 5절 #3 미정 — 확정되면 Length 하나만 바꾼다.
    /// </summary>
    public static class RandomCode
    {
        public const int Length = 4;

        public static string GenerateRandomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char[] code = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                code[i] = chars[Random.Range(0, chars.Length)];
            }

            return new string(code);
        }
    }
}
