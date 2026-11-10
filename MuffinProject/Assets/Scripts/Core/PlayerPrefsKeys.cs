using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>PlayerPrefs 키 모음 (CODE_CONVENTION 5절). 키 문자열은 여기에만 쓴다.</summary>
    public static class PlayerPrefsKeys
    {
        public const string PlayerName = "PlayerName";

        /// <summary>약관 · 개인정보 동의 기록 (1 = 동의). 계정 삭제 때 지운다 (23-login 7-5 · 6-4)</summary>
        public const string TermsAgreed = "TermsAgreed";
    }
}
