using System.Text.RegularExpressions;

namespace Chapchu.UI.Title
{
    /// <summary>닉네임 검사 결과. Valid 가 아니면 GetErrorMessage 로 문구를 얻는다.</summary>
    public enum NicknameValidationResult
    {
        Valid,
        Empty,
        TooShort,
        TooLong,
        InvalidCharacters,
    }

    /// <summary>닉네임 길이 · 글자 검사와 실패 문구 (12-title-ui.md 4-2절). 검사는 입력한 클라이언트가 한다.</summary>
    public static class NicknameValidator
    {
        /// <summary>최대 길이 (12-title-ui.md 4-2). 입력창도 여기서 막는다.</summary>
        public const int MaxLength = 16;

        /// <summary>최소 길이 (12-title-ui.md 4-2).</summary>
        public const int MinLength = 2;

        // 한글 완성형 · 영문 · 숫자만 (12-title-ui.md 4-2)
        private static readonly Regex AllowedCharsRegex =
            new(@"^[가-힣a-zA-Z0-9]+$", RegexOptions.Compiled);

        /// <summary>앞뒤 공백을 자른 닉네임을 검사한다. 순서: 비었음 → 짧음 → 김 → 허용 안 된 문자.</summary>
        public static NicknameValidationResult Validate(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return NicknameValidationResult.Empty;

            if (nickname.Length < MinLength)
                return NicknameValidationResult.TooShort;

            if (nickname.Length > MaxLength)
                return NicknameValidationResult.TooLong;

            if (!AllowedCharsRegex.IsMatch(nickname))
                return NicknameValidationResult.InvalidCharacters;

            return NicknameValidationResult.Valid;
        }

        /// <summary>검사 실패 사유의 화면 문구 (12-title-ui.md 10-3).</summary>
        public static string GetErrorMessage(NicknameValidationResult result) => result switch
        {
            NicknameValidationResult.Empty             => "닉네임을 입력해 주세요.",
            NicknameValidationResult.TooShort          => $"{MinLength}자 이상 입력해 주세요.",
            NicknameValidationResult.TooLong           => $"{MaxLength}자 이하로 입력해 주세요.",
            NicknameValidationResult.InvalidCharacters => "사용할 수 없는 문자가 포함되어 있습니다.",
            _                                          => "알 수 없는 오류입니다."
        };
    }
}
