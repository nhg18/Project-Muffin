using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

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
        BannedWord,
    }

    /// <summary>닉네임 길이 · 글자 · 금칙어 검사와 실패 문구, 추천 닉네임 (12-title-ui.md 4-2절 · 23-login.md 8절). 검사는 입력한 클라이언트가 한다.</summary>
    public static class NicknameValidator
    {
        /// <summary>최대 길이 (12-title-ui.md 4-2). 입력창도 여기서 막는다.</summary>
        public const int MaxLength = 16;

        /// <summary>최소 길이 (12-title-ui.md 4-2).</summary>
        public const int MinLength = 2;

        // 한글 완성형 · 영문 · 숫자만 (12-title-ui.md 4-2)
        private static readonly Regex AllowedCharsRegex =
            new(@"^[가-힣a-zA-Z0-9]+$", RegexOptions.Compiled);

        // 23 8-2 3단계 — 숫자 → 영문 유사문자. 1 은 두 갈래(l · ㅣ)라 따로 만든다
        private static readonly (char from, char to)[] DigitLookAlikes =
        {
            ('0', 'o'), ('5', 's'), ('8', 'b'), ('3', 'e'), ('4', 'a'), ('7', 't'),
        };

        // 추천 닉네임 = 꾸밈말 + 이름 + 두 자리 — 최대 8자라 4-2 를 늘 통과한다 (통합 화면 UI v2 타이틀)
        private static readonly string[] SuggestAdjectives = { "용감한", "배고픈", "졸린", "신난", "수상한", "날쌘", "씩씩한", "엉뚱한", "느긋한", "말랑한" };
        private static readonly string[] SuggestNouns = { "치와와", "멍멍이", "찹츄", "강아지", "뼈다귀" };

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

        /// <summary>
        /// 금칙어 검사 (23 8-2) — Validate 를 통과한 닉네임에 쓴다. 소문자 → 숫자 유사문자 치환 → 통과 목록 단어 지우기 → 금칙 목록 부분 일치.
        /// 공백 · 특수문자 제거 · 자모 결합 · 초성 검사(1 · 4 · 7단계)는 허용 문자 검사를 통과한 입력에는 해당하는 글자가 없어 뺐다 — 서버 검사(Cloud Code)는 전부 돈다.
        /// </summary>
        public static bool ContainsBannedWord(string nickname, IReadOnlyList<string> banned, IReadOnlyList<string> allowed)
        {
            if (banned == null || banned.Count == 0)
                return false;

            string lowered = nickname.ToLowerInvariant();
            foreach ((char from, char to) in DigitLookAlikes)
                lowered = lowered.Replace(from, to);

            return ContainsAny(lowered.Replace('1', 'l'), banned, allowed) || ContainsAny(lowered.Replace('1', 'ㅣ'), banned, allowed);
        }

        /// <summary>검사 실패 사유의 화면 문구 (12-title-ui.md 10-3).</summary>
        public static string GetErrorMessage(NicknameValidationResult result) => result switch
        {
            NicknameValidationResult.Empty             => "닉네임을 입력해 주세요.",
            NicknameValidationResult.TooShort          => $"{MinLength}자 이상 입력해 주세요.",
            NicknameValidationResult.TooLong           => $"{MaxLength}자 이하로 입력해 주세요.",
            NicknameValidationResult.InvalidCharacters => "사용할 수 없는 문자가 포함되어 있어요.",
            NicknameValidationResult.BannedWord        => "사용할 수 없는 단어가 있어요.",
            _                                          => "알 수 없는 오류입니다."
        };

        /// <summary>추천 닉네임 — 꾸밈말 + 이름 + 10 ~ 99.</summary>
        public static string Suggest()
            => SuggestAdjectives[Random.Range(0, SuggestAdjectives.Length)] + SuggestNouns[Random.Range(0, SuggestNouns.Length)] + Random.Range(10, 100);

        private static bool ContainsAny(string normalized, IReadOnlyList<string> banned, IReadOnlyList<string> allowed)
        {
            if (allowed != null)
            {
                foreach (string word in allowed)
                {
                    if (!string.IsNullOrEmpty(word))
                        normalized = normalized.Replace(word.ToLowerInvariant(), string.Empty);
                }
            }

            foreach (string word in banned)
            {
                if (!string.IsNullOrEmpty(word) && normalized.Contains(word.ToLowerInvariant()))
                    return true;
            }

            return false;
        }
    }
}
