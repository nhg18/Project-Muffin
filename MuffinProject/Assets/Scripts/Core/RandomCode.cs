using Chapchu.Game;

namespace Chapchu.Core
{
    /// <summary>
    /// 방 코드 생성 — 영문 대문자 6자리 (08-room.md 5절 #3, 2026-11-15). 4자리(약 46만 개)는 다 넣어 보며 남의 비공개 방을 찾을 수 있다.
    /// </summary>
    public static class RandomCode
    {
        public const int Length = 6;

        public static string GenerateRandomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char[] code = new char[Length];

            // 암호 난수 — 만든 시각으로 코드를 맞추지 못하게. 글자마다 확률이 같다 (SecureRandom.Next 는 치우침 없이 뽑는다)
            var random = new SecureRandom();
            for (int i = 0; i < Length; i++)
                code[i] = chars[random.Next(chars.Length)];

            return new string(code);
        }
    }
}
