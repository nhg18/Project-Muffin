﻿using System;
using System.Text;
using Chapchu.Core;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;

namespace Chapchu.Account
{
    /// <summary>
    /// 플레이 기록(UGS Analytics) 창구 — 수집 동의 · 켜기 / 끄기 · 이벤트 보내기. 기준 문서: docs/systems/27-analytics.md 3 · 5 · 6절
    /// 이벤트 이름 · 값 이름은 여기에만 쓴다 — 웹 대시보드의 SQL 이 같은 이름을 읽으므로 27 5절과 한 글자도 다르면 안 된다.
    /// 에디터 · 개발 빌드는 보내지 않고 콘솔에만 찍는다 (27 3절 #7). 분석이 실패해도 게임은 그대로 간다 (27 3절 #6).
    /// </summary>
    public static class AnalyticsRecorder
    {
        /// <summary>consentAccepted 의 from — 어디서 켰는지 (27 5-1)</summary>
        public enum ConsentFrom { Title, Settings }

        /// <summary>signInFailed 의 kind (27 5-1)</summary>
        public enum SignInFailKind { Network, Rejected }

        /// <summary>titleBlocked 의 reason (27 5-1)</summary>
        public enum TitleBlockReason { Update, Maintenance }

        /// <summary>accountLinked 의 provider (27 5-1)</summary>
        public enum LinkProvider { Google, Apple }

        /// <summary>roomEntered 의 via (27 5-2)</summary>
        public enum RoomVia { QuickStart, Create, Code, Invite }

        /// <summary>roomLeftBeforeStart 의 reason (27 5-2)</summary>
        public enum RoomLeaveReason { Self, Kicked, Disconnected, VersionMismatch }

        /// <summary>matchEnded · myMatchResult 의 winType (27 5-3)</summary>
        public enum WinType { Kill, Chapchu, Draw }

        /// <summary>matchLeft 의 reason (27 5-3)</summary>
        public enum MatchLeaveReason { Quit, TimeoutKick, Disconnect }

        /// <summary>resultAction 의 action (27 5-3)</summary>
        public enum ResultAction { ToRoom, Exit }

        /// <summary>screenViewed 의 screen · from (27 5-5). None 은 from 에만 — 앱을 켠 직후</summary>
        public enum ScreenName { None, Title, Lobby, Room, Game, Result, Shop, Decorate, Dex, Quest, Friends, Profile, Settings }

        /// <summary>지금 화면 언어 코드 — 지역 언어(25-localization)가 아직 없어 한국어 하나다. 언어 고르기가 붙으면 그 값으로 바꾼다</summary>
        private const string CurrentLanguage = "ko";

        /// <summary>보내지 않고 콘솔에만 찍는 빌드인지 — 에디터 · 개발 빌드 · 테스트 클론 (27 3절 #7)</summary>
        public static bool IsLogOnly => Debug.isDebugBuild;

        /// <summary>플레이 기록 수집을 아직 고르지 않았는지 — 타이틀이 동의 팝업을 띄울지 정한다 (20 7-12)</summary>
        public static bool HasChoice => PlayerPrefs.HasKey(PlayerPrefsKeys.PlayRecordAgreed);

        /// <summary>플레이 기록 수집에 동의했는지 (27 3절 #2)</summary>
        public static bool IsAgreed => PlayerPrefs.GetInt(PlayerPrefsKeys.PlayRecordAgreed, 0) == 1;

        /// <summary>
        /// 첫 실행 팝업에서 고른 값을 기기에 기록한다 — 약관 동의와 같은 때(닉네임 「시작」, 23 6-1).
        /// 수집은 아직 켜지 않는다 — UGS 가 준비된 뒤(로그인 전) <see cref="StartIfAgreed"/>.
        /// </summary>
        public static void SaveChoice(bool agreed)
        {
            PlayerPrefs.SetInt(PlayerPrefsKeys.PlayRecordAgreed, agreed ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// UGS 가 준비된 뒤, 로그인 전에 부른다 — 동의했으면 수집을 켠다. 로그인 실패(signInFailed)도 남기려고 (27 6절, PM 2026-11-20).
        /// <paramref name="justAgreed"/> 는 이번 실행에서 처음 동의했을 때만 참 — 그때만 consentAccepted 를 보낸다.
        /// </summary>
        public static void StartIfAgreed(bool justAgreed)
        {
            if (!IsAgreed)
                return;

            SetCollecting(true);

            if (justAgreed)
                RecordConsentAccepted(ConsentFrom.Title);
        }

        /// <summary>설정 「플레이 기록 보내기」 스위치 (17 7-4 · 27 6절). 끄면 이미 보낸 기록도 삭제를 요청한다</summary>
        public static void SetAgreedFromSettings(bool agreed)
        {
            if (agreed == IsAgreed && HasChoice)
                return;

            SaveChoice(agreed);

            if (agreed)
            {
                SetCollecting(true);
                RecordConsentAccepted(ConsentFrom.Settings);
                return;
            }

            SetCollecting(false);
            RequestDeletion();
        }

        /// <summary>계정 삭제가 성공한 뒤 — 분석 데이터 삭제 요청 · 수집 끔 · 기기의 선택도 지운다 (27 6절 · 23 6-4)</summary>
        public static void ForgetForAccountDeletion()
        {
            SetCollecting(false);
            RequestDeletion();
            PlayerPrefs.DeleteKey(PlayerPrefsKeys.PlayRecordAgreed);
            PlayerPrefs.Save();
        }

        // ── 5-1 처음 실행 · 계정 ──

        public static void RecordConsentAccepted(ConsentFrom from)
            => Record("consentAccepted", ("language", CurrentLanguage), ("from", ToValue(from)));

        public static void RecordNicknameSaved(bool isFirst, int rejectCount)
            => Record("nicknameSaved", ("isFirst", isFirst), ("rejectCount", rejectCount));

        public static void RecordSignInFailed(SignInFailKind kind, string errorCode)
            => Record("signInFailed", ("kind", ToValue(kind)), ("errorCode", errorCode ?? string.Empty));

        public static void RecordTitleBlocked(TitleBlockReason reason)
            => Record("titleBlocked", ("reason", ToValue(reason)));

        public static void RecordAccountLinked(LinkProvider provider, bool switched)
            => Record("accountLinked", ("provider", ToValue(provider)), ("switched", switched));

        // ── 5-2 로비 · 방 ──

        public static void RecordRoomEntered(RoomVia via, bool isPublic, int humans)
            => Record("roomEntered", ("via", ToValue(via)), ("isPublic", isPublic), ("humans", humans));

        public static void RecordRoomLeftBeforeStart(int waitSeconds, RoomLeaveReason reason)
            => Record("roomLeftBeforeStart", ("waitSeconds", waitSeconds), ("reason", ToValue(reason)));

        public static void RecordLanguageChanged(string from, string to)
            => Record("languageChanged", ("from", from), ("to", to));

        // ── 5-3 판 ── (방장만 보내는 셋은 matchStarted · matchEnded · matchCardStats — 27 2절)

        public static void RecordMatchStarted(string matchId, int humans, int bots, int botEasy, int botNormal, int botHard, bool quickStart, int matchIndexInRoom)
            => Record("matchStarted",
                ("matchId", matchId), ("humans", humans), ("bots", bots), ("botEasy", botEasy),
                ("botNormal", botNormal), ("botHard", botHard), ("quickStart", quickStart),
                ("matchIndexInRoom", matchIndexInRoom));

        public static void RecordMatchEnded(string matchId, int turns, int durationSeconds, WinType winType, bool winnerIsBot, int leavers, int reconnects)
            => Record("matchEnded",
                ("matchId", matchId), ("turns", turns), ("durationSeconds", durationSeconds),
                ("winType", ToValue(winType)), ("winnerIsBot", winnerIsBot), ("leavers", leavers),
                ("reconnects", reconnects));

        public static void RecordMatchCardStats(string matchId, int cardId, int plays, int playersUsed, bool winnerUsed)
            => Record("matchCardStats",
                ("matchId", matchId), ("cardId", cardId), ("plays", plays), ("playersUsed", playersUsed),
                ("winnerUsed", winnerUsed));

        public static void RecordMyMatchResult(string matchId, int rank, bool won, WinType winType, int survivedTurns, int damageDealt, int kills, int cardsPlayed, int timeouts, int chapchuDeclares)
            => Record("myMatchResult",
                ("matchId", matchId), ("rank", rank), ("won", won), ("winType", ToValue(winType)),
                ("survivedTurns", survivedTurns), ("damageDealt", damageDealt), ("kills", kills),
                ("cardsPlayed", cardsPlayed), ("timeouts", timeouts), ("chapchuDeclares", chapchuDeclares));

        public static void RecordMatchLeft(string matchId, int turn, MatchLeaveReason reason)
            => Record("matchLeft", ("matchId", matchId), ("turn", turn), ("reason", ToValue(reason)));

        public static void RecordResultAction(ResultAction action)
            => Record("resultAction", ("action", ToValue(action)));

        // ── 5-4 재화 · 치장 ── (questRewardClaimed · boxOpened 는 서버 지급(24 2절)이 붙을 때 더한다 — 지금 값은 기기 안의 임시 값)

        /// <summary>slot 은 슬롯 enum 이름의 첫 글자를 소문자로 (Character → character)</summary>
        public static void RecordItemEquipped(string itemId, Enum slot)
            => Record("itemEquipped", ("itemId", itemId), ("slot", ToValue(slot)));

        // ── 5-5 화면 · 로딩 ──

        /// <summary>지금 보고 있는 화면 — 팝업을 닫을 때 그 아래 화면으로 돌아가려고 읽는다</summary>
        public static ScreenName CurrentScreen { get; private set; } = ScreenName.None;

        /// <summary>화면 · 팝업에 들어감. 같은 화면에 머무는 동안은 다시 보내지 않는다 — from 은 바로 앞 화면 (27 5-5)</summary>
        public static void RecordScreenViewed(ScreenName screen)
        {
            if (screen == CurrentScreen)
                return;

            ScreenName from = CurrentScreen;
            CurrentScreen = screen;
            Record("screenViewed", ("screen", ToValue(screen)), ("from", ToValue(from)));
        }

        public static void RecordLobbyReached(float loadSeconds, bool isFirstRun, bool cardDataDownloaded)
            => Record("lobbyReached", ("loadSeconds", loadSeconds), ("isFirstRun", isFirstRun), ("cardDataDownloaded", cardDataDownloaded));

        private static void Record(string name, params (string key, object value)[] values)
        {
            if (!IsAgreed)
                return;

            if (IsLogOnly)
            {
                Debug.Log($"[{nameof(AnalyticsRecorder)}] (개발 빌드 — 보내지 않음) {Describe(name, values)}");
                return;
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
                return; // 로그인 전 — 수집이 아직 켜지지 않았다 (27 6절)

            var analyticsEvent = new CustomEvent(name);
            foreach ((string key, object value) in values)
                analyticsEvent.Add(key, value);

            try
            {
                AnalyticsService.Instance.RecordEvent(analyticsEvent);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(AnalyticsRecorder)}] 이벤트를 버림 — {exception.Message}");
            }
        }

        // 동의 상태는 Unity 의 사용자 동의(EndUserConsent)로 SDK 에 알린다 — 6.x 의 수집 켜기 · 끄기 방식
        private static void SetCollecting(bool on)
        {
            if (IsLogOnly)
            {
                Debug.Log($"[{nameof(AnalyticsRecorder)}] (개발 빌드 — 보내지 않음) 수집 {(on ? "켬" : "끔")}");
                return;
            }

            ConsentState state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = on ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state);
        }

        // 수집을 끈 뒤에만 부를 수 있다 (SDK 가 막는다). 삭제는 서버에서 30일 안에 끝난다 (27 4절)
        private static void RequestDeletion()
        {
            if (IsLogOnly || UnityServices.State != ServicesInitializationState.Initialized)
                return;

            try
            {
                AnalyticsService.Instance.RequestDataDeletion();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(AnalyticsRecorder)}] 삭제 요청 실패 — {exception.Message}");
            }
        }

        // enum 이름을 27 5절의 값 모양(camelCase)으로 — QuickStart → quickStart
        private static string ToValue(Enum value)
        {
            string name = value.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string Describe(string name, (string key, object value)[] values)
        {
            var text = new StringBuilder(name);
            foreach ((string key, object value) in values)
                text.Append(' ').Append(key).Append('=').Append(value);

            return text.ToString();
        }
    }
}
