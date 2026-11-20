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

        /// <summary>플레이 기록 수집 선택 (1 = 동의 · 0 = 동의 안 함 · 없으면 아직 고르지 않음). 계정 삭제 때 지운다 (27-analytics 6절)</summary>
        public const string PlayRecordAgreed = "PlayRecordAgreed";

        /// <summary>설정의 효과음 · 배경음 · 초대 알림 스위치 (1 = 켬, 없으면 켬). 이 기기에만 저장한다 (17-lobby-ui-full 5-4)</summary>
        public const string SfxOn = "SfxOn";
        public const string BgmOn = "BgmOn";
        public const string InviteNoticeOn = "InviteNoticeOn";

        /// <summary>기기에 저장한 원격 카드 데이터의 확인값 — 마지막으로 받아 쓴 것 (26-card-data 6-1). 없으면 저장본이 없다</summary>
        public const string CardDataHash = "CardDataHash";

        /// <summary>그 저장본을 받은 앱 버전 — 앱이 업데이트되면 옛 저장본으로 물러나지 않는다 (26 8절)</summary>
        public const string CardDataAppVersion = "CardDataAppVersion";
    }
}
