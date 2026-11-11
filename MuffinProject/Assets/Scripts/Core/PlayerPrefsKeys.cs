using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>PlayerPrefs 키 모음 (CODE_CONVENTION 5절). 키 문자열은 여기에만 쓴다.</summary>
    public static class PlayerPrefsKeys
    {
        /// <summary>마지막으로 쓴 닉네임 — 게스트는 다음 실행의 닉네임 창에 이 값이 미리 들어 있다</summary>
        public const string PlayerName = "PlayerName";

        /// <summary>
        /// 마지막 로그인이 계정(Google · Apple)이었는지 (1 = 계정). 이때만 다음 실행에 누르지 않아도 자동 로그인한다.
        /// 게스트는 저장된 로그인이 있어도 매번 「화면을 눌러 시작」 부터 (PM 2026-11-11)
        /// </summary>
        public const string AccountLogin = "AccountLogin";

        /// <summary>약관 · 개인정보 동의 기록 (1 = 동의). 계정 삭제 때 지운다 (23-login 7-5 · 6-4)</summary>
        public const string TermsAgreed = "TermsAgreed";
    }
}
