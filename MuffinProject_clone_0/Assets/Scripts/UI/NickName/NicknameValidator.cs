using System;
using System.Text.RegularExpressions;

namespace UI.NickName
{
    public enum NicknameValidationResult
    {
        Valid,
        Empty,
        TooShort,
        TooLong,
        InvalidCharacters,
    }
    
    public static class NicknameValidator
    {
        public const int MaxLength = 16;
        public const int MinLength = 2;

        private static readonly Regex AllowedCharsRegex = 
            new(@"^[가-힣a-zA-Z0-9]+$", RegexOptions.Compiled);

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
