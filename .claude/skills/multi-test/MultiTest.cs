using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Chapchu.Cards;
using Chapchu.DebugTools;
using Chapchu.Game;
using Chapchu.Game.Bots;
using Chapchu.Game.Effects;
using Chapchu.Network;
using Chapchu.UI.GameScreen;
using Chapchu.UI.Lobby;
using Chapchu.UI.Popups;
using Chapchu.UI.ResultScreen;
using Chapchu.UI.RoomScreen;
using Chapchu.UI.Title;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 멀티 테스트 진입점 — run.sh 가 `unity command script.run --entry MultiTest.Xxx` 로 에디터마다 부른다.
/// 프로젝트 밖 파일이라 빌드에 들어가지 않는다. 화면 버튼 대신 각 컴포넌트의 private 처리 함수를 리플렉션으로 부른다 —
/// 이름이 바뀌면 여기만 고친다 (아래 "부르는 함수" 표).
/// 반환 문자열이 "FAIL" 로 시작하면 run.sh 가 실패로 본다.
/// </summary>
public static class MultiTest
{
    // ── 부르는 함수 (코드에서 이름이 바뀌면 여기를 고친다) ──
    private const string JoinButtonMethod = "ClickJoinButton";             // DebugScript
    private const string StartRequestedMethod = "HandleStartRequested";    // RoomPresenter
    private const string TappedMethod = "HandleTapped";                    // TitlePresenter — 「화면을 눌러 시작」
    private const string AllAgreeMethod = "HandleAllClicked";              // TermsAgreeView — 「모두 동의하기」
    private const string AgreeConfirmMethod = "HandleConfirmClicked";      // TermsAgreeView — 「동의하고 계속」
    private const string PlayRecordAgreeMethod = "HandleAgreeClicked";     // TitlePopupView — 플레이 기록 수집 「동의」 (20 7-12)
    private const string GuestClickedMethod = "HandleGuestClicked";        // LoginSheetView — 「게스트로 시작」
    private const string StartClickedMethod = "HandleStartClicked";        // LoginSheetView — 닉네임 「시작」
    private const string JoinRoomRequestedMethod = "HandleJoinRoomRequested"; // LobbyRoomRequester
    private const string CreateRoomRequestedMethod = "HandleCreateRoomRequested"; // LobbyRoomRequester
    private const string SubmitClickedMethod = "HandleSubmitClicked";      // LobbyModalView — 입력 창 「참가」
    private const string WarnOkClickedMethod = "HandleWarnOkClicked";      // LobbyModalView — 경고 창 「확인」
    private const string ModalFieldField = "field";                        // LobbyModalView — 입력칸
    private const string WarnTitleField = "warnTitle";                     // LobbyModalView — 경고 창 제목 · 본문
    private const string WarnMessageField = "warnMessage";
    private const string LeaveClickedMethod = "HandleLeaveClicked";        // RoomPresenter — 「나가기」 (혼자면 확인 없이 나간다)
    private const string RoomLastLogField = "lastLineText";                // RoomLogView — 접힌 로그의 최근 한 줄
    private const string PanelChapchuField = "_isChapchu";                 // MyPanelPresenter — 내 찹츄 상태 (찹츄 자리 「찹츄 완료」)
    private const string SeatOfField = "_seatOf";                          // OpponentSeatsPresenter — actorNumber → 좌석
    private const string SeatChapchuField = "_chapchu";                    // OpponentSeatView — 찹츄 표시
    private const string CardUseRequestedMethod = "HandleCardUseRequested"; // HandPresenter — 손패 카드의 사용을 청했다 (카드) — 더블클릭과 같은 길
    private const string HandCardsField = "_cards";                        // HandView — 손패의 카드들
    private const string CardGlowField = "glow";                           // CardView — 「낼 수 있다」 빛
    private const string TrapSlotClickedMethod = "HandleSlotClicked";      // MyTrapsPresenter — 내 함정 칸을 눌렀다 (칸 번호)
    private const string TrapPreviewField = "_preview";                    // MyTrapsPresenter — 크게 보기 중인 카드
    private const string TrapsOfField = "_trapsOf";                        // OpponentSeatsPresenter — actorNumber → 함정 칸
    private const string PickedField = "_picked";                          // OpponentSeatsPresenter — 대상 선택 중 고른 actorNumber
    private const string MyHandField = "_myHand";                          // GameDebugInput — 카드 ID
    private const string MyHandInstancesField = "_myHandInstances";        // GameDebugInput — 인스턴스 ID
    private const string GameEndedField = "_gameEnded";                    // GameDebugInput — 게임 종료를 받았다
    private const string MyTrapsField = "_myTraps";                        // GameDebugInput — 설치한 함정 인스턴스 ID
    private const string GaepanHudNumberField = "numberText";              // GaepanHudView — 남은 바퀴 숫자 (1021)
    private const string ChoiceSecondButtonField = "secondButton";         // ChoicePopupView — 「80 피해 받기」 (1019)
    private const string ChoiceFirstButtonField = "firstButton";           // ChoicePopupView — 「손패 전부 버리기」
    private const string TimerLabelField = "labelText";                    // ReactionTimerView — 알약 글 (「반응」 · 「{이름} 고르는 중」)
    private const string WarningMainTextField = "mainText";                // WarningPopup — 제목 (WarningPopupText)
    private const string WarningSubTextField = "subText";                  // WarningPopup — 사유
    private const string HandUsableField = "_usable";                      // HandView — 낼 수 있는 카운터 (ReactionSkipProbe)
    private const string SkipButtonField = "skipButton";                   // ReactionTimerView — 스킵 버튼 (CounterProbe)
    private const string PillLabelField = "labelText";                     // ReactionTimerView — 알약 글자
    private const string NoTimeoutField = "_noTimeoutActors";              // GameServer — 시간 초과 대상이 아닌 사람 (ExpireTurn)
    private const string TurnDeadlineField = "_turnDeadline";              // GameServer — 턴 마감 (ExpireTurn)
    private const string PunServerField = "_server";                       // PunGameServer — 규칙 원본 (방장)
    private const string ServerDeckField = "deck";                         // PunGameServer — 게임에 쓰는 덱
    private const string ServerPlayersField = "_players";                  // GameServer — 플레이어 원본 상태
    private const string ServerTurnOrderField = "_turnOrder";              // GameServer — 턴 순서
    private const string ServerDiscardField = "_discardPile";              // GameServer — 버림 더미
    private const string ServerTrapSetOrderField = "_trapSetOrder";        // GameServer — 함정 설치 순서
    private const string ServerNextTrapSetOrderField = "_nextTrapSetOrder";
    private const string ServerDeckListField = "_deck";                    // GameServer — 덱
    private const string RulesDeckPath = "Assets/Cards/Decks/Deck_Default.asset";
    private const string MainDeckPath = "Assets/Cards/Main/Deck_Main.asset"; // 카드 묶음 (11-card-list 0절 — 카드 가져오기)

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string SlotNameField = "nameText";     // RoomSlotView — 이름표 글자
    private const string SlotBotTagField = "botTagLabel"; // RoomSlotView — 봇 꼬리표 글자 「(봇 · 보통)」
    private const string SlotButtonField = "slotButton"; // RoomSlotView — 칸 전체 버튼
    private const string ResultPodiumsField = "_podiums";     // ResultPresenter — 순위 순 시상대
    private const string ResultTitleField = "title";          // ResultPresenter — 제목 · 알약
    private const string ResultButtonsField = "buttons";      // ResultPresenter — 「판 기록」 · 「로비로」 · 「한 판 더」
    private const string ResultBoardField = "board";          // ResultPresenter — 「판 기록」 판
    private const string ResultEmotesField = "emotes";        // ResultPresenter — 이모트 버튼
    private const string ServerStatsField = "_stats";         // GameServer — 판 지표 (actorNumber → StatKind 순 값)
    private const string BubbleLogKey = "MultiTest.BubbleLog"; // ArmBubbleLog 가 본 말풍선 (에디터마다 따로)

    // 규칙 상수 — GameServer 의 private 상수와 같게 둔다 (04-card.md 8절 반응 10초 · 03-turn.md 2절 턴 30초).
    private const double ReactionSeconds = 5, TurnSeconds = 30; // 반응 5초 — 낼 수 있는 사람이 있을 때 (04 8절)
    private const double BotTurnSeconds = 9; // 보통 봇이 수를 두는 가장 늦은 때 — 고민되는 수 5.5초 + 망설임 2.5초 = 8초에 여유 (18-bot.md 4절 · 7-7)

    // ActIfMyTurn 이 마지막으로 행동한 턴의 마감 시각 (에디터마다 따로). script.run 은 부를 때마다 새로 컴파일돼 static 이 남지 않아 SessionState 에 둔다
    private const string ActedTurnKey = "MultiTest.ActedTurnDeadline";
    private const string ActedCardKey = "MultiTest.ActedCard"; // 이번 턴에 낸 카드 (인스턴스) — 거절됐는지 보려고

    // ── 준비 · 뒷정리 ──

    /// <summary>저장 안 한 씬이 있으면 FAIL (사람의 작업을 덮지 않는다). 없으면 Refresh 로 새 파일 · 코드를 가져온다.</summary>
    public static string Preflight()
    {
        var dirty = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty)
                dirty.Add(scene.path);
        }

        if (dirty.Count > 0)
            return "FAIL 저장 안 한 씬: " + string.Join(", ", dirty);

        if (EditorApplication.isPlaying)
            return "FAIL 이미 플레이 중";

        AssetDatabase.Refresh();
        return "OK " + SceneManager.GetActiveScene().path;
    }

    /// <summary>플레이 시작 씬을 정한다. 빈 문자열이면 되돌린다 (현재 씬에서 시작).</summary>
    public static string SetStartScene(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            EditorSceneManager.playModeStartScene = null;
            return "OK 시작 씬 되돌림";
        }

        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (scene == null)
            return "FAIL 씬 없음: " + path;

        EditorSceneManager.playModeStartScene = scene;
        return "OK 시작 씬 " + path;
    }

    // ── 멀티: 디버그 로비 → 방 → 게임 ──

    /// <summary>디버그 로비의 입장 버튼. 연결 전이면 WAIT.</summary>
    public static string Join()
    {
        DebugScript debug = Object.FindFirstObjectByType<DebugScript>();
        if (debug == null)
            return "WAIT 디버그 로비 아님 (" + SceneManager.GetActiveScene().name + ")";

        if (PhotonNetwork.NetworkClientState != Photon.Realtime.ClientState.ConnectedToMasterServer) // NameServer 단계에서 누르면 JoinOrCreateRoom 이 실패한다
            return "WAIT 연결 중 " + PhotonNetwork.NetworkClientState;

        Call(debug, JoinButtonMethod);
        return "OK 입장 요청";
    }

    /// <summary>방 인원이 expected 명이 되면 방장이 시작을 누른다. 덜 모였으면 WAIT.</summary>
    public static string StartGame(string expected)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "WAIT 방 없음";

        if (!PhotonNetwork.IsMasterClient)
            return "FAIL 방장이 아님 — 메인 에디터가 먼저 입장해야 한다";

        int count = PhotonNetwork.CurrentRoom.PlayerCount;
        if (count < int.Parse(expected))
            return "WAIT 인원 " + count + " / " + expected;

        RoomPresenter presenter = Object.FindFirstObjectByType<RoomPresenter>();
        if (presenter == null)
            return "WAIT 방 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        Call(presenter, StartRequestedMethod);
        return "OK 시작 요청, 인원 " + count;
    }

    /// <summary>내 턴이면 행동 하나를 보낸다 — 효과 있는 행동 카드가 손에 있으면 다음 사람에게, 없으면 뽑기. 남의 턴이면 SKIP.</summary>
    public static string ActIfMyTurn()
    {
        GameDebugInput input = Object.FindFirstObjectByType<GameDebugInput>();
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (input == null || server == null)
            return "WAIT 게임 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        if (!TryGetTurnActor(out int turnActor))
            return "WAIT 턴 정보 없음";

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if ((bool)typeof(GameDebugInput).GetField(GameEndedField, Private).GetValue(input))
            return "SKIP P" + me + " 게임 끝";

        if (turnActor != me)
            return "SKIP P" + me + " 남의 턴(P" + turnActor + ")";

        // 카드를 낸 턴은 체인 처리 · 후처리 동안 같은 턴이 이어진다 (03-turn.md 4절) — 턴 마감 시각으로 같은 턴인지 본다
        double deadline = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.TurnDeadline, out object value) ? (double)value : -1;
        string turnKey = deadline.ToString("R");
        var hand = (List<int>)typeof(GameDebugInput).GetField(MyHandField, Private).GetValue(input);
        var instances = (List<int>)typeof(GameDebugInput).GetField(MyHandInstancesField, Private).GetValue(input);
        if (SessionState.GetString(ActedTurnKey, "") == turnKey)
        {
            // 낸 카드가 아직 손패에 있다 = 거절됐다 (대상 조건 · 행동 금지 등). 그대로 두면 시간 초과가 쌓여 3번째에 강제 퇴장된다 (03-turn 4절) — 대신 뽑는다
            int tried = SessionState.GetInt(ActedCardKey, -1);
            if (tried < 0 || !instances.Contains(tried))
                return "SKIP P" + me + " 처리 중";
            SessionState.SetInt(ActedCardKey, -1);
            server.RequestDraw();
            return "OK P" + me + " · 낸 카드가 거절됨 → 뽑기";
        }
        SessionState.SetString(ActedTurnKey, turnKey);
        SessionState.SetInt(ActedCardKey, -1);
        var deck = (DeckData)typeof(PunGameServer).GetField(ServerDeckField, Private).GetValue(server);

        // 함정은 메인 행동이 아니다 — 빈 칸이 있으면 손패의 함정 하나를 먼저 설치한다 (04-card.md 6절)
        var traps = (List<int>)typeof(GameDebugInput).GetField(MyTrapsField, Private).GetValue(input);
        int trapIndex = hand.FindIndex(cardId => deck.GetCard(cardId)?.type == CardType.Trap);
        string setLog = "";
        if (trapIndex >= 0 && traps.Count < 3)
        {
            // 화면 경로 — 손패의 함정 카드의 사용을 청한다 (더블클릭과 같은 길) (HandPresenter 가 설치 요청으로 바꾼다)
            if (UseFromHand(instances[trapIndex]))
                setLog = " · 함정 설치(화면)";
            else
            {
                server.RequestSetTrap(instances[trapIndex]);
                setLog = " · 함정 설치";
            }
        }

        // 대상에게 함정이 있어야 낼 수 있는 카드(104 파헤치기)는 함정이 없으면 건너뛴다 — 내면 거절돼 턴이 시간 초과까지 멈춘다
        int target = NextActor(me);
        bool targetHasTrap = BotRoster.TryGetPublic(target, PlayerProps.TrapCount, out object trapCount) && trapCount is int trapsOfTarget && trapsOfTarget > 0;
        int index = hand.FindIndex(id => IsActionWithEffect(deck, id) && (deck.GetCard(id).targetCondition != TargetCondition.HasTrap || targetHasTrap));

        if (index < 0)
        {
            server.RequestDraw();
            return "OK P" + me + setLog + " · 뽑기";
        }

        // 방장은 자기에게 보낸 RPC 를 그 자리에서 실행해 손패 목록이 바로 줄어든다 — 카드 번호는 요청 전에 읽는다
        int cardId = hand[index];
        SessionState.SetInt(ActedCardKey, instances[index]);
        server.RequestPlayCard(instances[index], new[] { target });
        return "OK P" + me + setLog + " · 카드 " + cardId + " → P" + target;
    }

    /// <summary>남의 턴에 카드를 내 본다 (거절 확인용). 내 턴이면 SKIP.</summary>
    public static string PlayOutOfTurn()
    {
        GameDebugInput input = Object.FindFirstObjectByType<GameDebugInput>();
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (input == null || server == null || !TryGetTurnActor(out int turnActor))
            return "WAIT 게임 화면 아님";

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (turnActor == me)
            return "SKIP P" + me + " 내 턴";

        var instances = (List<int>)typeof(GameDebugInput).GetField(MyHandInstancesField, Private).GetValue(input);
        if (instances.Count == 0)
            return "SKIP P" + me + " 손패 없음";

        server.RequestPlayCard(instances[0], new[] { turnActor });
        return "OK P" + me + " 남의 턴에 카드 사용 요청";
    }

    // ── 찹츄 자동 선언 (07-win-condition.md 4.1 · 10-ui.md 「찹츄 버튼 상태」) ──

    private const string ChapchuReportedKey = "MultiTest.ChapchuReported";

    /// <summary>
    /// 내 화면에 찹츄가 선언돼 있으면 한 번만 「찹츄 선언됨」 을 알린다 (선언은 자동 — 누르는 것이 없다). 아니고 내 턴이면 뽑는다.
    /// 선언된 뒤에는 뽑지 않는다 — 10장을 지켜 다음 내 턴 시작에 이긴다.
    /// </summary>
    public static string ChapchuStep()
    {
        GameDebugInput input = Object.FindFirstObjectByType<GameDebugInput>();
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        MyPanelPresenter panel = Object.FindFirstObjectByType<MyPanelPresenter>();
        if (input == null || server == null || panel == null)
            return "WAIT 게임 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        if (!TryGetTurnActor(out int turnActor))
            return "WAIT 턴 정보 없음";

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if ((bool)typeof(GameDebugInput).GetField(GameEndedField, Private).GetValue(input))
            return "SKIP P" + me + " 게임 끝";

        int handCount = ((List<int>)typeof(GameDebugInput).GetField(MyHandField, Private).GetValue(input)).Count;
        if (ChapchuStateOf(panel) == "Declared")
        {
            string room = PhotonNetwork.CurrentRoom.Name + ":" + panel.GetInstanceID(); // 방 이름은 판마다 같다 (Debug) — 이 판의 화면 오브젝트로 가른다
            if (SessionState.GetString(ChapchuReportedKey, "") == room)
                return "SKIP P" + me + " 선언됨 — 10장을 지키며 내 턴을 기다림";
            SessionState.SetString(ChapchuReportedKey, room);
            return "OK P" + me + " 찹츄 선언됨 (손패 " + handCount + ")";
        }

        if (turnActor != me)
            return "SKIP P" + me + " 남의 턴(P" + turnActor + ")";

        double deadline = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.TurnDeadline, out object value) ? (double)value : -1;
        string turnKey = deadline.ToString("R");
        if (SessionState.GetString(ActedTurnKey, "") == turnKey)
            return "SKIP P" + me + " 처리 중";
        SessionState.SetString(ActedTurnKey, turnKey);

        server.RequestDraw();
        return "OK P" + me + " 뽑기 (손패 " + handCount + ")";
    }

    /// <summary>
    /// 찹츄 선언 연출 찍기 — 연출(ChapchuShoutView)이 켜지고 secondsCsv 초 뒤마다 prefix + "_<초>.png". 1.5초짜리라 CLI 부름 사이로는 놓친다 — 게임 중에 미리 걸어 둔다. 첫 선언 한 번만.
    /// </summary>
    public static string ArmShoutShots(string prefix, string secondsCsv)
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";
        ChapchuShoutView shout = Object.FindFirstObjectByType<ChapchuShoutView>(FindObjectsInactive.Include);
        if (shout == null) return "FAIL 씬에 ChapchuShoutView 가 없다";
        double[] times = secondsCsv.Split(',').Select(double.Parse).ToArray();
        double startedAt = -1;
        int taken = 0;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying || shout == null || taken >= times.Length)
            {
                EditorApplication.update -= tick;
                return;
            }
            if (startedAt < 0)
            {
                if (!shout.gameObject.activeInHierarchy) return;
                startedAt = EditorApplication.timeSinceStartup;
            }
            if (EditorApplication.timeSinceStartup - startedAt < times[taken]) return;
            ScreenCapture.CaptureScreenshot(prefix + "_" + times[taken].ToString("0.0") + ".png");
            taken++;
        };
        EditorApplication.update += tick;
        return "OK 찹츄 선언 연출 스크린샷 대기 → " + prefix + "_<" + secondsCsv + ">.png";
    }

    /// <summary>actor 의 찹츄 상태가 이 화면에 보일 때까지 WAIT — 내 것이면 찹츄 자리 「찹츄 완료」, 남의 것이면 그 좌석의 찹츄 표시.</summary>
    public static string ChapchuExpect(string actor)
    {
        MyPanelPresenter panel = Object.FindFirstObjectByType<MyPanelPresenter>();
        OpponentSeatsPresenter seats = Object.FindFirstObjectByType<OpponentSeatsPresenter>();
        if (panel == null || seats == null)
            return "WAIT 게임 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        int target = int.Parse(actor);
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (target == me)
        {
            string state = ChapchuStateOf(panel);
            return state == "Declared" ? "OK P" + me + " 내 찹츄 자리 「찹츄 완료」" : "WAIT P" + me + " 내 찹츄 자리 " + state;
        }

        var seatOf = (Dictionary<int, OpponentSeatView>)typeof(OpponentSeatsPresenter).GetField(SeatOfField, Private).GetValue(seats);
        if (!seatOf.TryGetValue(target, out OpponentSeatView seat))
            return "FAIL P" + target + " 의 좌석이 없다";

        bool shown = (bool)typeof(OpponentSeatView).GetField(SeatChapchuField, Private).GetValue(seat);
        return shown ? "OK P" + me + " 화면: P" + target + " 좌석 찹츄 표시" : "WAIT P" + me + " 화면: P" + target + " 좌석 찹츄 표시 없음";
    }

    private static string ChapchuStateOf(MyPanelPresenter panel)
    {
        FieldInfo info = typeof(MyPanelPresenter).GetField(PanelChapchuField, Private);
        if (info == null)
            throw new MissingFieldException(nameof(MyPanelPresenter), PanelChapchuField + " — 이름이 바뀌었으면 MultiTest.cs 의 표를 고친다");

        return (bool)info.GetValue(panel) ? "Declared" : "NotDeclared";
    }

    // ── 한 클라이언트 UI 흐름: 타이틀 → 로비 → 방 → 나가기 ──

    /// <summary>
    /// UI 한 단계. title = 닉네임 넣고 접속 / bad-code = 없는 방 코드로 참가 / warning = 경고 팝업 확인 + 토스트 /
    /// create = 방 만들기 / leave = 방 로그 읽고 나가기 / scene = 지금 씬 이름만.
    /// </summary>
    /// <summary>
    /// 남의 턴 클라이언트가 반응 시간에 카운터 요청을 보낸다 — 카운터 RPC 가 방장에 닿아 판정되는지 본다.
    /// 손패에 카운터가 있으면 그것을 낸다 → 행동 카드가 무효돼야 한다 (「카드 무효」 줄). 없으면 행동 카드를 보낸다 → 거절 코드 8.
    /// </summary>
    public static string CounterProbe()
    {
        GameDebugInput input = Object.FindFirstObjectByType<GameDebugInput>();
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (input == null || server == null || !TryGetTurnActor(out int turnActor))
            return "WAIT 게임 화면 아님";

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (turnActor == me)
            return "SKIP P" + me + " 내 턴";

        var hand = (List<int>)typeof(GameDebugInput).GetField(MyHandField, Private).GetValue(input);
        var instances = (List<int>)typeof(GameDebugInput).GetField(MyHandInstancesField, Private).GetValue(input);
        if (instances.Count == 0)
            return "SKIP P" + me + " 손패 없음";

        int counter = hand.FindIndex(cardId => server.Deck.GetCard(cardId)?.type == CardType.Counter);

        // 카운터 카드가 있고 대상을 고르지 않는 카드면 화면 경로로 — 손패의 그 카드의 사용을 청한다 (더블클릭과 같은 길). 초록 빛(방장이 알려 준 「낼 수 있는 카운터」)도 같이 본다
        if (counter >= 0)
        {
            TargetType targetType = server.Deck.GetCard(hand[counter]).targetType;
            CardView card = FindHandCard(instances[counter]);
            // 방장이 「지금 낼 수 있다」 고 알려 준 카운터만 화면 경로로 — 실제 카운터는 반응 조건이 있어 손에 있어도 못 낼 수 있다 (11-card-list 1-3)
            HandView handView = Object.FindFirstObjectByType<HandView>();
            bool usable = handView != null && ((HashSet<int>)typeof(HandView).GetField(HandUsableField, Private).GetValue(handView)).Contains(instances[counter]);
            if (!usable)
            {
                server.RequestPlayCounter(instances[counter], new int[0]);
                return "OK P" + me + " 반응 시간에 카운터 (지금 낼 수 없는 카운터 — 거절 기대)";
            }
            if (card != null && targetType != TargetType.OneOther && targetType != TargetType.TwoOthers)
            {
                var glow = (GameObject)typeof(CardView).GetField(CardGlowField, Private).GetValue(card);
                string glowLog = glow == null ? "빛 없음" : glow.activeSelf ? "초록 빛 켜짐" : "초록 빛 꺼짐";
                // 스킵 버튼 — 낼 수 있는 카운터가 있으면 반응 시간 동안 알약이 「스킵」 으로 켜진다 (10-ui 5절)
                ReactionTimerView pill = Object.FindFirstObjectByType<ReactionTimerView>();
                var skip = pill == null ? null : (UnityEngine.UI.Button)typeof(ReactionTimerView).GetField(SkipButtonField, Private).GetValue(pill);
                string skipLog = skip == null ? "스킵 버튼 없음" : skip.isActiveAndEnabled ? "스킵 버튼 켜짐 「" + ReadText(pill, PillLabelField) + "」" : "스킵 버튼 꺼짐";
                UseFromHand(instances[counter]);
                return "OK P" + me + " 반응 시간에 카운터 (카운터 카드) · 화면 · " + glowLog + " · " + skipLog;
            }
        }

        server.RequestPlayCounter(instances[Math.Max(counter, 0)], new int[0]);
        return "OK P" + me + (counter >= 0 ? " 반응 시간에 카운터 (카운터 카드)" : " 반응 시간에 카운터 (행동 카드)");
    }

    // 손패 뷰에서 그 인스턴스의 카드. 없으면 null.
    private static CardView FindHandCard(int instanceId)
    {
        HandView view = Object.FindFirstObjectByType<HandView>();
        if (view == null) return null;
        var cards = (List<CardView>)typeof(HandView).GetField(HandCardsField, Private).GetValue(view);
        return cards.FirstOrDefault(card => card.InstanceId == instanceId);
    }

    // 화면 경로 — 손패의 카드를 더블클릭한 것처럼 HandPresenter 에 사용을 청한다 (10-ui 4절 「사용 요청」). 카드가 손패 뷰에 없으면 false.
    private static bool UseFromHand(int instanceId)
    {
        HandPresenter presenter = Object.FindFirstObjectByType<HandPresenter>();
        CardView card = FindHandCard(instanceId);
        if (presenter == null || card == null) return false;

        typeof(HandPresenter).GetMethod(CardUseRequestedMethod, Private).Invoke(presenter, new object[] { card });
        return true;
    }

    /// <summary>내 함정 칸 0 을 누른 것처럼 — 크게 보기 카드가 떴는가. 내 함정이 없으면 SKIP.</summary>
    public static string TrapPreview()
    {
        MyTrapsPresenter mine = Object.FindFirstObjectByType<MyTrapsPresenter>();
        if (mine == null) return "WAIT 게임 화면 아님";
        if (mine.GetComponent<TrapSlotsView>().Count == 0) return "SKIP 내 함정 없음";

        typeof(MyTrapsPresenter).GetMethod(TrapSlotClickedMethod, Private).Invoke(mine, new object[] { 0 });
        var preview = (CardView)typeof(MyTrapsPresenter).GetField(TrapPreviewField, Private).GetValue(mine);
        return preview != null ? "OK 내 함정 크게 보기 — 카드 " + preview.CardId : "FAIL 칸을 눌렀는데 크게 보기 카드가 없다";
    }

    /// <summary>
    /// 나를 포함하는 카드(OneAny)의 대상 선택 (10-ui.md 6절) — ① SelfPickOpen: 선택을 열고 링 · 테가 켜졌는지.
    /// ② SelfPickTap: 내 탭 영역 가운데를 레이캐스트하면 맨 위가 내 패널 아래 물체인지(가림판 · 다른 HUD 에 가리지 않는다) 보고, 그 물체에 탭을 보내 내가 골라졌는지.
    /// 레이캐스트는 다음 프레임부터 — 막 올린 하위 캔버스는 한 번 그려져야 깊이가 정해진다. ③ SelfPickDone: 다음 프레임 뒤 선택이 닫히고 층이 내려갔는지.
    /// 손패 카드 없이 좌석 프레젠터를 직접 부른다 — 카드 요청은 rules 의 Effects(1006) 가 본다.
    /// </summary>
    public static string SelfPickOpen()
    {
        OpponentSeatsPresenter seats = Object.FindFirstObjectByType<OpponentSeatsPresenter>();
        MyPanelView panel = Object.FindFirstObjectByType<MyPanelView>();
        if (seats == null || panel == null) return "WAIT 게임 화면 아님";
        if (seats.IsSelectingTargets) return "WAIT 이미 대상 선택 중";

        seats.StartCoroutine(seats.SelectTargetsRoutine("핥아주기", 1, true, picked => SessionState.SetString("MultiTest.SelfPick", picked == null ? "null" : string.Join(",", picked))));
        SessionState.SetString("MultiTest.SelfPick", "pending");
        var so = new SerializedObject(panel);
        bool ring = ((GameObject)so.FindProperty("pickRing").objectReferenceValue).activeSelf, outline = ((GameObject)so.FindProperty("pickOutline").objectReferenceValue).activeSelf;
        return ring && outline ? "OK 선택 열림 · 링 · 테 켜짐" : "FAIL 선택을 열었는데 링 " + ring + " · 테 " + outline;
    }

    public static string SelfPickTap()
    {
        OpponentSeatsPresenter seats = Object.FindFirstObjectByType<OpponentSeatsPresenter>();
        MyPanelView panel = Object.FindFirstObjectByType<MyPanelView>();
        if (seats == null || panel == null) return "FAIL 게임 화면 아님";
        if (!seats.IsSelectingTargets) return "FAIL 선택이 닫혀 있다 (" + SessionState.GetString("MultiTest.SelfPick", "?") + ")";
        if (!SelfPickHit(panel, out string what, out GameObject top)) return "FAIL 내 패널이 가려졌다 — 맨 위 " + what;

        var click = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(top, click, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        var picked = (List<int>)typeof(OpponentSeatsPresenter).GetField(PickedField, Private).GetValue(seats);
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        return picked.Count == 1 && picked[0] == me ? "OK 내 패널 탭 → P" + me : "FAIL 탭했는데 고른 사람 [" + string.Join(",", picked) + "]";
    }

    public static string SelfPickDone()
    {
        MyPanelView panel = Object.FindFirstObjectByType<MyPanelView>();
        string result = SessionState.GetString("MultiTest.SelfPick", "?");
        var so = new SerializedObject(panel);
        var ring = (GameObject)so.FindProperty("pickRing").objectReferenceValue;
        var tap = (UnityEngine.UI.Image)so.FindProperty("tapArea").objectReferenceValue;
        var layers = so.FindProperty("pickLayers");
        bool lowered = true;
        for (int i = 0; i < layers.arraySize; i++)
            lowered &= !((Canvas)layers.GetArrayElementAtIndex(i).objectReferenceValue).overrideSorting;
        string me = PhotonNetwork.LocalPlayer.ActorNumber.ToString();
        if (result == "pending") return "WAIT 아직 닫히지 않음";
        return result == me && !ring.activeSelf && !tap.raycastTarget && lowered
            ? "OK 결과 [" + result + "] · 링 · 탭 · 층 원래대로"
            : "FAIL 결과 [" + result + "] · 링 " + ring.activeSelf + " · 탭 " + tap.raycastTarget + " · 층 내려감 " + lowered;
    }

    // 내 탭 영역 가운데의 레이캐스트 맨 위가 내 패널 아래 물체인가 + 링 · 테가 켜졌나
    private static bool SelfPickHit(MyPanelView panel, out string what, out GameObject top)
    {
        var so = new SerializedObject(panel);
        var ring = (GameObject)so.FindProperty("pickRing").objectReferenceValue;
        var outline = (GameObject)so.FindProperty("pickOutline").objectReferenceValue;
        var tap = (UnityEngine.UI.Image)so.FindProperty("tapArea").objectReferenceValue;
        Canvas.ForceUpdateCanvases();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, tap.rectTransform.TransformPoint(tap.rectTransform.rect.center));
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = point }, hits);
        top = hits.Count > 0 ? hits[0].gameObject : null;
        what = (top != null ? top.name + (top.transform.parent != null ? " (" + top.transform.parent.name + ")" : "") : "없음") + " · 링 " + ring.activeSelf + " · 테 " + outline.activeSelf;
        return top != null && top.transform.IsChildOf(panel.transform) && ring.activeSelf && outline.activeSelf;
    }

    /// <summary>
    /// 화면의 함정 칸 · 손패가 방장의 값과 같은가 — 내 함정 칸 수 · 상대 좌석마다의 함정 칸 수 = 방 속성 trapCount, 내 손패 뷰의 장수 = handCount.
    /// 연출이 다 끝난 뒤에 부른다.
    /// </summary>
    public static string TableUiExpect()
    {
        MyTrapsPresenter mine = Object.FindFirstObjectByType<MyTrapsPresenter>();
        OpponentSeatsPresenter seats = Object.FindFirstObjectByType<OpponentSeatsPresenter>();
        HandView handView = Object.FindFirstObjectByType<HandView>();
        if (mine == null || seats == null || handView == null)
            return "WAIT 게임 화면 아님";

        var wrong = new List<string>();
        var log = new StringBuilder();
        var trapsOf = (Dictionary<int, TrapSlotsView>)typeof(OpponentSeatsPresenter).GetField(TrapsOfField, Private).GetValue(seats);
        foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
        {
            int expected = player.CustomProperties.TryGetValue(PlayerProps.TrapCount, out object value) && value is int count ? count : 0;
            int shown;
            if (player.IsLocal)
                shown = mine.GetComponent<TrapSlotsView>().Count;
            else if (trapsOf.TryGetValue(player.ActorNumber, out TrapSlotsView slots))
                shown = slots.Count;
            else
                continue;

            log.Append(" P" + player.ActorNumber + " 함정 " + shown);
            if (shown != expected)
                wrong.Add("P" + player.ActorNumber + " 함정 칸 " + shown + " ≠ " + expected);
        }

        int handExpected = PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerProps.HandCount, out object handValue) && handValue is int handCount ? handCount : -1;
        log.Append(" · 내 손패 " + handView.Count + "장");
        if (handView.Count != handExpected)
            wrong.Add("내 손패 뷰 " + handView.Count + "장 ≠ " + handExpected);

        return wrong.Count > 0 ? "FAIL 화면 ≠ 방장: " + string.Join(" · ", wrong) : "OK 화면 = 방장 —" + log;
    }

    /// <summary>내 카드 다시 받기 (09-network.md 10절) — 방장이 보낸 지금 내 카드가 알림으로 따라온 기록과 같은지 GameDebugInput 이 로그로 남긴다.</summary>
    public static string SyncProbe()
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";

        server.RequestMySync();
        return "OK P" + PhotonNetwork.LocalPlayer.ActorNumber + " 내 카드 다시 받기 요청";
    }

    /// <summary>처리 번호 (09-network.md 5절) — 이미 받은 번호로 알림을 흉내 내면 버려져야 한다.
    /// 방장이 아닌 사람이 보낸 결과(network-rpc)도 새 번호라도 버려져야 한다. 둘 다 버려지면 OK.</summary>
    public static string StaleProbe()
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";

        Photon.Realtime.Player notMaster = PhotonNetwork.PlayerList.FirstOrDefault(p => !p.IsMasterClient);
        if (notMaster == null)
            return "WAIT 방장 말고 다른 사람 없음";

        int last = (int)typeof(PunGameServer).GetField("_receivedSeq", Private).GetValue(server);
        int raised = 0;
        Action<int, int> count = (a, h) => raised++;
        MethodInfo onHp = typeof(PunGameServer).GetMethod("RPC_OnHpChanged", Private);
        Chapchu.Network.Events.GameEvents.OnHpChanged += count;
        onHp.Invoke(server, new object[] { last, PhotonNetwork.LocalPlayer.ActorNumber, 1, new PhotonMessageInfo(PhotonNetwork.MasterClient, PhotonNetwork.ServerTimestamp, null) });
        int staleRaised = raised;
        onHp.Invoke(server, new object[] { last + 1, PhotonNetwork.LocalPlayer.ActorNumber, 1, new PhotonMessageInfo(notMaster, PhotonNetwork.ServerTimestamp, null) });
        Chapchu.Network.Events.GameEvents.OnHpChanged -= count;
        if (staleRaised > 0)
            return "FAIL 묵은 번호 " + last + " 가 반영됐다";
        if (raised > 0)
            return "FAIL 방장 아닌 P" + notMaster.ActorNumber + " 의 결과가 반영됐다";
        return "OK 묵은 번호 " + last + " 버림 · 방장 아닌 P" + notMaster.ActorNumber + " 의 결과 버림";
    }

    /// <summary>디버그 로비에서 「혼자 하기」 (18-bot.md 10절) — 봇 3 이 든 비공개 방을 만든다. 방 화면 이동은 DebugScript 가 한다.</summary>
    public static string Solo()
    {
        if (Object.FindFirstObjectByType<DebugScript>() == null)
            return "WAIT 디버그 로비 아님 (" + SceneManager.GetActiveScene().name + ")";

        if (PhotonNetwork.NetworkClientState != Photon.Realtime.ClientState.ConnectedToMasterServer)
            return "WAIT 연결 중 " + PhotonNetwork.NetworkClientState;

        NetworkManager.Instance.CreateSoloRoom();
        return "OK 혼자 하기 요청";
    }

    /// <summary>방의 봇 목록 · 정원 · 공개 여부가 기대와 같을 때까지 WAIT. bots = "1,2,3" 처럼.</summary>
    public static string ExpectBots(string bots, string maxPlayers, string visible)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "WAIT 방 없음";

        string now = string.Join(",", BotRoster.Numbers);
        string state = "봇 [" + now + "] · 정원 " + PhotonNetwork.CurrentRoom.MaxPlayers + " · 공개 " + PhotonNetwork.CurrentRoom.IsVisible + " · 열림 " + PhotonNetwork.CurrentRoom.IsOpen;
        if (now != bots || PhotonNetwork.CurrentRoom.MaxPlayers.ToString() != maxPlayers || PhotonNetwork.CurrentRoom.IsVisible.ToString() != visible)
            return "WAIT " + state;

        return "OK " + state;
    }

    public static string RemoveBot(string number) => NetworkManager.Instance.RemoveBot(int.Parse(number)) ? "OK 봇 " + number + " 제거 요청" : "FAIL 봇 제거 안 됨";

    /// <summary>방에 사람이 expected 명 모일 때까지 WAIT.</summary>
    public static string RoomCount(string expected) => PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount >= int.Parse(expected)
        ? "OK 방 인원 " + PhotonNetwork.CurrentRoom.PlayerCount
        : "WAIT 방 인원 " + (PhotonNetwork.CurrentRoom == null ? 0 : PhotonNetwork.CurrentRoom.PlayerCount) + " / " + expected;

    public static string AddBot() => NetworkManager.Instance.AddBot() ? "OK 봇 추가 요청" : "FAIL 봇 추가 안 됨";

    /// <summary>방 화면에서 봇 number 의 칸을 누른다 — 방장이면 난이도가 다음 것으로 바뀐다 (18-bot.md 8-1).</summary>
    public static string TapBotSlot(string number)
    {
        foreach (RoomSlotView slot in Object.FindObjectsByType<RoomSlotView>(FindObjectsSortMode.None))
        {
            var label = (TMP_Text)typeof(RoomSlotView).GetField(SlotNameField, Private).GetValue(slot);
            var button = (UnityEngine.UI.Button)typeof(RoomSlotView).GetField(SlotButtonField, Private).GetValue(slot);
            if (!label.gameObject.activeInHierarchy || label.text != "봇 " + number)
                continue;
            if (!button.interactable)
                return "FAIL 봇 " + number + " 칸이 눌리지 않는다";

            button.onClick.Invoke();
            var tag = (TMP_Text)typeof(RoomSlotView).GetField(SlotBotTagField, Private).GetValue(slot);
            return "OK 봇 " + number + " 칸 누름 → 꼬리표 「" + tag.text + "」";
        }

        return "WAIT 봇 " + number + " 칸이 없다";
    }

    /// <summary>봇 number 의 난이도가 level(Easy · Normal · Hard) 인가 — 방장이 읽는 값 · 방 속성(서버 확인) · 칸의 꼬리표 셋 다. 이름표는 닉네임 그대로.</summary>
    public static string ExpectBotLevel(string number, string level)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "WAIT 방 없음";

        int index = int.Parse(number) - 1;
        var want = (BotLevel)Enum.Parse(typeof(BotLevel), level);
        int inRoom = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.BotLevels, out object value) && value is int[] levels && levels.Length > index
            ? levels[index] : (int)BotLevel.Normal;
        string wantTag = "(봇 · " + (want == BotLevel.Easy ? "쉬움" : want == BotLevel.Hard ? "어려움" : "보통") + ")";
        bool shown = false;
        foreach (RoomSlotView slot in Object.FindObjectsByType<RoomSlotView>(FindObjectsSortMode.None))
        {
            var label = (TMP_Text)typeof(RoomSlotView).GetField(SlotNameField, Private).GetValue(slot);
            var tag = (TMP_Text)typeof(RoomSlotView).GetField(SlotBotTagField, Private).GetValue(slot);
            if (!label.gameObject.activeInHierarchy || label.text != "봇 " + number)
                continue;

            // 꼬리표가 켜져 있고 · 글자가 맞고 · 글자가 꼬리표 밖으로 넘치지 않는다
            float tagWidth = ((RectTransform)tag.transform.parent).rect.width;
            shown = tag.gameObject.activeInHierarchy && tag.text == wantTag && tag.preferredWidth <= tagWidth;
        }

        string state = "봇 " + number + " 난이도 " + BotRoster.LevelOf(index + 1) + " · 방 속성 " + (BotLevel)inRoom + " · 꼬리표 " + (shown ? "「" + wantTag + "」" : "다름");
        return (BotRoster.LevelOf(index + 1) == want && inRoom == (int)want && shown ? "OK " : "WAIT ") + state;
    }

    /// <summary>
    /// 게임이 끝나 결과 화면으로 온 뒤 — 모두(사람 = Player 속성, 봇 = 방 속성)의 공개 HP 가 시작 값으로 되돌려졌는가 (PunGameServer.ResetPublicProps).
    /// 지난 판 HP 가 남으면 다음 판 화면이 그 값을 보이고 기록에 가짜 「HP +N」 줄이 생긴다. 속성이 닿을 때까지 WAIT.
    /// </summary>
    public static string ExpectHpReset()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "WAIT 방 없음";

        var log = new StringBuilder();
        bool all = true;
        foreach (int actor in PhotonNetwork.PlayerList.Select(player => player.ActorNumber).Concat(BotRoster.Actors))
        {
            int hp = BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object value) && value is int number ? number : -1;
            log.Append(" P" + actor + " ♥" + hp);
            all &= hp == GameServer.MaxHp;
        }

        return (all ? "OK 공개 HP 시작 값 —" : "WAIT 공개 HP 가 시작 값이 아님 —") + log;
    }

    // ── 결과 화면 (ResultScene — 07-win-condition.md 11절) ──

    private static T ResultField<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);

    private static bool IsShown(object owner, string field)
    {
        object value = owner.GetType().GetField(field, Private).GetValue(owner);
        return value is GameObject go ? go.activeInHierarchy : ((Component)value).gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 결과 화면에 그려진 것 한 줄 — 제목 · 승리 방식 알약 + 시상대마다 (순위 순) 순위 · 이름 · HP 또는 탈락 줄 · 「…에게 당함」 · 왕관 · 찹츄 승리 마크 · 업적 배지
    /// + 「판 기록」 의 지표 줄마다 사람(순위 순) 값(1등은 *) → 업적.
    /// 「나」 표시 · 준비 · 나감은 화면마다 · 때마다 달라 넣지 않는다 — 에디터끼리 이 줄이 같아야 한다.
    /// </summary>
    public static string ResultSummary()
    {
        ResultPresenter presenter = Object.FindFirstObjectByType<ResultPresenter>();
        if (presenter == null)
            return "WAIT 결과 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        var podiums = ResultField<List<ResultPodiumView>>(presenter, ResultPodiumsField);
        if (podiums.Count == 0)
            return "WAIT 시상대가 아직 없다";

        var title = ResultField<ResultTitleView>(presenter, ResultTitleField);
        var line = new StringBuilder("OK 「" + ResultField<TMP_Text>(title, "titleText").text + "」 [" + ResultField<TMP_Text>(title, "reasonLabelText").text + " · "
            + ResultField<TMP_Text>(title, "reasonText").text + "]");
        foreach (ResultPodiumView podium in podiums)
        {
            line.Append(" | " + (IsShown(podium, "tiedChip") ? "공동 " : "") + ResultField<TMP_Text>(podium, "rankText").text + "위 " + ResultField<TMP_Text>(podium, "nameText").text);
            if (IsShown(podium, "hpBar")) line.Append(" ♥" + ResultField<TMP_Text>(podium, "hpText").text);
            if (IsShown(podium, "statText")) line.Append(" " + ResultField<TMP_Text>(podium, "statText").text);
            if (IsShown(podium, "subText")) line.Append(" (" + ResultField<TMP_Text>(podium, "subText").text + ")");
            if (IsShown(podium, "crown")) line.Append(" 왕관");
            if (IsShown(podium, "winMark")) line.Append(" 찹츄마크");
            var badges = ResultField<List<ResultBadgeView>>(podium, "_badges");
            if (badges.Count > 0)
                line.Append(" 배지[" + string.Join(", ", badges.Select(badge => ResultField<TMP_Text>(badge, "nameText").text + " " + ResultField<TMP_Text>(badge, "valueText").text)) + "]");
        }

        var board = ResultField<ResultBoardView>(presenter, ResultBoardField);
        foreach (ResultBoardRowView row in ResultField<List<ResultBoardRowView>>(board, "_rows"))
        {
            line.Append(" | " + ResultField<TMP_Text>(row, "metricText").text + " "
                + string.Join(" ", ResultField<ResultBoardRowView.Cell[]>(row, "cells").Where(cell => cell.root.activeSelf).Select(cell => cell.text.text + (cell.pill.enabled ? "*" : "")))
                + " → " + (ResultField<GameObject>(row, "award").activeSelf ? ResultField<TMP_Text>(row, "awardText").text : "없음"));
        }

        return line.ToString();
    }

    /// <summary>결과 화면의 준비 · 나감 · 버튼 상태 한 줄 (화면마다 다를 수 있다). 「한 판 더」 가 꺼져 있으면 "OK … 한 판 더 꺼짐".</summary>
    public static string ResultState()
    {
        ResultPresenter presenter = Object.FindFirstObjectByType<ResultPresenter>();
        if (presenter == null)
            return "WAIT 결과 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        var podiums = ResultField<List<ResultPodiumView>>(presenter, ResultPodiumsField);
        var buttons = ResultField<ResultButtonsView>(presenter, ResultButtonsField);
        var again = ResultField<UnityEngine.UI.Button>(buttons, "againButton");
        var line = new StringBuilder("OK");
        foreach (ResultPodiumView podium in podiums)
        {
            line.Append(" " + ResultField<TMP_Text>(podium, "nameText").text + (IsShown(podium, "meChip") ? "(나)" : "")
                + (IsShown(podium, "goneChip") ? "=나감" : IsShown(podium, "readyChip") ? "=준비" : "=대기"));
        }

        line.Append(" · 한 판 더 " + (again.interactable ? "켜짐" : "꺼짐") + " 「" + ResultField<TMP_Text>(buttons, "againText").text + " / " + ResultField<TMP_Text>(buttons, "againSubText").text
            + "」 · 로비로 「" + ResultField<TMP_Text>(buttons, "lobbySubText").text + "」");
        return line.ToString();
    }

    /// <summary>결과 화면의 버튼을 누른다 — which = again(「한 판 더」) · lobby(「로비로」) · board(「판 기록」). 등장 연출이 끝나 눌릴 때까지 WAIT.</summary>
    public static string ResultTap(string which)
    {
        ResultPresenter presenter = Object.FindFirstObjectByType<ResultPresenter>();
        if (presenter == null)
            return "WAIT 결과 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        var buttons = ResultField<ResultButtonsView>(presenter, ResultButtonsField);
        if (!ResultField<CanvasGroup>(buttons, "group").blocksRaycasts)
            return "WAIT 버튼이 아직 떠오르는 중";

        string label = which == "lobby" ? "로비로" : which == "board" ? "판 기록" : "한 판 더";
        var button = ResultField<UnityEngine.UI.Button>(buttons, which == "lobby" ? "lobbyButton" : which == "board" ? "boardButton" : "againButton");
        if (!button.interactable)
            return "FAIL 「" + label + "」 가 눌리지 않는다";

        button.onClick.Invoke();
        return "OK 「" + label + "」 누름";
    }

    /// <summary>「판 기록」 판 — want = close 면 ✕ 를 눌러 닫는다 (열려 있지 않았으면 FAIL). open · closed 면 그 상태가 될 때까지 WAIT.</summary>
    public static string ResultBoard(string want)
    {
        ResultPresenter presenter = Object.FindFirstObjectByType<ResultPresenter>();
        if (presenter == null)
            return "WAIT 결과 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        var board = ResultField<ResultBoardView>(presenter, ResultBoardField);
        int rows = ResultField<List<ResultBoardRowView>>(board, "_rows").Count;
        if (want == "close")
        {
            if (!board.IsOpen)
                return "FAIL 판 기록이 열려 있지 않다";
            ResultField<UnityEngine.UI.Button>(board, "closeButton").onClick.Invoke();
            return board.IsOpen ? "FAIL ✕ 를 눌러도 판 기록이 닫히지 않는다" : "OK 판 기록(지표 " + rows + "줄)을 ✕ 로 닫음";
        }

        return board.IsOpen == (want == "open") ? "OK 판 기록 " + (board.IsOpen ? "열림 — 지표 " + rows + "줄" : "닫힘") : "WAIT 판 기록이 " + (board.IsOpen ? "열려" : "닫혀") + " 있다";
    }

    /// <summary>결과 화면의 이모트 버튼을 누른다 — index = 0 부터. 등장 연출 · 쉬는 시간이 끝나 눌릴 때까지 WAIT. 답 끝에 내 이름 (「나 = 이름」).</summary>
    public static string ResultEmote(string index)
    {
        ResultPresenter presenter = Object.FindFirstObjectByType<ResultPresenter>();
        if (presenter == null)
            return "WAIT 결과 화면 아님 (" + SceneManager.GetActiveScene().name + ")";

        var emotes = ResultField<ResultEmoteView>(presenter, ResultEmotesField);
        CanvasGroup group = ResultField<CanvasGroup>(emotes, "group");
        if (!group.blocksRaycasts || !group.interactable)
            return "WAIT 이모트 버튼이 아직 눌리지 않는다";

        int i = int.Parse(index);
        ResultPodiumView mine = ResultField<List<ResultPodiumView>>(presenter, ResultPodiumsField).FirstOrDefault(podium => IsShown(podium, "meChip"));
        ResultField<UnityEngine.UI.Button[]>(emotes, "buttons")[i].onClick.Invoke();
        return "OK 이모트 " + i + " 「" + emotes.TextOf(i) + "」 누름 — 나 = " + (mine != null ? ResultField<TMP_Text>(mine, "nameText").text : "?");
    }

    /// <summary>
    /// 말풍선 지켜보기 — 시상대의 말풍선(이모트 · 「준비!」)이 뜰 때마다 「이름:글」 을 적어 둔다 (BubbleLog 로 읽는다).
    /// 말풍선은 2.4초면 사라져 CLI 부름 사이로는 놓친다. 결과 화면을 벗어나면 스스로 멈춘다.
    /// </summary>
    public static string ArmBubbleLog()
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";
        SessionState.SetString(BubbleLogKey, "");
        var showing = new Dictionary<ResultPodiumView, string>();
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            ResultPresenter presenter = EditorApplication.isPlaying ? Object.FindFirstObjectByType<ResultPresenter>() : null;
            if (presenter == null)
            {
                EditorApplication.update -= tick;
                return;
            }
            foreach (ResultPodiumView podium in ResultField<List<ResultPodiumView>>(presenter, ResultPodiumsField))
            {
                if (ResultField<CanvasGroup>(podium, "bubbleGroup").alpha <= 0.5f)
                {
                    showing.Remove(podium);
                    continue;
                }
                string text = ResultField<TMP_Text>(podium, "bubbleText").text;
                if (showing.TryGetValue(podium, out string last) && last == text)
                    continue;
                showing[podium] = text;
                SessionState.SetString(BubbleLogKey, SessionState.GetString(BubbleLogKey, "") + " " + ResultField<TMP_Text>(podium, "nameText").text + ":" + text);
            }
        };
        EditorApplication.update += tick;
        return "OK 말풍선 지켜보는 중";
    }

    /// <summary>ArmBubbleLog 뒤로 뜬 말풍선 — 「이름:글」 들.</summary>
    public static string BubbleLog() => "OK 말풍선 —" + SessionState.GetString(BubbleLogKey, " (지켜보지 않음)");

    /// <summary>
    /// 결과 화면 등장 연출 찍기 — 결과 씬이 열리고 secondsCsv 초 뒤마다 prefix + "_<초>.png" (색종이처럼 CLI 부름 사이로는 놓치는 것). 게임 중에 미리 걸어 둔다.
    /// </summary>
    public static string ArmResultShots(string prefix, string secondsCsv)
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";
        double[] times = secondsCsv.Split(',').Select(double.Parse).ToArray();
        double openedAt = -1;
        int taken = 0;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying || taken >= times.Length)
            {
                EditorApplication.update -= tick;
                return;
            }
            if (openedAt < 0)
            {
                if (Object.FindFirstObjectByType<ResultPresenter>() == null) return;
                openedAt = EditorApplication.timeSinceStartup;
            }
            if (EditorApplication.timeSinceStartup - openedAt < times[taken]) return;
            ScreenCapture.CaptureScreenshot(prefix + "_" + times[taken].ToString("0.0") + ".png");
            taken++;
        };
        EditorApplication.update += tick;
        return "OK 결과 화면 스크린샷 대기 → " + prefix + "_<" + secondsCsv + ">.png";
    }

    /// <summary>지금 게임 뷰의 그리는 횟수 — 화면을 붙였을 때 worklog 에 남기는 값 (CODE_CONVENTION 10절). 통과 · 실패 없이 숫자만.</summary>
    public static string Batches() => "OK batches " + UnityStats.batches + " · setPass " + UnityStats.setPassCalls + " (" + SceneManager.GetActiveScene().name + ")";

    /// <summary>방이 닫혀 있는가 — 결과 화면 · 다음 판은 방을 다시 열지 않는다 (08-room.md 4절).</summary>
    public static string ExpectRoomClosed()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return "WAIT 방 없음";

        return (PhotonNetwork.CurrentRoom.IsOpen ? "WAIT 방이 열려 있다" : "OK 방 닫힘") + " · 공개 " + PhotonNetwork.CurrentRoom.IsVisible;
    }

    /// <summary>가운데 체인 자리에 남은 카드 수 — 112 대소동의 새 판 뒤에는 0 이어야 한다 (ChainCardsPresenter 아래의 CardView).</summary>
    public static string ChainCardsEmpty()
    {
        ChainCardsPresenter chain = Object.FindFirstObjectByType<ChainCardsPresenter>();
        if (chain == null)
            return "WAIT 게임 화면 아님";

        int count = chain.GetComponentsInChildren<CardView>(true).Length;
        return count == 0 ? "OK 가운데 카드 없음" : "FAIL 가운데에 카드 " + count + "장이 남았다";
    }

    /// <summary>방장의 규칙 원본에서 모두의 체력을 hp 로 — 한 판을 빨리 끝내 보는 full 시나리오용. 화면 값은 다음 HpChanged 부터 맞는다.</summary>
    public static string SetAllHp(string hp)
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";

        if (!PhotonNetwork.IsMasterClient)
            return "FAIL 방장만 부른다";

        var rules = (GameServer)typeof(PunGameServer).GetField(PunServerField, Private).GetValue(server);
        var states = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(rules);
        foreach (PlayerState state in states.Values)
            state.Hp = int.Parse(hp);

        return "OK 모두 체력 " + hp + " — " + rules.DebugState();
    }

    // 방장: 지금 턴 주인 손패에 카드 한 장을 넣고 바로 쓴다 (드문 카드의 네트워크 경로 확인 — 1012 대소동). 결과는 실제 Send 로 보낸다
    public static string ForceCard(string cardId)
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";
        if (!PhotonNetwork.IsMasterClient)
            return "FAIL 방장만 부른다";

        var rules = (GameServer)typeof(PunGameServer).GetField(PunServerField, Private).GetValue(server);
        int actor = rules.CurrentTurnActor;
        if (actor == -1)
            return "WAIT 턴 없음";
        var states = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(rules);
        int id = int.Parse(cardId);
        Card c = server.Deck.CreateCards().FirstOrDefault(card => card.CardId == id);
        if (c == null)
            return "FAIL 덱에 카드 " + cardId + " 없음";

        var forced = new Card(90000 + id, c.CardId, c.Type, c.Target, c.Effects, c.ReactsTo, c.RevealSeconds, c.ResolveSeconds, c.Trigger, c.TriggerValue, c.Condition, c.TargetCondition);
        ((PlayerState)states[actor]).Hand.Add(forced);
        int[] targets = new int[0];
        if (c.Target == TargetType.OneOther)
        {
            foreach (int other in states.Keys)
            {
                if (other != actor && ((PlayerState)states[other]).Life == LifeState.Alive)
                {
                    targets = new[] { other };
                    break;
                }
            }
        }
        IReadOnlyList<GameEvent> events = rules.PlayCard(actor, forced.InstanceId, targets);
        string first = events.Count > 0 ? events[0].GetType().Name : "없음";
        typeof(PunGameServer).GetMethod("Send", Private).Invoke(server, new object[] { events });
        return first == "CardPlayed" ? "OK P" + actor + " 카드 " + cardId + " 사용" + (targets.Length > 0 ? " → P" + targets[0] : "") : "FAIL 사용 결과 " + first;
    }

    // 반응 시간의 스킵 버튼 (10-ui 5절 · 04-card 8절) — 방장 화면에서 본다. 남의 턴에 부른다.
    //   arm   = 내 손패에 낼 수 있는 카운터 1장을 쥐여 주고(화면 손패에도), 턴 주인이 나를 노리는 행동 카드를 내게 한다
    //   click = 반응 시간이 열렸으면 초록 빛 · 「스킵」 버튼이 켜졌는지 보고 버튼을 누른다 (Button.onClick — 화면 경로)
    //   after = 버튼 · 초록 빛이 꺼졌는지, 스킵한 뒤의 카운터가 거절되는지(방장 규칙에 직접)
    private const string SkipProbeKey = "MultiTest.SkipProbeCard";
    private const int SkipProbeInstance = 95000;
    private const string PickShotKey = "MultiTest.PickShot";

    /// <summary>counter pick 의 「pick」 단계가 좌석을 누르기 직전(대상 고르는 중)의 게임 뷰를 이 파일로 찍게 한다.</summary>
    public static string ArmPickShot(string path)
    {
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        SessionState.SetString(PickShotKey, path);
        return "OK 대상 고르는 중 스크린샷 → " + path;
    }

    private static float ChainLayerAlpha()
    {
        TopCardLayerView layer = Object.FindFirstObjectByType<TopCardLayerView>();
        if (layer == null) return -1f;
        return ((UnityEngine.UI.RawImage)typeof(TopCardLayerView).GetField("chainImage", Private).GetValue(layer)).color.a;
    }

    public static string ReactionSkipProbe(string step)
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";
        if (!PhotonNetwork.IsMasterClient)
            return "FAIL 방장만 부른다";

        var rules = (GameServer)typeof(PunGameServer).GetField(PunServerField, Private).GetValue(server);
        var states = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(rules);
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        ReactionTimerView pill = Object.FindFirstObjectByType<ReactionTimerView>();
        var skip = pill == null ? null : (UnityEngine.UI.Button)typeof(ReactionTimerView).GetField(SkipButtonField, Private).GetValue(pill);
        if (skip == null)
            return "FAIL 스킵 버튼이 씬에 없다";

        switch (step)
        {
            case "arm":
            case "arm2021":
            case "arm2018":
            case "arm2002":
            {
                int actor = rules.CurrentTurnActor;
                if (actor == -1 || actor == me)
                    return "WAIT 남의 턴이 아니다 (P" + actor + ")";

                SessionState.EraseBool("MultiTest.CostShot");
                List<Card> all = server.Deck.CreateCards().ToList();
                Card hit = all.FirstOrDefault(c => c.Type == CardType.Action && c.Target == TargetType.OneOther && c.TargetCondition == TargetCondition.None);
                // 게임 덱(Deck_Main)의 실제 카운터 — 아무 행동 카드에 내는 것 (2003 진짜 할거야?, 11-card-list 1-3)
                // arm2021 = 2021 으르렁 (나를 노린 카드에 — 대가로 손패 1장 고르기)
                // arm2018 = 2018 물귀신 (나에게 오는 피해에 — 낼 때 화면에서 1명을 고른다)
                // arm2002 = 2002 반사 (나를 노린 행동 카드에 — 막고 내가 쓴 사람에게 다시 쓴다)
                Card counter = step == "arm2002" ? all.FirstOrDefault(c => c.CardId == 2002) : step == "arm2021" ? all.FirstOrDefault(c => c.CardId == 2021) : step == "arm2018" ? all.FirstOrDefault(c => c.CardId == 2018) : all.FirstOrDefault(c => c.Type == CardType.Counter && c.Target == TargetType.None
                    && c.Condition == ReactCondition.Any && c.ReactsTo.Contains(CardType.Action));
                if (hit == null || counter == null)
                    return "FAIL 덱에 맞는 행동 · 카운터 카드가 없다";

                // 시험은 화면을 찍고 누르는 데 몇 초가 걸린다 — 대가 고르기 시간만 넉넉히 (규칙의 5초는 rules 가 본다)
                CardEffect[] counterEffects = counter.Effects.Select(e => e is ChooseDiscardEffect ? new ChooseDiscardEffect { seconds = 60 } : e).ToArray();
                var mine = new Card(SkipProbeInstance, counter.CardId, counter.Type, counter.Target, counterEffects, counter.ReactsTo, counter.RevealSeconds, counter.ResolveSeconds, counter.Trigger, counter.TriggerValue, counter.Condition, counter.TargetCondition);
                var forced = new Card(SkipProbeInstance + 1, hit.CardId, hit.Type, hit.Target, hit.Effects, hit.ReactsTo, hit.RevealSeconds, hit.ResolveSeconds, hit.Trigger, hit.TriggerValue, hit.Condition, hit.TargetCondition);
                ((PlayerState)states[me]).Hand.Add(mine);
                ((PlayerState)states[actor]).Hand.Add(forced);
                Object.FindFirstObjectByType<HandPresenter>().GainCards(new[] { SkipProbeInstance }, new[] { counter.CardId }); // 화면 손패에도 그 카드를

                IReadOnlyList<GameEvent> events = rules.PlayCard(actor, forced.InstanceId, new[] { me });
                bool asked = events.OfType<CountersUsable>().Any(u => u.Actor == me && u.CardInstanceIds.Contains(SkipProbeInstance));
                typeof(PunGameServer).GetMethod("Send", Private).Invoke(server, new object[] { events });
                if (!events.OfType<CardPlayed>().Any())
                    return "FAIL 카드 사용이 거절됨 — " + (events.Count > 0 ? events[0].GetType().Name : "없음");
                return asked ? "OK P" + actor + " 가 카드 " + hit.CardId + " → P" + me + " · 내 카운터 " + counter.CardId + " 를 물어 옴" : "FAIL 방장이 내 카운터를 묻지 않았다";
            }
            case "click":
            {
                CardView card = FindHandCard(SkipProbeInstance);
                if (card == null)
                    return "WAIT 손패에 카운터가 아직 없다";
                var glow = (GameObject)typeof(CardView).GetField(CardGlowField, Private).GetValue(card);
                if (!skip.isActiveAndEnabled)
                    return "WAIT 스킵 버튼이 아직 꺼져 있다 (반응 시간 전)";

                string label = ReadText(pill, PillLabelField);
                bool glowing = glow != null && glow.activeSelf;
                // 시험용 카운터는 게임 덱의 행동 카드 모양으로 그려진다 — 행동 카드 프리팹에는 초록 빛 오브젝트가 없어 빛은 보지 못한다.
                // 대신 손패 뷰가 그 카드를 「낼 수 있는 카운터」 로 알고 있는지 본다.
                HandView handView = Object.FindFirstObjectByType<HandView>();
                bool listed = ((HashSet<int>)typeof(HandView).GetField(HandUsableField, Private).GetValue(handView)).Contains(SkipProbeInstance);
                if (glow == null)
                    glowing = listed;
                bool secondsShown = ((TMP_Text)typeof(ReactionTimerView).GetField("secondsText", Private).GetValue(pill)).gameObject.activeInHierarchy;
                skip.onClick.Invoke();
                bool off = !skip.isActiveAndEnabled && !(glow != null && glow.activeSelf)
                    && ((HashSet<int>)typeof(HandView).GetField(HandUsableField, Private).GetValue(handView)).Count == 0;
                return (label == "스킵" && glowing && !secondsShown && off ? "OK" : "FAIL") + " 버튼 「" + label + "」 · 낼 수 있는 카운터 표시 " + (glowing ? "켜짐" : "꺼짐")
                    + " · 남은 초 " + (secondsShown ? "보임" : "안 보임") + " → 누른 뒤 " + (off ? "버튼 · 빛 꺼짐" : "아직 켜져 있다");
            }
            case "cost":
            {
                // 2021 의 대가 — 고르기 판에 내 손패가 카드 칸으로 떴는가 → 첫 칸을 누른다
                ChoicePopupView popup = Object.FindFirstObjectByType<ChoicePopupView>(FindObjectsInactive.Include);
                var panel = (GameObject)typeof(ChoicePopupView).GetField("panel", Private).GetValue(popup);
                var grid = (UnityEngine.UI.GridLayoutGroup)typeof(ChoicePopupView).GetField("cardsGrid", Private).GetValue(popup);
                var buttons = (GameObject)typeof(ChoicePopupView).GetField("buttonsRow", Private).GetValue(popup);
                if (!panel.activeInHierarchy)
                    return "WAIT 고르기 판이 아직 안 열렸다";
                ChoiceCardTile[] tiles = grid.GetComponentsInChildren<ChoiceCardTile>(false);
                int handCount = ((PlayerState)states[me]).Hand.Count;
                string title = ReadText(popup, "titleText");
                bool ok = grid.gameObject.activeInHierarchy && !buttons.activeSelf && tiles.Length == handCount && handCount > 0
                    && tiles.All(t => ((PlayerState)states[me]).Hand.Any(c => c.InstanceId == t.InstanceId));
                if (!ok)
                    return "FAIL 고르기 판 「" + title + "」 카드 칸 " + tiles.Length + " · 손패 " + handCount + " · 버튼 줄 " + (buttons.activeSelf ? "켜짐" : "꺼짐");
                int pick = tiles[0].InstanceId;
                SessionState.SetInt("MultiTest.CostPick", pick);
                SessionState.SetInt("MultiTest.CostHand", handCount);
                if (SessionState.GetBool("MultiTest.CostShot", false) == false) { SessionState.SetBool("MultiTest.CostShot", true); return "SHOT 고르기 판 「" + title + "」 카드 칸 " + tiles.Length + " = 손패 " + handCount; }
                SessionState.EraseBool("MultiTest.CostShot");
                ((UnityEngine.UI.Button)typeof(ChoiceCardTile).GetField("button", Private).GetValue(tiles[0])).onClick.Invoke();
                return "OK 고르기 판 「" + title + "」 카드 칸 " + tiles.Length + " = 손패 " + handCount + " → 카드 #" + pick + " 을 누름";
            }
            case "costdone":
            {
                ChoicePopupView popup = Object.FindFirstObjectByType<ChoicePopupView>(FindObjectsInactive.Include);
                var panel = (GameObject)typeof(ChoicePopupView).GetField("panel", Private).GetValue(popup);
                int pick = SessionState.GetInt("MultiTest.CostPick", -1), before = SessionState.GetInt("MultiTest.CostHand", -1);
                List<Card> hand = ((PlayerState)states[me]).Hand;
                if (panel.activeInHierarchy || hand.Any(c => c.InstanceId == pick))
                    return "WAIT 고른 카드가 아직 안 버려졌다";
                return (hand.Count == before - 1 ? "OK" : "FAIL") + " 고른 카드 #" + pick + " 이 버려짐 · 손패 " + before + " → " + hand.Count + " · 판 닫힘";
            }
            case "play":
            {
                // 실제 카운터를 화면 경로(더블클릭과 같은 길)로 낸다 — 반응 시간이 열린 뒤
                if (!skip.isActiveAndEnabled)
                    return "WAIT 스킵 버튼이 아직 꺼져 있다 (반응 시간 전)";
                return UseFromHand(SkipProbeInstance) ? "OK P" + me + " 카운터를 화면에서 냄" : "WAIT 손패에 카운터가 아직 없다";
            }
            case "pickclosed":
            {
                // 대상 고르기가 끝나면 체인 층(낸 카드)의 불투명도가 돌아온다 (10-ui.md 6절)
                float alpha = ChainLayerAlpha();
                return alpha < 0.99f ? "WAIT 체인 층 불투명도 " + alpha.ToString("0.00") : "OK 대상 고르기 끝 — 체인 층 불투명도 " + alpha.ToString("0.00");
            }
            case "pick":
            {
                // 대상을 고르는 카운터 (2018) — 손패에서 내면 대상 고르기가 열린다 → 상대 좌석을 누른다
                OpponentSeatsPresenter seats = Object.FindFirstObjectByType<OpponentSeatsPresenter>();
                if (seats == null || !seats.IsSelectingTargets)
                    return "WAIT 대상 고르기가 아직 안 열렸다";
                int other = states.Keys.Cast<int>().First(a => a != me);
                var seatOf = (Dictionary<int, OpponentSeatView>)typeof(OpponentSeatsPresenter).GetField("_seatOf", Private).GetValue(seats);
                if (!seatOf.TryGetValue(other, out OpponentSeatView seat))
                    return "FAIL P" + other + " 의 좌석이 없다";
                // 고르는 동안 체인 층(낸 카드)이 흐려져 상대 좌석을 가리지 않는다 (10-ui.md 6절). 한 번 부름에 같이 본다 — 반응 시간이 짧다
                float faded = ChainLayerAlpha();
                if (faded > 0.26f)
                    return "WAIT 체인 층 불투명도 " + faded.ToString("0.00");
                string shot = SessionState.GetString(PickShotKey, "");
                if (shot != "")
                {
                    SessionState.EraseString(PickShotKey);
                    ScreenCapture.CaptureScreenshot(shot); // 이 프레임 끝에 찍힌다 — 고르기가 아직 열려 있다
                }
                typeof(OpponentSeatsPresenter).GetMethod("HandleSeatClicked", Private).Invoke(seats, new object[] { seat });
                return "OK 대상 고르기가 열림 · 체인 층 불투명도 " + faded.ToString("0.00") + " → P" + other + " 의 좌석을 누름";
            }
            case "countered":
            {
                // 2003 — 막힌 행동 카드는 쓴 사람 손패로, 카운터는 버림 더미로, 나는 피해 없음 (11-card-list 1-3)
                var discard = (List<Card>)typeof(GameServer).GetField("_discardPile", Private).GetValue(rules);
                int actor = states.Keys.Cast<int>().First(a => a != me);
                bool returned = ((PlayerState)states[actor]).Hand.Any(c => c.InstanceId == SkipProbeInstance + 1);
                bool spent = discard.Any(c => c.InstanceId == SkipProbeInstance) && !((PlayerState)states[me]).Hand.Any(c => c.InstanceId == SkipProbeInstance);
                // 체인이 아직 처리 전 — 덱에 실제 카운터가 있어 상대가 내 카운터에 낼 카드(알아)를 쥐고 있으면 반응 시간 5초를 다 기다린다
                bool pending = ((System.Collections.ICollection)typeof(GameServer).GetField("_chain", Private).GetValue(rules)).Count > 0;
                if (!spent && (pending || FindHandCard(SkipProbeInstance) != null))
                    return "WAIT 아직 처리 전";
                int hp = ((PlayerState)states[me]).Hp;
                if (discard.Any(c => c.InstanceId == SkipProbeInstance && c.CardId == 2018))
                {
                    // 2018 — 막지 않는다. 나도 받고, 고른 사람(카드를 쓴 사람)도 같은 양
                    if (pending)
                        return "WAIT 아직 처리 전";
                    int otherHp = ((PlayerState)states[actor]).Hp;
                    return (hp < 100 && otherHp == hp ? "OK" : "FAIL") + " 물귀신 — 내 HP " + hp + " · 고른 사람 HP " + otherHp + " · 카운터 버림 더미";
                }
                if (discard.Any(c => c.InstanceId == SkipProbeInstance && c.CardId == 2002))
                {
                    // 2002 — 막힌 카드는 버림 더미로(반환 아님), 내가 쓴 사람에게 다시 써서 쓴 사람만 피해를 받는다
                    bool blocked = discard.Any(c => c.InstanceId == SkipProbeInstance + 1);
                    if (!blocked && pending)
                        return "WAIT 아직 처리 전";
                    int userHp = ((PlayerState)states[actor]).Hp;
                    return (blocked && !returned && hp == 100 && userHp < 100 ? "OK" : "FAIL") + " 반사 — 막힌 카드 " + (blocked ? "버림 더미로" : "버려지지 않음") + " · 내 HP " + hp + " · 쓴 사람 HP " + userHp;
                }
                if (discard.Any(c => c.InstanceId == SkipProbeInstance && c.CardId == 2021))
                {
                    // 2021 — 막힌 카드는 버림 더미로(반환 아님), 나는 피해 없음
                    bool blocked = discard.Any(c => c.InstanceId == SkipProbeInstance + 1);
                    if (!blocked && pending)
                        return "WAIT 아직 처리 전";
                    return (blocked && !returned && hp == 100 ? "OK" : "FAIL") + " 막힌 카드 " + (blocked ? "버림 더미로" : "버려지지 않음") + " · 카운터 버림 더미 · 내 HP " + hp;
                }
                return (returned && spent && hp == 100 ? "OK" : "FAIL") + " 막힌 카드 " + (returned ? "쓴 사람 손패로" : "돌아가지 않음") + " · 카운터 " + (spent ? "버림 더미" : "버려지지 않음") + " · 내 HP " + hp;
            }
            case "after":
            {
                // 스킵한 뒤에는 이번 반응 시간에 카운터를 받지 않는다 (PM 2026-11-19) — 이미 처리가 시작됐어도 코드 6
                IReadOnlyList<GameEvent> result = rules.PlayCounter(me, SkipProbeInstance, new int[0]);
                int code = result.Count == 1 && result[0] is RequestRejected rejected ? rejected.Code : 0;
                ((PlayerState)states[me]).Hand.RemoveAll(c => c.InstanceId == SkipProbeInstance);
                Object.FindFirstObjectByType<HandPresenter>().LoseCards(new[] { SkipProbeInstance });
                return (code == RejectCode.NotReactionTime && !skip.isActiveAndEnabled ? "OK" : "FAIL") + " 스킵 뒤 카운터 → 코드 " + code + " · 버튼 " + (skip.isActiveAndEnabled ? "켜짐" : "꺼짐");
            }
            default:
                return "FAIL 모르는 단계 " + step;
        }
    }

    // 방장: 지금 턴의 제한 시간을 끝낸다 — 다음 Tick 에 시간 초과로 넘어간다 (행동 없이 턴을 빨리 돌리기 — 1021 개판 바퀴)
    // mode "auto" = 실제 시간 초과 (자동 뽑기 · 연속 3번째 강제 퇴장, 03-turn 4절). "others" = 방장(나)의 턴은 그냥 넘기고 남의 턴만 실제 시간 초과.
    // 그 밖에는 그 턴 주인을 대상에서 빼서 예전처럼 턴만 넘긴다.
    // 떠 있는 공용 경고 팝업(PopupManager)의 「제목 / 사유」 — 강제 퇴장 뒤 로비 도착 (22-popup-ui 3절)
    // 방장 이주 시험 (run.sh migrate) — leave: 지금 방을 나간다 · stop: (방장) 사본을 더 보내지 않는다 · drop: (예비 방장) 받아 둔 사본을 버린다 · state: 내 상태
    public static string MigrateProbe(string step)
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";

        FieldInfo backup = typeof(PunGameServer).GetField("_backup", Private);
        FieldInfo started = typeof(PunGameServer).GetField("_started", Private);
        if (backup == null || started == null)
            return "FAIL PunGameServer 의 _backup · _started 필드 이름이 바뀌었다";

        switch (step)
        {
            case "leave":
                PhotonNetwork.LeaveRoom();
                return "OK 방을 나감 (방장 " + NetworkManager.IsTrustedMaster + ")";
            case "stop":
                if (!NetworkManager.IsTrustedMaster) return "WAIT 방장 아님";
                started.SetValue(server, false);
                return "OK 사본 보내기 멈춤";
            case "drop":
                backup.SetValue(server, null);
                return "OK 사본 버림";
            default:
                return "OK 방장 " + NetworkManager.IsTrustedMaster + " · 사본 " + (backup.GetValue(server) != null) + " · 판 보유 " + started.GetValue(server);
        }
    }

    /// <summary>기록 서랍에 그 글자의 줄이 있는가.</summary>
    public static string HistoryHas(string text)
    {
        HistoryPresenter history = Object.FindFirstObjectByType<HistoryPresenter>();
        if (history == null)
            return "WAIT 게임 화면 아님";

        var entries = (List<HistoryEntry>)typeof(HistoryPresenter).GetField("_entries", Private).GetValue(history);
        int count = entries.Count(e => e.Text == text);
        return count > 0 ? "OK 기록 「" + text + "」 " + count + "줄" : "WAIT 기록에 「" + text + "」 없음 (" + entries.Count + "줄)";
    }

    /// <summary>기록 서랍에 그 글자가 든 줄이 있는가 — 카드 이름 · 사람 이름이 섞인 줄 (카운터 기록 줄).</summary>
    public static string HistoryHasPart(string text)
    {
        HistoryPresenter history = Object.FindFirstObjectByType<HistoryPresenter>();
        if (history == null)
            return "WAIT 게임 화면 아님";

        var entries = (List<HistoryEntry>)typeof(HistoryPresenter).GetField("_entries", Private).GetValue(history);
        HistoryEntry found = entries.LastOrDefault(e => e.Text != null && e.Text.Contains(text));
        return found != null ? "OK 기록 「" + found.Text + "」" : "WAIT 기록에 「" + text + "」 이 든 줄 없음 (" + entries.Count + "줄)";
    }

    /// <summary>지금 방을 나간다 (어느 씬에서든).</summary>
    public static string LeaveRoomNow()
    {
        if (!PhotonNetwork.InRoom) return "WAIT 방 없음";
        bool master = NetworkManager.IsTrustedMaster;
        PhotonNetwork.LeaveRoom();
        return "OK 방을 나감 (방장 " + master + ")";
    }

    public static string InRoom() => "OK 방 안 " + PhotonNetwork.InRoom + " · " + SceneManager.GetActiveScene().name;

    public static string WarningPopupText()
    {
        WarningPopup popup = Object.FindFirstObjectByType<WarningPopup>();
        return popup == null ? "WAIT 경고 팝업 없음" : "OK " + ReadText(popup, WarningMainTextField) + " / " + ReadText(popup, WarningSubTextField);
    }

    public static string ExpireTurn(string mode = "")
    {
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (server == null)
            return "WAIT 게임 화면 아님";
        if (!PhotonNetwork.IsMasterClient)
            return "FAIL 방장만 부른다";

        var rules = (GameServer)typeof(PunGameServer).GetField(PunServerField, Private).GetValue(server);
        if (rules.IsGameOver)
            return "SKIP 게임 끝";
        var noTimeout = (HashSet<int>)typeof(GameServer).GetField(NoTimeoutField, Private).GetValue(rules);
        if (mode == "others")
            mode = rules.CurrentTurnActor == PhotonNetwork.LocalPlayer.ActorNumber ? "" : "auto";
        if (mode == "mine") // 방장 자신의 턴만 시간 초과 (방장 강제 퇴장 → 이주)
            mode = rules.CurrentTurnActor == PhotonNetwork.LocalPlayer.ActorNumber ? "auto" : "";
        if (mode == "auto")
            noTimeout.Remove(rules.CurrentTurnActor);
        else
            noTimeout.Add(rules.CurrentTurnActor);
        typeof(GameServer).GetField(TurnDeadlineField, Private).SetValue(rules, 0d);
        int streak = ((PlayerState)((System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(rules))[rules.CurrentTurnActor]).TimeoutStreak;
        return "OK P" + rules.CurrentTurnActor + " 턴 시간 끝" + (mode == "auto" ? " (자동 행동 · 연속 " + streak + " → " + (streak + 1) + ")" : "") + " — " + rules.DebugState();
    }

    // 1019 목표 조준 — 고르기 시작 알림을 기다렸다가 (5초 안에 골라야 해서 CLI 부름 사이로는 늦다 — 에디터 안에서 바로 한다)
    // 고르는 사람이면 판이 열린 것을 보고 ChoiceClickDelay 뒤 pick("0" · "1") 버튼을 누른다 (화면 경로 — Button.onClick).
    // 남이면 ChoiceLookDelay 뒤 반응 타이머 알약이 「{이름} 고르는 중」 인지 본다. shot 이 있으면 그 순간 게임 뷰를 찍는다. 결과는 ChoiceResult.
    private const string ChoiceResultKey = "MultiTest.Choice";
    private const double ChoiceLookDelay = 1.5;
    private const double ChoiceClickDelay = 2.5;

    public static string ArmChoice(string pick, string shot)
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";
        SessionState.SetString(ChoiceResultKey, "WAIT 고르기 시작 전");

        Action<int, int, int, int, int, double, double, int> onStarted = null;
        onStarted = (actor, user, cardId, handCount, damage, startsAt, deadline, kind) =>
        {
            Chapchu.Network.Events.GameEvents.OnChoiceStarted -= onStarted;
            bool mine = actor == PhotonNetwork.LocalPlayer.ActorNumber;
            double startedAt = EditorApplication.timeSinceStartup;
            bool shotTaken = false;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                double elapsed = EditorApplication.timeSinceStartup - startedAt;
                if (!shotTaken && elapsed >= ChoiceLookDelay)
                {
                    shotTaken = true;
                    if (!string.IsNullOrEmpty(shot) && shot != "-")
                        ScreenCapture.CaptureScreenshot(shot);
                    if (!mine)
                    {
                        EditorApplication.update -= tick;
                        ReactionTimerView timer = Object.FindFirstObjectByType<ReactionTimerView>();
                        var text = timer == null ? null : (TMP_Text)typeof(ReactionTimerView).GetField(TimerLabelField, Private).GetValue(timer);
                        SessionState.SetString(ChoiceResultKey, text != null && text.gameObject.activeInHierarchy && text.text.EndsWith("고르는 중")
                            ? "SEEN 「" + text.text + "」" : "FAIL 「고르는 중」 이 안 보인다 (알약 「" + (text == null ? "없음" : text.text) + "」)");
                    }
                    return;
                }
                if (!mine || elapsed < ChoiceClickDelay)
                    return;

                EditorApplication.update -= tick;
                ChoicePopupView popup = Object.FindFirstObjectByType<ChoicePopupView>();
                if (popup == null || !popup.IsOpen)
                {
                    SessionState.SetString(ChoiceResultKey, "FAIL 고르기 판이 열리지 않았다");
                    return;
                }
                var button = (UnityEngine.UI.Button)typeof(ChoicePopupView).GetField(pick == "0" ? ChoiceFirstButtonField : ChoiceSecondButtonField, Private).GetValue(popup);
                string label = button.GetComponentInChildren<TMP_Text>(true).text;
                button.onClick.Invoke();
                SessionState.SetString(ChoiceResultKey, "OPEN 고르기 판에서 「" + label + "」 누름 · 닫힘 " + !popup.IsOpen);
            };
            EditorApplication.update += tick;
        };
        Chapchu.Network.Events.GameEvents.OnChoiceStarted += onStarted;
        return "OK 고르기 대기";
    }

    public static string ChoiceResult() => SessionState.GetString(ChoiceResultKey, "WAIT 준비 안 됨");

    public static string Ui(string step)
    {
        string scene = SceneManager.GetActiveScene().name;
        switch (step)
        {
            case "title":
            {
                // 계정(Google · Apple) 로그인이 저장돼 있으면 누르지 않아도 로비로 간다. 게스트는 매번 터치 → (처음만 약관 동의) → 게스트 → 닉네임 → 시작
                TitlePresenter presenter = Object.FindFirstObjectByType<TitlePresenter>();
                if (presenter == null)
                    return scene == "LobbyScene" ? "OK 자동 로그인 → 로비" : "WAIT 타이틀 아님 (" + scene + ")";

                // 플레이 기록 수집 팝업(처음만) — 「동의」. 개발 빌드라 보내지는 않고 콘솔에만 찍힌다 (27 3절 #7)
                TitlePopupView popup = Object.FindFirstObjectByType<TitlePopupView>();
                if (popup != null && popup.Current == TitlePopup.PlayRecord)
                {
                    Call(popup, PlayRecordAgreeMethod);
                    return "WAIT 플레이 기록 동의";
                }

                LoginSheetView sheet = Object.FindFirstObjectByType<LoginSheetView>();
                TitleBottomView bottom = Object.FindFirstObjectByType<TitleBottomView>();
                if (!sheet.IsOpen)
                {
                    if (!bottom.IsWaitingForTap)
                        return "WAIT 자동 로그인 · 접속 중";

                    Call(presenter, TappedMethod);
                    return "WAIT 화면 터치";
                }

                switch (sheet.Step)
                {
                    case LoginSheetStep.Agree:
                        TermsAgreeView terms = Object.FindFirstObjectByType<TermsAgreeView>();
                        Call(terms, AllAgreeMethod);
                        Call(terms, AgreeConfirmMethod);
                        return "WAIT 약관 동의";
                    case LoginSheetStep.Login:
                        Call(sheet, GuestClickedMethod);
                        return "WAIT 게스트로 시작";
                    case LoginSheetStep.Nickname:
                        Object.FindFirstObjectByType<NicknameFieldView>().SetText("테스터");
                        Call(sheet, StartClickedMethod);
                        return "OK 닉네임 「테스터」 로 시작";
                    case LoginSheetStep.Fail:
                        return "FAIL 로그인 창 실패 — 콘솔을 본다";
                    default:
                        return "WAIT 로그인 창 " + sheet.Step;
                }
            }
            case "bad-code":
            {
                LobbyRoomRequester presenter = Object.FindFirstObjectByType<LobbyRoomRequester>();
                if (presenter == null || !PhotonNetwork.IsConnectedAndReady)
                    return "WAIT 로비 아님 (" + scene + ")";

                Call(presenter, JoinRoomRequestedMethod);
                LobbyModalView modal = Object.FindFirstObjectByType<LobbyModalView>();
                if (modal == null || !modal.IsInputOpen)
                    return "FAIL 방 코드 입력 창이 안 열림";

                ((TMP_InputField)typeof(LobbyModalView).GetField(ModalFieldField, Private).GetValue(modal)).text = "ZZZZ";
                Call(modal, SubmitClickedMethod);
                return "OK 없는 방 코드 ZZZZ 제출";
            }
            case "warning":
            {
                LobbyModalView modal = Object.FindFirstObjectByType<LobbyModalView>();
                if (modal == null || !modal.IsWarnOpen)
                    return "WAIT 경고 창 없음";

                string text = ReadText(modal, WarnTitleField) + " / " + ReadText(modal, WarnMessageField);
                Call(modal, WarnOkClickedMethod);
                PopupManager.Instance.ShowToast("토스트 확인");
                return "OK 경고 \"" + text + "\" 확인 · 토스트 띄움";
            }
            case "create":
            {
                LobbyRoomRequester presenter = Object.FindFirstObjectByType<LobbyRoomRequester>();
                if (presenter == null)
                    return "WAIT 로비 아님 (" + scene + ")";

                Call(presenter, CreateRoomRequestedMethod);
                return "OK 방 만들기";
            }
            case "leave":
            {
                RoomPresenter room = Object.FindFirstObjectByType<RoomPresenter>();
                if (room == null)
                    return "WAIT 방 아님 (" + scene + ")";

                string log = ReadText(Object.FindFirstObjectByType<RoomLogView>(), RoomLastLogField);
                Call(room, LeaveClickedMethod);
                return "OK 방 로그 \"" + log + "\" → 나가기";
            }
            default:
                return "OK " + scene;
        }
    }

    // ── 규칙 계층만 (네트워크 없음) ──

    /// <summary>
    /// GameServer 를 직접 돌린다. players 명이 덱 에셋으로 시작해 rounds 턴 동안 행동 카드(없으면 뽑기)를 다음 사람에게 쓰고,
    /// 마지막에 남의 턴 요청이 거절되는지 본다. 피해량은 카드 에셋 값과 비교한다.
    /// </summary>
    public static string Rules(string players, string rounds)
    {
        DeckData deck = FindDeck();
        if (deck == null)
            return "FAIL 덱 에셋 없음";

        int[] actors = Enumerable.Range(1, int.Parse(players)).ToArray();
        double now = 0;
        var server = new GameServer(new System.Random(1), () => now);
        server.StartGame(actors, deck.CreateCards());

        var states = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(server);
        var sb = new StringBuilder();
        bool failed = false;
        sb.AppendLine("시작 " + server.DebugState());

        for (int round = 0; round < int.Parse(rounds); round++)
        {
            int me = server.CurrentTurnActor;
            List<Card> hand = ((PlayerState)states[me]).Hand;
            Card card = hand.Find(c => c.Type == CardType.Action && c.Effects.Count > 0);
            if (card == null)
            {
                server.Draw(me);
                sb.AppendLine("P" + me + " 뽑기");
                continue;
            }

            int target = actors[(Array.IndexOf(actors, me) + 1) % actors.Length];
            int before = ((PlayerState)states[target]).Hp;
            IReadOnlyList<GameEvent> events = server.PlayCard(me, card.InstanceId, new[] { target });
            bool waits = ((PlayerState)states[target]).Hp == before; // 체인 처리 전에는 아직 그대로

            // 반응 마감 → 체인 처리(효과) → 후처리 시간 → 다음 턴 (04-card.md 8절)
            now += card.RevealSeconds + ReactionSeconds;
            var resolved = server.Tick();
            int after = ((PlayerState)states[target]).Hp;
            now += card.ResolveSeconds;
            bool nextTurn = server.Tick().Any(e => e is TurnStarted) && server.CurrentTurnActor != me;
            int expected = ExpectedDamage(deck, card.CardId);

            bool ok = events.Any(e => e is CardPlayed) && waits && resolved.Any(e => e is CardResolved) && nextTurn
                      && before - after == Math.Min(expected, before);
            failed |= !ok;
            sb.AppendLine((ok ? "" : "FAIL ") + "P" + me + " " + card + " → P" + target + " ♥" + before + "→" + after + " (기대 -" + expected + ")");
        }

        int other = actors.First(a => a != server.CurrentTurnActor);
        IReadOnlyList<GameEvent> rejected = server.PlayCard(other, ((PlayerState)states[other]).Hand[0].InstanceId, new[] { server.CurrentTurnActor });
        bool rejectOk = rejected.Count == 1 && rejected[0] is RequestRejected;
        failed |= !rejectOk;
        sb.AppendLine((rejectOk ? "" : "FAIL ") + "남의 턴 요청 → " + rejected[0].GetType().Name);

        // 참가자가 아닌 사람의 요청 — 예외 없이 거절 10. 시작 전 원본(방장의 첫 프레임 · 방장이 바뀐 뒤)과 진행 중인 게임의 모르는 번호
        const int stranger = 99;
        var notStarted = new GameServer(new System.Random(1), () => now);
        var requests = new List<Func<IReadOnlyList<GameEvent>>>
        {
            () => notStarted.PlayCounter(actors[0], 1, null)
        };
        if (!server.IsGameOver)
            requests.AddRange(new Func<IReadOnlyList<GameEvent>>[] { () => server.PlayCounter(stranger, 1, null) });

        bool strangerOk;
        try
        {
            strangerOk = requests.All(request => request() is var r && r.Count == 1 && r[0] is RequestRejected rr && rr.Code == RejectCode.NotAlive);
        }
        catch (Exception e)
        {
            strangerOk = false;
            sb.AppendLine("FAIL 참가자가 아닌 요청에서 예외 — " + e.GetType().Name);
        }
        failed |= !strangerOk;
        sb.AppendLine((strangerOk ? "" : "FAIL ") + "참가자가 아닌 사람의 카운터 · 함정 발동 요청 → 10 (" + requests.Count + "건)");

        // 남의 닉네임 표시 거르기 — 서식 태그 · 제어 문자 · 길이. 검사를 통과한 이름은 그대로
        var names = new (string raw, string shown)[]
        {
            ("배고픈찹츄42", "배고픈찹츄42"), ("Player#2", "Player#2"), ("<b>방장</b><size=200>", "b방장/bsize=200"),
            ("a​b‮c\nd", "abcd"), ("  <>  ", ""), (new string('가', 40), new string('가', NicknameValidator.MaxLength)),
            (new string('a', NicknameValidator.MaxLength - 1) + "\U0001F600", new string('a', NicknameValidator.MaxLength - 1)),
        };
        var badNames = names.Where(n => NicknameValidator.ForDisplay(n.raw) != n.shown).Select(n => NicknameValidator.ForDisplay(n.raw)).ToList();
        failed |= badNames.Count > 0;
        sb.AppendLine((badNames.Count == 0 ? "" : "FAIL ") + "닉네임 표시 거르기 " + (names.Length - badNames.Count) + "/" + names.Length + (badNames.Count == 0 ? "" : " — 틀림: " + string.Join(" | ", badNames)));
        sb.Append("끝 " + server.DebugState());

        return (failed ? "FAIL\n" : "OK\n") + sb;
    }

    /// <summary>
    /// 규칙 계층의 체인 · 카운터 (04-card.md 7 ~ 9절, 기능 5). 카드는 메모리에서 만들고 손패를 정해 준다 — 에셋과 무관.
    /// 연출 시간은 등장 1.46초 · 후처리 1.28초 (10-ui.md 5-1 기본값).
    /// </summary>
    public static string Chain()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        // 1. 아무도 반응하지 않음 — 반응 마감 전엔 그대로, 마감에 효과, 후처리 뒤 다음 턴. 처리 중 메인 행동은 거절
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + ReactionSeconds - 0.1);
            Check(t.Tick().Count == 0 && t.Hp(t.O1) == 100, "마감 전 — 효과 없음");
            Check(t.Code(t.Server.Draw(t.Turn)) == RejectCode.MainActionUsed, "처리 중 뽑기 → 7");
            Check(t.Code(t.Play(t.Turn, ChainTable.Hit, t.O2)) == RejectCode.MainActionUsed, "처리 중 행동 카드 → 7");
            t.At(ChainTable.Reveal + ReactionSeconds);
            IReadOnlyList<GameEvent> resolved = t.Tick();
            Check(resolved.OfType<CardResolved>().Count() == 1 && t.Hp(t.O1) == 80 && !resolved.OfType<TurnStarted>().Any(), "마감 — 효과 적용 · 턴은 아직");
            t.At(ChainTable.Reveal + ReactionSeconds + ChainTable.Resolve);
            Check(t.Tick().OfType<TurnStarted>().Any() && t.Server.CurrentTurnActor != t.Turn, "후처리 뒤 다음 턴");
        }

        // 2. 연출 중 카운터는 거절, 열린 뒤 카운터 → 무효. 반응 시간은 카운터 기준으로 다시
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(0.5);
            Check(t.Code(t.Counter(t.O1, ChainTable.Negate)) == RejectCode.NotReactionTime, "연출 중 카운터 → 6");
            t.At(1.5);
            IReadOnlyList<GameEvent> counter = t.Counter(t.O1, ChainTable.Negate);
            ReactionStarted reaction = counter.OfType<ReactionStarted>().FirstOrDefault();
            Check(reaction != null && Math.Abs(reaction.OpensAt - (1.5 + ChainTable.Reveal)) < 1e-6, "카운터 → 반응 시간 다시 (연출 뒤 — 마감은 알림에 없다)");
            t.At(ChainTable.Reveal + ReactionSeconds);
            Check(t.Tick().Count == 0, "처음 마감이 지나도 처리 안 함");
            t.At(1.5 + ChainTable.Reveal + ReactionSeconds);
            List<GameEvent> events = t.Tick().ToList();
            int resolvedAt = events.FindIndex(e => e is CardResolved);
            int negatedAt = events.FindIndex(e => e is CardNegated);
            Check(resolvedAt >= 0 && negatedAt > resolvedAt && t.Hp(t.O1) == 100, "위에서부터 — 카운터 처리 → 행동 카드 무효 · 피해 없음");
            Check(t.DiscardCount == 2, "무효된 카드도 버림 더미로");
            Check(t.Discard[0].CardId == ChainTable.Hit && t.Discard[1].CardId == ChainTable.Negate, "쓴 순서대로 쌓인다 — 맨 위 = 마지막에 낸 카운터");
        }

        // 3. 카운터의 카운터 — C 가 B 를 무효 → A 정상 (04 9.1)
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(1.5);
            t.Counter(t.O1, ChainTable.Negate);
            t.At(1.5 + ChainTable.Reveal + 0.1);
            Check(!(t.Counter(t.O2, ChainTable.Negate)[0] is RequestRejected), "카운터에 카운터");
            t.At(100);
            t.Tick();
            Check(t.Hp(t.O1) == 80, "C → B 무효 → A 적용 (♥80)");
        }

        // 4. 본인에게만 무효 (C05) — 맞은 두 사람 중 쓴 사람만 빠진다
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Wave, null);
            t.At(1.5);
            t.Counter(t.O1, ChainTable.SelfOnly);
            t.At(100);
            CardResolved wave = t.Tick().OfType<CardResolved>().LastOrDefault();
            Check(t.Hp(t.O1) == 100 && t.Hp(t.O2) == 80 && wave != null && !wave.Targets.Contains(t.O1), "본인만 무효 — 쓴 사람 ♥100 · 다른 사람 ♥80");
        }

        // 5. 거절 코드 — 반응할 수 없는 카운터 · 카운터 아닌 카드 · 마감 뒤 · 체인 없음
        {
            var t = new ChainTable();
            Check(t.Code(t.Counter(t.O1, ChainTable.Negate)) == RejectCode.NotReactionTime, "체인 없음 → 6");
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(2);
            Check(t.Code(t.Counter(t.O1, ChainTable.TrapOnly)) == RejectCode.CannotReact, "함정에만 반응하는 카운터 → 9");
            Check(t.Code(t.Counter(t.O1, ChainTable.Hit)) == RejectCode.NotCounterCard, "행동 카드로 카운터 → 8");
            t.At(ChainTable.Reveal + ReactionSeconds);
            Check(t.Code(t.Counter(t.O1, ChainTable.Negate)) == RejectCode.NotReactionTime, "마감 시각 → 6");
        }

        // 6. 처리 중 턴 제한 시간은 멈춘다 — 29초에 내도 30초에 넘어가지 않는다 (03-turn.md 4절)
        {
            var t = new ChainTable();
            t.At(TurnSeconds - 1);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(TurnSeconds + 0.5);
            Check(!t.Tick().OfType<TurnStarted>().Any() && t.Server.CurrentTurnActor == t.Turn, "30초가 지나도 턴 유지");
            t.At(TurnSeconds - 1 + ChainTable.Reveal + ReactionSeconds);
            t.Tick();
            Check(t.Hp(t.O1) == 80, "체인 처리");
        }

        // 7. 카드를 낸 사람이 나감 — 체인은 끝까지, 그 뒤 그 사람 다음 차례로
        {
            var t = new ChainTable();
            int user = t.Turn;
            t.Play(user, ChainTable.Hit, t.O1);
            Check(!t.Server.RemovePlayer(user).OfType<TurnStarted>().Any(), "나가도 처리 중엔 턴 유지");
            t.At(ChainTable.Reveal + ReactionSeconds);
            t.Tick();
            t.At(100);
            t.Tick();
            Check(t.Hp(t.O1) == 80 && t.Server.CurrentTurnActor == t.NextOf(user), "끝까지 처리 → 다음 차례 P" + t.Server.CurrentTurnActor);
        }

        // ── 반응 시간: 5초 · 스킵 · 고민 연장 (04-card 8절, 2026-11-15 · PM 2026-11-19) ──
        double opens = ChainTable.Reveal;
        bool Resolved(IEnumerable<GameEvent> events) => events.OfType<CardResolved>().Any();

        // 낼 수 있는 사람이 없으면 반응 시간을 열자마자(0초) 처리한다
        {
            var t = new ChainTable();
            foreach (int actor in new[] { t.Turn, t.O1, t.O2 })
            {
                t.Hand(actor).Clear();
                t.GiveCard(actor, ChainTable.Hit);
            }
            IReadOnlyList<GameEvent> played = t.Play(t.Turn, ChainTable.Hit, t.O1);
            Check(!played.OfType<CountersUsable>().Any(), "카운터가 아무에게도 없다 — 물을 사람 없음");
            t.At(opens - 0.1);
            Check(!Resolved(t.Tick()), "연출 중에는 처리하지 않는다");
            t.At(opens);
            Check(Resolved(t.Tick()) && t.Hp(t.O1) < 100, "낼 사람이 없으면 연출 직후 바로 처리 (0초)");
        }

        // 모두 스킵하면 남은 시간을 기다리지 않는다 · 스킵한 사람의 카운터는 받지 않는다
        {
            var t = new ChainTable();
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            Check(asked.Length >= 2, "낼 수 있는 사람에게만 목록 — " + asked.Length + "명");
            t.At(opens - 0.1);
            t.Server.SkipReaction(asked[0].Actor);
            t.At(opens + 0.5);
            Check(!Resolved(t.Tick()), "연출 중의 스킵은 버린다 — 아직 처리 없음");
            for (int i = 0; i < asked.Length - 1; i++)
                t.Server.SkipReaction(asked[i].Actor);
            Check(!Resolved(t.Tick()), "한 명이 남았으면 기다린다");
            Check(t.Code(t.Server.PlayCounter(asked[0].Actor, asked[0].CardInstanceIds[0], new int[0])) == RejectCode.NotReactionTime, "스킵한 사람의 카운터 → 6");
            t.Server.SkipReaction(asked[asked.Length - 1].Actor);
            Check(Resolved(t.Tick()), "모두 스킵 → 바로 처리 (열린 지 0.5초)");
        }

        // 스킵하지 않으면 5초 마감 · 물어 오지 않은 사람의 스킵은 버린다
        {
            var t = new ChainTable();
            t.Hand(t.O2).Clear();
            t.GiveCard(t.O2, ChainTable.Hit);
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            Check(asked.All(a => a.Actor != t.O2), "카운터가 없는 사람은 묻지 않는다");
            t.At(opens + 1);
            t.Server.SkipReaction(t.O2);
            t.At(opens + ReactionSeconds - 0.1);
            Check(!Resolved(t.Tick()), "마감 전 — 처리 없음 (5초)");
            t.At(opens + ReactionSeconds);
            Check(Resolved(t.Tick()), "5초 마감 → 처리");
        }

        // 고민 연장 — 막판에 집으면 「그 순간 + 3초」 까지, 한 사람당 1회, 일찍 집으면 늘지 않는다
        {
            var t = new ChainTable();
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            CountersUsable who = asked[0];
            int hitId = t.Hand(who.Actor).First(c => !who.CardInstanceIds.Contains(c.InstanceId)).InstanceId;
            t.At(opens - 0.1);
            t.Server.HoldReaction(who.Actor, who.CardInstanceIds[0]); // 연출 중 — 버린다 (1회를 쓰지도 않는다)
            t.At(opens + 4);
            t.Server.HoldReaction(who.Actor, hitId); // 낼 수 없는 카드 — 버린다
            t.At(opens + 4.5);
            Check(t.Server.HoldReaction(who.Actor, who.CardInstanceIds[0]).Count == 0, "고민 중 — 아무것도 알리지 않는다");
            t.At(opens + ReactionSeconds + 0.1);
            Check(!Resolved(t.Tick()), "4.5초에 집음 → 5초가 지나도 처리 없음");
            t.At(opens + 7.4);
            t.Server.HoldReaction(who.Actor, who.CardInstanceIds[0]); // 두 번째 — 버린다
            Check(!Resolved(t.Tick()), "7.4초 — 아직 (마감 7.5초)");
            t.At(opens + 7.5);
            Check(Resolved(t.Tick()), "집은 순간 + 3초 = 7.5초에 처리 · 두 번째 연장은 없다");

            t = new ChainTable();
            who = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().First();
            t.At(opens + 1);
            t.Server.HoldReaction(who.Actor, who.CardInstanceIds[0]);
            t.At(opens + ReactionSeconds);
            Check(Resolved(t.Tick()), "1초에 집음(3초 넘게 남음) → 늘지 않고 5초에 처리");
        }

        // 새 카운터가 오르면 스킵 · 연장이 새로 — 스킵했던 사람도 다시 물어 온다
        {
            var t = new ChainTable();
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            CountersUsable skipper = asked.First(a => a.Actor != t.O1);
            t.At(opens + 1);
            t.Server.SkipReaction(skipper.Actor);
            IReadOnlyList<GameEvent> counter = t.Counter(t.O1, ChainTable.Negate);
            Check(counter.OfType<CardPlayed>().Any(), "다른 사람이 카운터를 냄 — 반응 시간 다시");
            bool askedAgain = counter.OfType<CountersUsable>().Any(a => a.Actor == skipper.Actor);
            t.At(1 + opens + opens + 4.9);
            Check(!askedAgain || !Resolved(t.Tick()), "새 반응 시간 — 앞의 스킵은 이어지지 않는다" + (askedAgain ? "" : " (그 사람이 낼 것이 없어 확인 생략)"));
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 규칙 계층의 사망 · 빈 덱 피해 · 나감 (06-health.md 8 · 9절 · 05-deck.md 5절 · 01 6절, 기능 7). ChainTable 을 같이 쓴다.
    /// </summary>
    public static string Death()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        // 1. 카드 피해로 HP 0 → 피해 연출 뒤 5초 사망 대기(게임 멈춤) → 최종 사망 → 손패 버림 · 턴에서 빠짐
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + ReactionSeconds);
            DeathPending pending = t.Tick().OfType<DeathPending>().FirstOrDefault();
            double startsAt = ChainTable.Reveal + ReactionSeconds + ChainTable.Resolve;
            Check(pending != null && pending.Actor == t.O1 && Math.Abs(pending.StartsAt - startsAt) < 1e-6 && Math.Abs(pending.Deadline - (startsAt + 5)) < 1e-6,
                "HP 0 → 사망 대기 (피해 연출 뒤부터 5초)");
            t.At(startsAt + 1);
            Check(!t.Tick().OfType<TurnStarted>().Any(), "사망 대기 중엔 다음 턴 없음");
            int discardBefore = t.DiscardCount;
            t.At(startsAt + 5);
            List<GameEvent> events = t.Tick().ToList();
            PlayerDied died = events.OfType<PlayerDied>().FirstOrDefault();
            Check(died != null && died.Actor == t.O1 && t.HandCount(t.O1) == 0 && t.DiscardCount == discardBefore + 6, "마감 → 최종 사망 · 손패 6장 버림 더미로");
            Check(died != null && died.CardIds.Length == 6, "사망 일에 버려진 카드 6장의 종류가 실린다");
            Check(events.OfType<TurnStarted>().Any() && t.Server.CurrentTurnActor == t.O2, "다음 턴은 죽은 사람을 건너뛴다 (P" + t.Server.CurrentTurnActor + ")");
            t.At(100);
            Check(t.Code(t.Play(t.O2, ChainTable.Hit, t.O1)) == RejectCode.InvalidTarget, "죽은 사람은 대상이 될 수 없다 → 5");
            t.Play(t.O2, ChainTable.Hit, t.Turn);
            t.At(100 + ChainTable.Reveal + 0.5);
            Check(t.Code(t.Counter(t.O1, ChainTable.Negate)) == RejectCode.NotAlive, "죽은 사람의 카운터 → 10");
        }

        // 2. 두 사람이 함께 HP 0 — 같은 마감, 함께 최종 사망 (06 11절)
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 10);
            t.SetHp(t.O2, 10);
            t.Play(t.Turn, ChainTable.Wave, null);
            t.At(ChainTable.Reveal + ReactionSeconds);
            DeathPending[] pendings = t.Tick().OfType<DeathPending>().ToArray();
            Check(pendings.Length == 2 && pendings[0].Deadline == pendings[1].Deadline, "동시 HP 0 → 둘 다 사망 대기 · 같은 마감");
            t.At(100);
            Check(t.Tick().OfType<PlayerDied>().Count() == 2, "함께 최종 사망");
        }

        // 3. 턴 뽑기는 2장, 손패가 찹츄 장수 − 1 이면 1장 (05 4절)
        {
            var t = new ChainTable();
            int before = t.HandCount(t.Turn);
            IReadOnlyList<GameEvent> drawn = t.Server.Draw(t.Turn);
            Check(drawn.OfType<CardDrawn>().Count() == GameServer.TurnDrawCount && t.HandCount(t.Turn) == before + 2 && drawn.OfType<TurnStarted>().Any(), "턴 뽑기 → 2장 · 턴 종료");

            var nine = new ChainTable();
            while (nine.HandCount(nine.Turn) < GameServer.ChapchuHandCount - 1)
                nine.GiveCard(nine.Turn, ChainTable.Hit);
            Check(nine.Server.Draw(nine.Turn).OfType<CardDrawn>().Count() == 1 && nine.HandCount(nine.Turn) == GameServer.ChapchuHandCount, "손패 9장에서 턴 뽑기 → 1장 · 딱 10장");
        }

        // 3-1. 빈 덱 피해 1 · 2 · 3 … (사람마다, 못 뽑은 장마다), 드로우는 턴 종료
        {
            var t = new ChainTable();
            t.EmptyDeck();
            IReadOnlyList<GameEvent> first = t.Server.Draw(t.Turn);
            int[] amounts = first.OfType<EmptyDeckDamaged>().Select(e => e.Amount).ToArray();
            Check(amounts.SequenceEqual(new[] { 1, 2 }) && t.Hp(t.Turn) == 97 && first.OfType<TurnStarted>().Any(), "빈 덱 첫 드로우(2장) → 피해 1 + 2 · 턴 종료");
            t.Server.Draw(t.O1);
            t.Server.Draw(t.O2);
            t.Server.Draw(t.Turn);
            Check(t.Hp(t.Turn) == 90 && t.Hp(t.O1) == 97, "두 번째 → 피해 3 + 4 (사람마다 따로)");
        }

        // 4. 빈 덱 피해로 HP 0 → 바로 5초 사망 대기 → 사망 뒤 다음 턴
        {
            var t = new ChainTable();
            t.EmptyDeck();
            t.SetHp(t.Turn, 1);
            t.At(3);
            List<GameEvent> events = t.Server.Draw(t.Turn).ToList();
            DeathPending pending = events.OfType<DeathPending>().FirstOrDefault();
            Check(pending != null && Math.Abs(pending.StartsAt - 3) < 1e-6 && !events.OfType<TurnStarted>().Any(), "빈 덱 피해로 HP 0 → 바로 사망 대기 · 턴은 대기 뒤");
            t.At(8);
            events = t.Tick().ToList();
            Check(events.OfType<PlayerDied>().Any() && events.OfType<TurnStarted>().Any() && t.Server.CurrentTurnActor == t.O1, "사망 → 다음 사람 턴");
        }

        // 5. 나감 — 손패가 버림 더미로 (01 6절)
        {
            var t = new ChainTable();
            int before = t.DiscardCount;
            PlayerLeft left = t.Server.RemovePlayer(t.O2).OfType<PlayerLeft>().FirstOrDefault();
            Check(left != null && !left.Forced && left.DiscardCount == before + 6 && t.HandCount(t.O2) == 0, "나감 → 손패 6장 버림 더미로 (강제 퇴장 표시 없음)");
        }

        double limit = GameServer.TurnTimeLimit + 0.3;
        // 다른 두 사람이 직접 뽑아 턴을 돌린다 — 손패를 줄여 두어 뽑다가 10장(찹츄)이 되지 않게
        void PassOthers(ChainTable table)
        {
            foreach (int other in new[] { table.O1, table.O2 })
            {
                table.SetHandSize(other, 3);
                table.Server.Draw(other);
            }
        }

        // 6. 시간 초과 자동 뽑기 — 카드는 내지 않고 뽑기만, 자동 뽑기로는 10장이 되지 않게 (03-turn 4절, PM 2026-11-19)
        foreach ((int hand, int drawn) in new[] { (0, 2), (6, 2), (7, 2), (8, 1), (9, 0), (10, 2), (11, 2) })
        {
            var t = new ChainTable(null, true);
            t.SetHandSize(t.Turn, hand);
            List<GameEvent> events = t.Run(limit);
            Check(events.OfType<CardDrawn>().Count(d => d.Actor == t.Turn) == drawn && !events.OfType<CardPlayed>().Any()
                && t.HandCount(t.Turn) == hand + drawn && t.Server.CurrentTurnActor == t.O1 && t.State(t.Turn).TimeoutStreak == 1
                && !events.OfType<ChapchuChanged>().Any(c => c.IsChapchu),
                $"시간 초과 · 손패 {hand}장 → {drawn}장 뽑고 턴 넘어감 · 카드 사용 없음 · 선언 없음 · 연속 1 (실제 +{t.HandCount(t.Turn) - hand})");
        }

        // 7. 9장 · 빈 덱 시간 초과 → 뽑지 않으니 빈 덱 피해도 없다. 8장이면 1장을 못 뽑아 피해 1
        {
            var t = new ChainTable(null, true);
            t.SetHandSize(t.Turn, 9);
            t.EmptyDeck();
            List<GameEvent> events = t.Run(limit);
            Check(!events.OfType<EmptyDeckDamaged>().Any() && t.Hp(t.Turn) == 100 && t.Server.CurrentTurnActor == t.O1, "시간 초과 · 9장 · 빈 덱 → 피해 없이 턴만 넘어감");

            t = new ChainTable(null, true);
            t.SetHandSize(t.Turn, 8);
            t.EmptyDeck();
            events = t.Run(limit);
            Check(events.OfType<EmptyDeckDamaged>().Count() == 1 && t.Hp(t.Turn) == 99, "시간 초과 · 8장 · 빈 덱 → 못 뽑은 1장만큼 빈 덱 피해 1");
        }

        // 8. 3턴 연속 시간 초과 → 3번째에는 뽑지 않고 바로 강제 퇴장 (나가기와 같은 처리 + 강제 퇴장 표시)
        {
            var t = new ChainTable(null, true);
            int a = t.Turn;
            for (int round = 1; round <= GameServer.TimeoutLimit; round++)
            {
                int before = t.HandCount(a);
                List<GameEvent> events = t.Run(t.Now + limit);
                if (round < GameServer.TimeoutLimit)
                {
                    Check(t.State(a).TimeoutStreak == round && t.HandCount(a) > before && !t.State(a).HasLeft, $"시간 초과 {round}번째 → 자동 뽑기 · 연속 {round}");
                    PassOthers(t);
                    Check(t.Server.CurrentTurnActor == a, "다른 두 사람이 직접 뽑아 다시 그 사람 턴");
                }
                else
                {
                    PlayerLeft left = events.OfType<PlayerLeft>().FirstOrDefault();
                    Check(left != null && left.Actor == a && left.Forced && !events.OfType<CardDrawn>().Any(d => d.Actor == a) && t.State(a).HasLeft
                        && t.HandCount(a) == 0 && t.Server.CurrentTurnActor == t.O1 && !t.Server.IsGameOver,
                        $"시간 초과 {round}번째 → 뽑지 않고 바로 강제 퇴장 · 손패 버림 · 다음 사람 턴");
                    GameEnded ended = t.Server.RemovePlayer(t.O2).OfType<GameEnded>().FirstOrDefault();
                    Check(ended != null && ended.Winner == t.O1, "강제 퇴장 뒤 한 명이 더 나가면 남은 사람 승리");
                }
            }
        }

        // 9. 직접 뽑기 · 카드 사용으로 턴을 끝내면 연속 횟수는 0 으로
        {
            var t = new ChainTable(null, true);
            int a = t.Turn;
            for (int i = 0; i < 2; i++)
            {
                t.Run(t.Now + limit);
                PassOthers(t);
            }
            Check(t.State(a).TimeoutStreak == 2 && t.Server.CurrentTurnActor == a, "두 번 시간 초과 — 연속 2");
            t.SetHandSize(a, 3);
            t.Server.Draw(a);
            Check(t.State(a).TimeoutStreak == 0, "직접 뽑기 → 연속 0");
            PassOthers(t);
            t.Run(t.Now + limit);
            Check(t.State(a).TimeoutStreak == 1 && !t.State(a).HasLeft, "그 뒤 시간 초과 → 연속 1 (퇴장 아님)");
            PassOthers(t);
            t.Play(a, ChainTable.Hit, t.O1);
            Check(t.State(a).TimeoutStreak == 0, "카드 사용 → 연속 0");
        }

        // 10. 봇(시간 초과 대상이 아닌 사람)은 마감에 그냥 넘어간다 — 뽑지 않고, 세지 않고, 퇴장도 없다 (18-bot 11절)
        {
            var t = new ChainTable();
            int a = t.Turn;
            bool drew = false;
            for (int round = 0; round < GameServer.TimeoutLimit + 1; round++)
            {
                drew |= t.Run(t.Now + limit).OfType<CardDrawn>().Any(d => d.Actor == a);
                PassOthers(t);
            }
            Check(!drew && t.HandCount(a) == 6 && t.State(a).TimeoutStreak == 0 && !t.State(a).HasLeft, "대상이 아닌 사람 — 시간 초과 4번에도 뽑기 · 횟수 · 퇴장 없음");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    /// <summary>규칙 계층의 찹츄 · 처치 승리 · 무승부 · 게임 종료 (07-win-condition.md, 기능 8). ChainTable 을 같이 쓴다.</summary>
    public static string Win()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        Func<int, Card> Act(TargetType target, params CardEffect[] e) => id => new Card(id, 900, CardType.Action, target, e, null, ChainTable.Reveal, ChainTable.Resolve);
        bool Declared(IEnumerable<GameEvent> events, int actor) => events.OfType<ChapchuChanged>().Any(e => e.Actor == actor && e.IsChapchu);
        bool Cleared(IEnumerable<GameEvent> events, int actor) => events.OfType<ChapchuChanged>().Any(e => e.Actor == actor && !e.IsChapchu);

        // 1. 자동 선언 (07 4.1) — 내 턴에 뽑아 정확히 10장 → 선언 · 대기 없이 턴 종료 → 다음 자기 턴 시작에 승리
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 8);
            IReadOnlyList<GameEvent> drawn = t.Server.Draw(t.Turn);
            Check(t.HandCount(t.Turn) == 10 && Declared(drawn, t.Turn) && drawn.OfType<TurnStarted>().Any(e => e.Actor == t.O1),
                "8장에서 2장 뽑아 10장 → 자동 선언 · 선언 대기 없이 바로 턴 종료");
            Check(!t.Server.Draw(t.O1).OfType<ChapchuChanged>().Any() && !t.State(t.O1).IsChapchu, "6장에서 뽑아 8장 → 선언 없음");
            List<GameEvent> events = t.Server.Draw(t.O2).ToList();
            GameEnded ended = events.OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.Turn && ended.Reason == GameEndReason.Chapchu && !events.OfType<TurnStarted>().Any(), "자기 턴이 오는 순간 10장 → 찹츄 승리 (턴 시작 없음)");
            Check(t.Code(t.Server.Draw(t.Turn)) == RejectCode.GameOver && t.Tick().Count == 0, "끝난 뒤 요청 → 13 · Tick 은 아무 일 없음");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 9);
            IReadOnlyList<GameEvent> drawn = t.Server.Draw(t.Turn);
            Check(drawn.OfType<CardDrawn>().Count() == 1 && t.HandCount(t.Turn) == 10 && Declared(drawn, t.Turn) && drawn.OfType<TurnStarted>().Any(e => e.Actor == t.O1),
                "9장에서 1장 뽑아 10장 → 자동 선언 · 바로 턴 종료");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 11);
            Check(Declared(t.Play(t.Turn, ChainTable.Hit, t.O1), t.Turn) && t.HandCount(t.Turn) == 10, "11장에서 카드를 내 10장 → 자동 선언");
        }

        // 1-2. 남의 턴에도 선언 · 효과 하나가 끝난 뒤의 장수만 본다 (지나친 10장은 선언 아님, PM 2026-11-19)
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 9);
            List<GameEvent> events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DrawEffect { subject = EffectSubject.Target, count = 1 })), t.O1);
            Check(t.HandCount(t.O1) == 10 && Declared(events, t.O1) && t.State(t.O1).IsChapchu, "남의 턴에 카드 효과로 10장 → 자동 선언");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 9);
            List<GameEvent> events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DrawEffect { subject = EffectSubject.Target, count = 2 })), t.O1);
            Check(t.HandCount(t.O1) == 11 && !events.OfType<ChapchuChanged>().Any() && !t.State(t.O1).IsChapchu, "9장에서 2장 뽑는 효과 → 11장 — 지나친 10장은 선언이 아니다");
        }

        // 2. 선언 뒤 장수가 바뀌어도 유지 → 자기 턴 시작에 10장이 아니면 무효 → 다시 10장이면 재선언 (07 4.2)
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 8);
            t.Server.Draw(t.Turn); // 10장 선언 → O1 턴
            List<GameEvent> events = t.PlayAndResolve(t.O1, t.Give(t.O1, Act(TargetType.OneOther, new DiscardEffect { subject = EffectSubject.Target, count = 1 })), t.Turn);
            Check(t.HandCount(t.Turn) == 9 && t.State(t.Turn).IsChapchu && !Cleared(events, t.Turn), "선언 뒤 9장이 돼도 찹츄 유지");
            t.RunUntilTurn(t.O2, t.Now + 20);
            events = t.Server.Draw(t.O2).ToList();
            Check(Cleared(events, t.Turn) && !t.Server.IsGameOver && events.OfType<TurnStarted>().Any(e => e.Actor == t.Turn) && !t.State(t.Turn).IsChapchu,
                "자기 턴 시작에 9장 → 선언 무효 · 턴은 보통대로 시작");
            events = t.Server.Draw(t.Turn).ToList();
            Check(t.HandCount(t.Turn) == 10 && Declared(events, t.Turn), "그 턴에 뽑아 다시 10장 → 재선언");
            t.Server.Draw(t.O1);
            GameEnded ended = t.Server.Draw(t.O2).OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.Turn && ended.Reason == GameEndReason.Chapchu, "재선언한 뒤 다음 자기 턴에 승리");
        }

        // 2-2. 사망 대기 중에는 선언하지 않는다 → 살아난 뒤 10장이면 선언 (PM 2026-11-19)
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 10);
            t.SetHandSize(t.O1, 9);
            List<GameEvent> events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DamageEffect { subject = EffectSubject.Target, amount = 10 }, new DrawEffect { subject = EffectSubject.Target, count = 1 })), t.O1);
            bool pending = events.OfType<DeathPending>().Any(e => e.Actor == t.O1);
            Check(pending && t.HandCount(t.O1) == 10 && !t.State(t.O1).IsChapchu, "HP 0 · 사망 대기 중 10장 → 선언하지 않는다");
            t.SetHp(t.O1, 30); // 대기 중 회복
            events = t.Run(t.Now + 8);
            Check(events.OfType<DeathAvoided>().Any(e => e.Actor == t.O1) && Declared(events, t.O1), "살아난 뒤 10장 → 선언");
        }

        // 2-3. 부여된 찹츄 (A20 · T16) — 장수 조건 없이 붙고, 그 사람의 턴 시작에 판정 (07 7 · 8절)
        {
            var t = new ChainTable();
            List<GameEvent> events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SetChapChuEffect { subject = EffectSubject.Target })), t.O1);
            Check(t.HandCount(t.O1) == 6 && Declared(events, t.O1), "6장인 사람에게 찹츄 부여 → 붙는다");
            events = t.Run(t.Now + 20);
            Check(Cleared(events, t.O1) && events.OfType<TurnStarted>().Any(e => e.Actor == t.O1) && !t.Server.IsGameOver, "부여된 찹츄도 턴 시작에 10장이 아니면 무효");
        }

        // 3. 처치 승리 — 마지막 1인, 사망 대기가 끝난 뒤
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 10);
            t.SetHp(t.O2, 10);
            t.Play(t.Turn, ChainTable.Wave, null);
            t.At(ChainTable.Reveal + ReactionSeconds);
            Check(!t.Tick().OfType<GameEnded>().Any(), "사망 대기 중엔 판정 안 함");
            t.At(100);
            List<GameEvent> events = t.Tick().ToList();
            GameEnded ended = events.OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.Turn && ended.Reason == GameEndReason.Elimination && !events.OfType<TurnStarted>().Any(), "남은 1인 처치 승리");
            int[] fallen = new[] { t.O1, t.O2 }.OrderBy(a => a).ToArray();
            Check(ended != null && ended.Actors.SequenceEqual(new[] { t.Turn }.Concat(fallen)) && ended.Ranks.SequenceEqual(new[] { 1, 2, 2 })
                  && ended.DownedBy.SequenceEqual(new[] { -1, t.Turn, t.Turn }) && ended.OutTurns.SequenceEqual(new[] { 0, 1, 1 })
                  && ended.Hps.SequenceEqual(new[] { 100, 0, 0 }) && !ended.Left.Any(left => left),
                "요약 — 같은 턴에 함께 탈락 → 공동 2위 · 당한 사람 = 카드 주인 · 탈락 턴 1 (" + Summary(ended) + ")");
        }

        // 4. 무승부 — 전원 최종 사망 (나 포함 전원 피해)
        {
            var t = new ChainTable();
            foreach (int actor in new[] { t.Turn, t.O1, t.O2 })
                t.SetHp(actor, 10);
            t.Play(t.Turn, ChainTable.Storm, null);
            t.At(100);
            t.Tick();
            t.At(200);
            GameEnded ended = t.Tick().OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == -1 && ended.Reason == GameEndReason.Draw, "전원 사망 → 무승부");
            Check(ended != null && ended.Actors.SequenceEqual(new[] { 1, 2, 3 }) && ended.Ranks.All(rank => rank == 1)
                  && ended.Actors.Select((actor, i) => ended.DownedBy[i] == (actor == t.Turn ? -1 : t.Turn)).All(ok => ok),
                "요약 — 무승부는 전원 1위 · 자기 피해는 「당함」 이 아니다 (" + Summary(ended) + ")");
        }

        // 5. 나가서 1명 — 체인이 없으면 바로, 체인 중이면 처리 뒤
        {
            var t = new ChainTable();
            t.Server.RemovePlayer(t.O1);
            GameEnded ended = t.Server.RemovePlayer(t.O2).OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.Turn, "다른 사람이 모두 나감 → 바로 처치 승리");
            Check(ended != null && ended.Ranks.SequenceEqual(new[] { 1, 2, 2 }) && ended.Left.SequenceEqual(new[] { false, true, true })
                  && ended.OutTurns.SequenceEqual(new[] { 0, 1, 1 }) && ended.DownedBy.All(by => by == -1),
                "요약 — 나간 사람은 나간 턴에 탈락 · 「나감」 표시 · 당한 사람 없음 (" + Summary(ended) + ")");
        }

        // 6. 요약의 순위 — 늦게 빠진 사람이 위. 죽은 뒤에 나가도 탈락 턴은 그대로 (07 11-3)
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + ReactionSeconds);
            t.Tick();
            t.At(100);
            t.Tick(); // O1 최종 사망 (1턴) → O2 의 턴 (2턴)
            bool second = t.Server.CurrentTurnActor == t.O2;
            t.Server.RemovePlayer(t.O1); // 죽은 사람이 나중에 나감
            GameEnded ended = t.Server.RemovePlayer(t.O2).OfType<GameEnded>().FirstOrDefault();
            Check(second && ended != null && ended.Actors.SequenceEqual(new[] { t.Turn, t.O2, t.O1 }) && ended.Ranks.SequenceEqual(new[] { 1, 2, 3 })
                  && ended.OutTurns.SequenceEqual(new[] { 0, 2, 1 }) && ended.DownedBy.SequenceEqual(new[] { -1, -1, t.Turn }) && ended.Left.SequenceEqual(new[] { false, true, false }), // 죽은 뒤 나간 사람은 사망으로 빠진 것 — 「이탈」 이 아니다 (07 11-2)
                "요약 — 2턴에 나간 사람 2위 · 1턴에 죽은 사람 3위 (죽은 뒤 나가도 그대로 · 이탈 아님) · 당한 사람 (" + Summary(ended) + ")");
        }

        // 7. 요약의 순위 — 찹츄 승리: 남은 사람은 HP 높은 순, 같으면 공동 (07 11-3)
        {
            var t = new ChainTable();
            t.State(t.O1).IsChapchu = true;
            t.SetHandSize(t.O1, 10);
            t.SetHp(t.O1, 5);
            t.SetHp(t.O2, 90);
            t.Play(t.Turn, ChainTable.Hit, t.O2);
            t.At(ChainTable.Reveal + ReactionSeconds);
            t.Tick();
            t.At(100);
            GameEnded ended = t.Tick().OfType<GameEnded>().FirstOrDefault();
            int o2Hp = t.Hp(t.O2);
            Check(ended != null && ended.Reason == GameEndReason.Chapchu && ended.Actors.SequenceEqual(new[] { t.O1, t.Turn, t.O2 }) && ended.Ranks.SequenceEqual(new[] { 1, 2, 3 })
                  && ended.Hps.SequenceEqual(new[] { 5, 100, o2Hp }) && o2Hp > 5 && o2Hp < 90 && ended.OutTurns.All(turn => turn == 0) && ended.DownedBy.All(by => by == -1),
                "요약 — 찹츄 승자가 HP 가 가장 낮아도 1위 · 남은 사람은 HP 순 (" + Summary(ended) + ")");
        }
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.Server.RemovePlayer(t.O1);
            Check(!t.Server.RemovePlayer(t.O2).OfType<GameEnded>().Any(), "체인 중에 모두 나감 → 아직");
            t.At(ChainTable.Reveal + ReactionSeconds);
            Check(t.Tick().OfType<GameEnded>().Any(), "체인 처리 뒤 처치 승리");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    /// <summary>규칙 계층의 함정 설치 · 자동 발동 (04-card.md 6절 · 9.2, 기능 6). ChainTable 을 같이 쓴다.</summary>
    public static string Trap()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        // 1. 설치 — 내 턴 · 메인 행동 전 · 3칸, 메인 행동이 아니다
        {
            var t = new ChainTable();
            for (int i = 0; i < 4; i++)
                t.GiveCard(t.Turn, ChainTable.TrapHit);
            TrapSet set = t.SetTrap(t.Turn, ChainTable.TrapHit).OfType<TrapSet>().FirstOrDefault();
            Check(set != null && set.TrapCount == 1 && set.HandCount == t.HandCount(t.Turn), "설치 → 함정 1장 · 손패에서 빠짐");
            t.SetTrap(t.Turn, ChainTable.TrapHit);
            t.SetTrap(t.Turn, ChainTable.TrapHit);
            Check(t.Code(t.SetTrap(t.Turn, ChainTable.TrapHit)) == RejectCode.TrapSlotFull, "4장째 → 15");
            Check(t.Code(t.SetTrap(t.Turn, ChainTable.Hit)) == RejectCode.NotTrapCard, "함정 아닌 카드 → 14");
            t.GiveCard(t.O1, ChainTable.TrapHit);
            Check(t.Code(t.SetTrap(t.O1, ChainTable.TrapHit)) == RejectCode.NotYourTurn, "남의 턴 설치 → 1");
            Check(!(t.Play(t.Turn, ChainTable.Hit, t.O1)[0] is RequestRejected), "설치 뒤에도 메인 행동 가능");
            Check(t.Code(t.SetTrap(t.Turn, ChainTable.TrapHit)) == RejectCode.MainActionUsed, "메인 행동 뒤 설치 → 7");
        }

        // 2. 피해 받은 후 — 체인이 끝나면 자동 발동 (반응 시간 · 함정 시간 없음), 대상 = 피해를 준 사람
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.GiveTrap(t.O2, ChainTable.TrapHit);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + ReactionSeconds);
            List<GameEvent> events = t.Tick().ToList();
            List<TrapActivated> traps = events.OfType<TrapActivated>().ToList();
            Check(traps.Count == 1 && traps[0].Actor == t.O1 && traps[0].Targets.SequenceEqual(new[] { t.Turn }) && traps[0].TrapCount == 0, "체인 끝 → 맞은 O1 의 함정만 자동 발동 · 대상 = 피해를 준 사람");
            Check(t.Hp(t.Turn) == 90 && t.TrapCount(t.O2) == 1, "반격 피해 (♥90) · 안 맞은 O2 의 함정은 남음");
            int resolvedAt = events.FindIndex(e => e is CardResolved), discardAt = events.FindIndex(e => e is ChainDiscarded), trapAt = events.FindIndex(e => e is TrapActivated);
            Check(resolvedAt >= 0 && discardAt > resolvedAt && trapAt > discardAt && !events.OfType<ReactionStarted>().Any(), $"행동 카드 처리 → 버림 더미 → 함정, 반응 시간 없음 ({resolvedAt} · {discardAt} · {trapAt})");
            Check(t.Discard[t.Discard.Count - 1].CardId == ChainTable.TrapHit, "발동한 함정도 버림 더미 맨 위로");
            double next = ChainTable.Reveal + ReactionSeconds + ChainTable.Resolve + ChainTable.Reveal + ChainTable.Resolve;
            t.At(next - 0.1);
            Check(!t.Tick().OfType<TurnStarted>().Any(), "함정 연출 중엔 다음 턴 없음");
            t.At(next + 0.1);
            t.Tick();
            Check(t.Server.CurrentTurnActor == t.O1, "함정 연출이 끝나면 다음 턴");
        }

        // 3. 함정의 효과는 다른 함정을 부르지 않는다 · 함정에는 카운터 못 함 · 지난 사건으로는 발동하지 않음
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.GiveTrap(t.Turn, ChainTable.TrapHit);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + ReactionSeconds);
            List<GameEvent> events = t.Tick().ToList();
            Check(events.OfType<TrapActivated>().Count() == 1 && t.TrapCount(t.Turn) == 1 && t.Hp(t.Turn) == 90 && t.Hp(t.O1) == 80, "함정에 맞은 사람의 함정은 발동하지 않는다");
            int code = t.Code(t.Counter(t.Turn, ChainTable.Negate));
            Check(code != 0, "함정에 카운터 → 거절 " + code);
            t.RunUntilTurn(t.O1, 60);
            t.Play(t.O1, ChainTable.Hit, t.O2);
            events = t.Run(t.Now + ChainTable.Reveal + ReactionSeconds + 1);
            Check(events.OfType<CardResolved>().Any() && !events.OfType<TrapActivated>().Any() && t.TrapCount(t.Turn) == 1, "지난 피해로는 다음 체인에서도 발동하지 않음");
        }

        // 4. 여러 함정 — 방 전체에서 설치한 순서대로
        {
            var t = new ChainTable();
            t.GiveTrap(t.O2, ChainTable.TrapHit);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.Play(t.Turn, ChainTable.Wave, null);
            t.At(ChainTable.Reveal + ReactionSeconds);
            List<TrapActivated> traps = t.Tick().OfType<TrapActivated>().ToList();
            Check(traps.Select(e => e.Actor).SequenceEqual(new[] { t.O2, t.O1 }) && t.Hp(t.Turn) == 80, "먼저 설치한 O2 → O1 순으로 발동 (♥80)");
        }

        // 5. HP 0 — 「본인 사망 대기」 함정만, 다른 함정은 남고 사망 대기 → 최종 사망
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.GiveTrap(t.O1, ChainTable.TrapDying);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + ReactionSeconds);
            List<GameEvent> events = t.Tick().ToList();
            List<TrapActivated> traps = events.OfType<TrapActivated>().ToList();
            Check(traps.Count == 1 && traps[0].CardId == ChainTable.TrapDying && t.TrapCount(t.O1) == 1 && t.Hp(t.Turn) == 100, "HP 0 → 본인 사망 대기 함정만 발동 · 피해 받은 후 함정은 남음");
            Check(events.OfType<DeathPending>().Any(e => e.Actor == t.O1), "함정 뒤 사망 대기");
            Check(t.Run(60).OfType<PlayerDied>().Any(e => e.Actor == t.O1), "사망 대기가 끝나면 최종 사망");
        }

        // 7. 나감 → 설치한 함정도 버림 더미로
        {
            var t = new ChainTable();
            t.GiveTrap(t.O2, ChainTable.TrapHit);
            t.GiveTrap(t.O2, ChainTable.TrapReact);
            int before = t.DiscardCount;
            PlayerLeft left = t.Server.RemovePlayer(t.O2).OfType<PlayerLeft>().FirstOrDefault();
            Check(left != null && left.DiscardCount == before + 6 + 2 && left.CardIds.Length == 8, "나감 → 손패 6 + 함정 2 버림 더미로 (카드 종류 8장이 실린다)");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 규칙 계층의 봇 (18-bot.md 7절). ① 봇끼리 기본 덱으로 여러 판을 끝까지 ② 찹츄 선언은 내 턴에만 ③ 나를 노린 카드에 카운터 ④ 맞은 뒤 함정 (자동) ⑤ 난이도 (거절 3회 상한은 봇이 미리 걸러 거절을 일부러 만들 수 없어 시험하지 않는다)
    /// 시계는 0.1초씩 직접 움직인다.
    /// </summary>
    public static string Bot()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        // ① 봇 4 — 기본 덱(카운터 · 함정 포함)으로 5판. 끝까지 가고, 매 판 이유 로그가 남는다
        DeckData deck = FindDeck();
        int finished = 0, lines = 0, counters = 0, traps = 0, longest = 0;
        string sample = "";
        for (int game = 0; game < 5; game++)
        {
            double now = 0;
            var server = new GameServer(new System.Random(game), () => now);
            int[] actors = { 1001, 1002, 1003, 1004 };
            server.StartGame(actors, deck.CreateCards(), actors);
            var states = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(server);
            foreach (PlayerState state in states.Values)
                state.Hp = 40;

            var driver = new BotDriver(server, actors, new System.Random(game + 100), () => now);
            int turns = 0;
            while (!server.IsGameOver && now < 3000)
            {
                now += 0.1;
                IReadOnlyList<GameEvent> ticked = server.Tick();
                driver.Observe(ticked);
                IReadOnlyList<GameEvent> acted = driver.Tick();
                driver.Observe(acted);
                turns += ticked.OfType<TurnStarted>().Count() + acted.OfType<TurnStarted>().Count();
                foreach (string log in driver.TakeLogs())
                {
                    lines++;
                    if (log.Contains("카운터 →")) counters++;
                    if (log.Contains("함정") && log.Contains("#") && !log.Contains("설치")) traps++;
                    if (sample.Length < 400) sample += "      " + log + "\n";
                }
            }

            if (server.IsGameOver)
                finished++;
            longest = Math.Max(longest, turns);
        }
        Check(finished == 5, $"봇 4 · 5판 모두 끝까지 ({finished}/5, 가장 긴 판 {longest}턴)");
        Check(lines > 0, $"행동 이유 로그 {lines}줄 · 카운터 {counters} · 함정 발동 {traps}");
        sb.Append(sample);

        // ② 찹츄 (7-5) — 선언은 자동이라 봇이 요청하지 않는다. 뽑아서 10장이면 그 순간 선언 · 턴 종료, 찹츄 · 10장으로 턴을 맞으면(「기다려」) 지킨다
        {
            double now = 0;
            var t = new ChainTable(() => now);
            var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
            t.SetHandSize(t.Turn, 9);
            var events = new List<GameEvent>();
            double drewAt = -1, declaredAt = -1;
            for (now = 0; now < 12 && declaredAt < 0; now += 0.1)
            {
                List<GameEvent> tick = t.Tick().Concat(driver.Tick()).ToList();
                events.AddRange(tick);
                if (drewAt < 0 && tick.OfType<CardDrawn>().Any()) drewAt = now;
                if (tick.OfType<ChapchuChanged>().Any(e => e.Actor == t.Turn && e.IsChapchu)) declaredAt = now;
            }
            Check(drewAt >= 0 && declaredAt == drewAt && !events.OfType<RequestRejected>().Any(), "내 턴 9장 → 뽑아 10장 → 그 순간 자동 선언 (봇의 선언 요청 없음)");
            Check(events.OfType<TurnStarted>().Any(e => e.Actor == t.O1), "선언 대기 없이 턴 종료");
        }
        {
            double now = 0;
            var t = new ChainTable(() => now);
            var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
            t.SetHandSize(t.Turn, 10);
            t.State(t.Turn).IsChapchu = true; // 「기다려」 로 승리가 미뤄진 턴
            var events = new List<GameEvent>();
            for (now = 0; now < BotTurnSeconds; now += 0.1)
                events.AddRange(t.Tick().Concat(driver.Tick()));
            Check(!events.OfType<CardDrawn>().Any() && !events.OfType<CardPlayed>().Any() && t.HandCount(t.Turn) == 10 && t.State(t.Turn).IsChapchu,
                "찹츄 · 10장으로 내 턴 → 지킨다 (뽑지도 내지도 않는다)");
        }

        // ③ 카운터 — 나를 노린 카드에 1 ~ 3초 뒤. 남끼리의 카드에는 내지 않는다
        {
            double now = 0;
            var t = new ChainTable(() => now);
            var driver = new BotDriver(t.Server, new[] { t.O1, t.O2 }, new System.Random(1), () => now);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            var events = new List<GameEvent>();
            for (now = 0; now < ChainTable.Reveal + ReactionSeconds - 0.1; now += 0.1)
                events.AddRange(driver.Tick());
            CardPlayed counter = events.OfType<CardPlayed>().FirstOrDefault();
            Check(counter != null && counter.Actor == t.O1, "나(O1)를 노린 카드 → O1 이 카운터");
            Check(counter != null && events.OfType<CardPlayed>().Count() == 1, "남(O2)은 끼지 않는다 · 한 창에 한 번");
        }

        // ④ 함정 — 맞은 뒤 자동 발동 (봇은 고르지 않는다)
        {
            double now = 0;
            var t = new ChainTable(() => now);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            ((PlayerState)t.States[t.O1]).Hand.RemoveAll(c => c.Type == CardType.Counter); // 카운터 없이 맞게
            var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => now);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            var events = new List<GameEvent>();
            int turnHp = -1;
            for (now = 0; now < 30 && turnHp < 0; now += 0.1) // 함정이 나오면 멈춘다 — 그 뒤 O1 의 턴에 봇이 또 때릴 수 있다
            {
                events.AddRange(t.Tick());
                if (events.OfType<TrapActivated>().Any())
                    turnHp = t.Hp(t.Turn);
                events.AddRange(driver.Tick());
            }
            TrapActivated trap = events.OfType<TrapActivated>().FirstOrDefault();
            Check(trap != null && trap.Actor == t.O1 && trap.Targets.SequenceEqual(new[] { t.Turn }) && turnHp == 90, $"맞은 뒤 함정 자동 반격 (♥{turnHp}, 봇은 고르지 않는다)");
        }

        // ── 카드 묶음 1 (11-card-list 1-2) — Deck_Main 의 실제 에셋 값(대상 · 대상 후보 조건 · 효과). 시간만 시험용 ──
        {
            DeckData main = AssetDatabase.LoadAssetAtPath<DeckData>(MainDeckPath);
            List<Card> mainCards = main != null ? main.CreateCards() : new List<Card>();
            int[] ids = mainCards.Select(c => c.CardId).ToArray();
            var expected = new Dictionary<int, int> { [1001] = 3, [1002] = 3, [1006] = 2, [1014] = 3, [1017] = 1, [1004] = 2, [1009] = 3, [1011] = 2, [1013] = 2, [1016] = 3, [1018] = 3, [1045] = 2, [1007] = 1, [1020] = 3, [1023] = 1, [1015] = 3, [1012] = 1, [1005] = 3, [1021] = 1, [1019] = 3, [2003] = 2, [2014] = 2, [2016] = 2, [2001] = 3, [2020] = 2, [2021] = 2, [2019] = 1, [2005] = 1, [2015] = 1, [2018] = 1, [2002] = 1, [2013] = 2 };
            Check(ids.Length == 65 && expected.All(pair => ids.Count(i => i == pair.Key) == pair.Value), $"Deck_Main 65장 (행동 45 + 카운터 20, 11-card-list 1-1 장수) — {ids.Length}장");

            Func<int, Card> Real(int cardId)
            {
                Card c = mainCards.First(x => x.CardId == cardId);
                return id => new Card(id, c.CardId, c.Type, c.Target, c.Effects, c.ReactsTo, ChainTable.Reveal, ChainTable.Resolve, c.Trigger, c.TriggerValue, c.Condition, c.TargetCondition, c.Uncounterable);
            }

            if (ids.Length == 65)
            {
                // ── 카운터 묶음 5 (11-card-list 1-3) — 2005 묻고 따블로 가 ──
                // 차례인 사람의 행동 카드 ← O2 의 2003 진짜 할거야? ← O1 의 따블 → 2003 무효 · 행동 카드 2배. 20초까지 돌린 사건을 돌려준다
                List<GameEvent> Doubled(ChainTable t, Card action, Card block, Card dbl, params int[] targets)
                {
                    t.PlayCard(t.Turn, action, targets);
                    t.At(2);
                    t.Counter(t.O2, block);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    var events = t.Counter(t.O1, dbl).ToList();
                    events.AddRange(t.Run(20));
                    return events;
                }
                {
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005)), early = t.Give(t.O2, Real(2005)), hit = t.Give(t.Turn, Real(1001));
                    t.PlayCard(t.Turn, hit, t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O2, early)) == RejectCode.CannotReact, "2005 행동 카드에는 거절 9 (카운터에만)");
                    t.Counter(t.O2, block);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    Check(t.Code(t.Counter(t.O2, early)) == RejectCode.CannotReact, "2005 내 카운터에는 거절 9");
                    t.Counter(t.O1, dbl);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 60 && !t.Hand(t.Turn).Contains(hit) && !t.Hand(t.O2).Contains(block), $"2005 묻고 따블로 가 — 2003 무효(반환 없음) · 1001 피해 20 → 40 (♥{t.Hp(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 30);
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    Doubled(t, t.Give(t.Turn, Real(1006)), block, dbl, t.O1);
                    Check(t.Hp(t.O1) == 70, $"2005 회복 20 → 40 (♥{t.Hp(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 0);
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    Card roll = t.Give(t.Turn, id => new Card(id, 1016, CardType.Action, TargetType.Self, new CardEffect[] { new DrawEffect { subject = EffectSubject.Self, count = 2, randomMax = 3 } }, null, ChainTable.Reveal, ChainTable.Resolve));
                    int drawn = Doubled(t, roll, block, dbl).OfType<CardDrawn>().Count(e => e.Actor == t.Turn);
                    Check(drawn == 4 || drawn == 6, $"2005 굴린 장수(2 ~ 3)도 2배 — {drawn}장");
                }
                {
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    int o1Hand = t.HandCount(t.O1) - 1, turnHand = t.HandCount(t.Turn);
                    Doubled(t, t.Give(t.Turn, Real(1002)), block, dbl, t.O1);
                    Check(t.HandCount(t.O1) == o1Hand - 2 && t.HandCount(t.Turn) == turnHand + 2, $"2005 빼앗기 1 → 2장 (대상 {t.HandCount(t.O1)} · 쓴 사람 {t.HandCount(t.Turn)})");
                }
                {
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    Doubled(t, t.Give(t.Turn, Real(1013)), block, dbl);
                    Check(t.HandCount(t.O1) == 10 && t.HandCount(t.O2) == 10, $"2005 1013 기억 삭제 — 다시 뽑기 5 → 10장 (사용자 2026-11-23) ({t.HandCount(t.O1)} · {t.HandCount(t.O2)})");
                }
                {
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    Doubled(t, t.Give(t.Turn, Real(1005)), block, dbl, t.O1);
                    Check(t.Hp(t.O1) == 2 && t.Hp(t.Turn) == 2, $"2005 1005 드롭킥 — 피해 98 · 못 쓰러뜨려 반동도 98 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.Turn)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 8); // + 따블 → 낼 때 9장으로 최다, 따블을 낸 뒤 8장
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    int turnHand = t.HandCount(t.Turn), o2Hand = t.HandCount(t.O2) - 1;
                    Doubled(t, t.Give(t.Turn, Real(1018)), block, dbl);
                    Check(t.HandCount(t.O1) == 4 && t.HandCount(t.Turn) == turnHand + 2 && t.HandCount(t.O2) == o2Hand + 2, $"2005 1018 무야호 — 1장씩 → 2장씩 (준 사람 {t.HandCount(t.O1)} · {t.HandCount(t.Turn)} · {t.HandCount(t.O2)})");
                }
                {
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    t.PutInDiscard(ChainTable.Hit); t.PutInDiscard(ChainTable.Hit); t.PutInDiscard(ChainTable.Hit);
                    int taken = Doubled(t, t.Give(t.Turn, Real(1009)), block, dbl).OfType<DiscardTaken>().Count(e => e.Actor == t.Turn);
                    Check(taken == 2, $"2005 1009 아 맞다! — 맨 위부터 2장 ({taken}장)");
                }
                {
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    t.GiveTrap(t.O1, ChainTable.TrapDying); t.GiveTrap(t.O1, ChainTable.TrapDying); t.GiveTrap(t.O1, ChainTable.TrapDying);
                    Doubled(t, t.Give(t.Turn, Real(1004)), block, dbl, t.O1);
                    Check(t.TrapCount(t.O1) == 1, $"2005 1004 파헤치기 — 함정 1 → 2장 파괴 (남은 함정 {t.TrapCount(t.O1)})");
                }
                {
                    // 1019 — 80 → 160 피해. 「전부 버리기」 는 숫자가 없어 그대로
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    t.At(2);
                    t.Counter(t.O2, block);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    t.Counter(t.O1, dbl);
                    ChoiceStarted asked = t.RunUntil<ChoiceStarted>(30);
                    if (asked != null)
                        t.Server.Choose(t.O1, DiscardOrDamageEffect.TakeDamage);
                    Check(asked != null && asked.Damage == 160 && t.Hp(t.O1) == 0, $"2005 1019 목표 조준 — 고르기 판의 피해 160 · 고르면 HP 0 (알림 {asked?.Damage} · ♥{t.Hp(t.O1)})");
                }
                {
                    // 숫자가 없는 카드 — 무효화만
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005));
                    var events = Doubled(t, t.Give(t.Turn, Real(1020)), block, dbl);
                    Check(events.OfType<TurnSkipMarked>().Count() == 1, "2005 숫자 없는 카드(1020 얼음!) — 무효화만 · 건너뛰기는 1턴 그대로");
                }
                {
                    // 따블 위에 따블 — 아래 따블 무효 · 그 아래 카운터(칼집)가 2배: 뽑기 1 → 2, 피해 감소 40 은 그대로. 1001 은 2배가 아니다
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 2);
                    Card knife = t.Give(t.O1, Real(2016)), dbl1 = t.Give(t.O2, Real(2005)), dbl2 = t.Give(t.Turn, Real(2005));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, knife);
                    t.At(2 + (ChainTable.Reveal + 0.1));
                    t.Counter(t.O2, dbl1);
                    t.At(2 + (ChainTable.Reveal + 0.1) * 2);
                    bool played = t.Code(t.Counter(t.Turn, dbl2)) == 0;
                    var events = t.Run(30);
                    Check(played && t.Hp(t.O1) == 100 && events.OfType<CardDrawn>().Count(e => e.Actor == t.O1) == 2,
                        $"2005 따블 위에 따블 — 아래 따블 무효 · 칼집 뽑기 2장 · 감소 40 그대로 · 1001 은 20 그대로 (♥{t.Hp(t.O1)} · 손패 {t.HandCount(t.O1)})");
                }
                {
                    // 침착이 2배 — 회복 10 → 20, 감소 20 은 그대로
                    var t = new ChainTable();
                    t.SetHp(t.O2, 50);
                    Card calm = t.Give(t.O2, Real(2001)), dbl1 = t.Give(t.O1, Real(2005)), dbl2 = t.Give(t.Turn, Real(2005));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)));
                    t.At(2);
                    t.Counter(t.O2, calm);
                    t.At(2 + (ChainTable.Reveal + 0.1));
                    t.Counter(t.O1, dbl1);
                    t.At(2 + (ChainTable.Reveal + 0.1) * 2);
                    t.Counter(t.Turn, dbl2);
                    t.Run(30);
                    Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 50, $"2005 침착이 2배 — 1017 의 40 − 20(그대로) = 20 피해 · 회복 20 (♥{t.Hp(t.O1)} · 낸 사람 50 − 20 + 20 = ♥{t.Hp(t.O2)})");
                }
                {
                    // 으르렁이 2배 — 벌칙 2 → 4장 · 대가 1 → 2장(한 장씩 두 번 고른다)
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 5);
                    t.SetHandSize(t.O1, 3);
                    Card growl = t.Give(t.O1, Real(2021)), dbl1 = t.Give(t.O2, Real(2005)), dbl2 = t.Give(t.Turn, Real(2005));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, growl);
                    t.At(2 + (ChainTable.Reveal + 0.1));
                    t.Counter(t.O2, dbl1);
                    t.At(2 + (ChainTable.Reveal + 0.1) * 2);
                    t.Counter(t.Turn, dbl2);
                    ChoiceStarted first = t.RunUntil<ChoiceStarted>(40);
                    int turnAfterPenalty = t.HandCount(t.Turn);
                    double firstDeadline = t.Server.OpenChoiceDeadline();
                    t.Run(t.Now + 1); // 두 고르기의 마감이 달라지게
                    var again = first != null ? t.Server.Choose(t.O1, t.Hand(t.O1)[0].InstanceId).ToList() : new List<GameEvent>();
                    bool second = again.OfType<ChoiceStarted>().Any(c => c.Actor == t.O1 && c.Kind == ChoiceKind.HandCard);
                    // 앞 고르기에 보낸 늦은 요청(그 고르기의 마감을 실어 온다)은 두 번째 고르기의 답으로 받지 않는다
                    bool staleRefused = second && t.Code(t.Server.Choose(t.O1, t.Hand(t.O1)[0].InstanceId, firstDeadline)) == RejectCode.NotChoosing && t.HandCount(t.O1) == 2;
                    Check(staleRefused, $"2배가 된 대가 — 앞 고르기에 보낸 늦은 요청은 거절(코드 20) · 손패 그대로({t.HandCount(t.O1)})");
                    var rest = second ? t.Server.Choose(t.O1, t.Hand(t.O1)[0].InstanceId).ToList() : new List<GameEvent>();
                    rest.AddRange(t.Run(t.Now + 10));
                    Check(first != null && turnAfterPenalty == 1 && second && !rest.OfType<ChoiceStarted>().Any() && t.HandCount(t.O1) == 1 && t.Hp(t.O1) == 100,
                        $"2005 으르렁이 2배 — 벌칙 4장(쓴 사람 손패 {turnAfterPenalty}) · 대가를 두 번 고름(손패 {t.HandCount(t.O1)}) · 막기 그대로(♥{t.Hp(t.O1)})");
                }
                {
                    // 따블이 알아로 무효 — 2배도 없고 아래 카운터(2003)가 그대로 처리된다. 알아 위에는 아무도 못 낸다
                    var t = new ChainTable();
                    Card block = t.Give(t.O2, Real(2003)), dbl = t.Give(t.O1, Real(2005)), know = t.Give(t.O2, Real(2014)), late = t.Give(t.Turn, Real(2005));
                    Card hit = t.Give(t.Turn, Real(1001));
                    t.PlayCard(t.Turn, hit, t.O1);
                    t.At(2);
                    t.Counter(t.O2, block);
                    t.At(2 + (ChainTable.Reveal + 0.1));
                    t.Counter(t.O1, dbl);
                    t.At(2 + (ChainTable.Reveal + 0.1) * 2);
                    t.Counter(t.O2, know);
                    t.At(2 + (ChainTable.Reveal + 0.1) * 3);
                    bool refused = t.Code(t.Counter(t.Turn, late)) != 0;
                    t.Run(30);
                    Check(refused && t.Hp(t.O1) == 100 && t.Hand(t.Turn).Contains(hit), $"2005 따블이 무효되면 2배 없음 — 2003 이 그대로 1001 을 무효 · 반환 (♥{t.Hp(t.O1)}) · 알아 위에는 못 낸다");
                }
                // ── 카운터 묶음 6 (11-card-list 1-3) — 2015 가로채기 · 2018 물귀신 ──
                const double Second = 2 + ChainTable.Reveal + 0.1; // 두 번째 카운터를 낼 때
                Card TwoHit(int id) => new Card(id, 1901, CardType.Action, TargetType.TwoOthers, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 10 } }, null, ChainTable.Reveal, ChainTable.Resolve);
                {
                    // 대상 1명 — 다른 사람으로. 인원 수가 다르면 거절 5
                    var t = new ChainTable();
                    Card grab = t.Give(t.O1, Real(2015));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, grab)) == RejectCode.InvalidTarget && t.Code(t.Counter(t.O1, grab, t.O2, t.Turn)) == RejectCode.InvalidTarget, "2015 대상 0명 · 2명 → 거절 5 (아래 카드는 1명)");
                    var played = t.Counter(t.O1, grab, t.O2);
                    t.Run(20);
                    Check(played.OfType<CardPlayed>().Any() && t.Hp(t.O1) == 100 && t.Hp(t.O2) == 80, $"2015 가로채기 — 1001 의 대상을 O2 로 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    // 쓴 사람 · 나도 고를 수 있다
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2015)), t.Turn);
                    t.Run(20);
                    var s = new ChainTable();
                    s.PlayCard(s.Turn, s.Give(s.Turn, Real(1001)), s.O1);
                    s.At(2);
                    s.Counter(s.O2, s.Give(s.O2, Real(2015)), s.O2);
                    s.Run(20);
                    Check(t.Hp(t.Turn) == 80 && t.Hp(t.O1) == 100 && s.Hp(s.O2) == 80 && s.Hp(s.O1) == 100, $"2015 새 대상 = 쓴 사람(♥{t.Hp(t.Turn)}) · 나(♥{s.Hp(s.O2)})");
                }
                {
                    // 빼앗기 — 새 대상이 쓴 사람이면 불발. 대상 후보 조건(손패 1장 이상)은 새 대상에게 걸지 않는다 — 맞지 않으면 그 효과만 불발
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1002)), t.O1);
                    int turnHand = t.HandCount(t.Turn);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2015)), t.Turn);
                    t.Run(20);
                    var s = new ChainTable();
                    s.SetHandSize(s.O2, 0);
                    s.PlayCard(s.Turn, s.Give(s.Turn, Real(1002)), s.O1);
                    int sHand = s.HandCount(s.Turn);
                    s.At(2);
                    var played = s.Counter(s.O1, s.Give(s.O1, Real(2015)), s.O2);
                    s.Run(20);
                    Check(t.HandCount(t.Turn) == turnHand && t.HandCount(t.O1) == 6, $"2015 빼앗기를 쓴 사람에게 — 불발 (쓴 사람 {t.HandCount(t.Turn)} · 낸 사람 {t.HandCount(t.O1)})");
                    Check(played.OfType<CardPlayed>().Any() && s.HandCount(s.Turn) == sHand && s.HandCount(s.O2) == 0 && s.HandCount(s.O1) == 6, "2015 손패 0장인 사람에게도 돌릴 수 있다 — 빼앗기만 불발 (사용자 2026-11-23)");
                }
                {
                    // 고르지 않는 카드 · 카운터에는 못 낸다
                    var t = new ChainTable();
                    Card grab = t.Give(t.O1, Real(2015)), block = t.Give(t.O2, Real(2003));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)));
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, grab, t.Turn)) == RejectCode.CannotReact, "2015 대상이 자동인 카드(1017)에는 거절 9");
                    t.Counter(t.O2, block);
                    t.At(Second);
                    Check(t.Code(t.Counter(t.O1, grab, t.Turn)) == RejectCode.CannotReact, "2015 카운터에는 거절 9 (행동 카드에만)");
                }
                {
                    // 대상 2명 — 서로 다른 2명
                    var t = new ChainTable();
                    Card grab = t.Give(t.O1, Real(2015));
                    t.PlayCard(t.Turn, t.Give(t.Turn, TwoHit), t.O1, t.O2);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, grab, t.Turn)) == RejectCode.InvalidTarget && t.Code(t.Counter(t.O1, grab, t.Turn, t.Turn)) == RejectCode.InvalidTarget, "2015 아래 카드가 2명 — 1명 · 같은 사람 둘은 거절 5");
                    t.Counter(t.O1, grab, t.Turn, t.O1);
                    t.Run(20);
                    Check(t.Hp(t.Turn) == 90 && t.Hp(t.O1) == 90 && t.Hp(t.O2) == 100, $"2015 대상 2명을 쓴 사람 · 나로 (♥{t.Hp(t.Turn)} · ♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    // 1005 드롭킥 — 반동 판정은 새 대상 기준
                    var t = new ChainTable();
                    t.SetHp(t.O2, 40);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2015)), t.O2);
                    t.Run(20);
                    var s = new ChainTable();
                    s.PlayCard(s.Turn, s.Give(s.Turn, Real(1005)), s.O1);
                    s.At(2);
                    s.Counter(s.O1, s.Give(s.O1, Real(2015)), s.O2);
                    s.Run(20);
                    Check(t.Hp(t.O2) <= 0 && t.Hp(t.Turn) == 100 && t.Hp(t.O1) == 100, $"2015 + 1005 — 새 대상을 쓰러뜨리면 반동 없음 (쓴 사람 ♥{t.Hp(t.Turn)})");
                    Check(s.Hp(s.O2) == 51 && s.Hp(s.Turn) == 51 && s.Hp(s.O1) == 100, $"2015 + 1005 — 새 대상을 못 쓰러뜨리면 반동 (♥{s.Hp(s.O2)} · 쓴 사람 ♥{s.Hp(s.Turn)})");
                }
                {
                    // 알아(2014)로 무효 — 원래 대상 그대로
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2015)), t.O2);
                    t.At(Second);
                    t.Counter(t.O2, t.Give(t.O2, Real(2014)));
                    t.Run(20);
                    Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 100, $"2015 가 무효되면 원래 대상 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    // 2018 물귀신 — 나는 그대로, 고른 사람도 같은 양. 준 사람 = 아래 카드를 쓴 사람
                    var statsField = typeof(GameServer).GetField(ServerStatsField, Private);
                    var t = new ChainTable();
                    Card ghost = t.Give(t.O1, Real(2018)), other = t.Give(t.O2, Real(2018));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O2, other, t.Turn)) == RejectCode.CannotReact, "2018 피해를 받지 않는 사람은 거절 9");
                    Check(t.Code(t.Counter(t.O1, ghost)) == RejectCode.InvalidTarget && t.Code(t.Counter(t.O1, ghost, t.O1)) == RejectCode.InvalidTarget, "2018 대상 없음 · 나 → 거절 5");
                    t.Counter(t.O1, ghost, t.O2);
                    t.Run(20);
                    int dealt = ((Dictionary<int, int[]>)statsField.GetValue(t.Server))[t.Turn][(int)StatKind.Dealt];
                    Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 80 && t.Hp(t.Turn) == 100, $"2018 물귀신 — 나 20 · 고른 사람도 20 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                    Check(dealt == 40, $"2018 같이 받은 피해를 준 사람 = 아래 카드를 쓴 사람 (가한 피해 {dealt})");
                }
                {
                    // 후보 — 그 카드의 피해를 받는 사람은 못 고른다. 쓴 사람은 고를 수 있다
                    var t = new ChainTable();
                    Card ghost = t.Give(t.O1, Real(2018));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)));
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, ghost, t.O2)) == RejectCode.InvalidTarget, "2018 같은 피해를 받는 사람은 거절 5");
                    t.Counter(t.O1, ghost, t.Turn);
                    t.Run(20);
                    Check(t.Hp(t.Turn) == 60 && t.Hp(t.O1) == 60 && t.Hp(t.O2) == 60, $"2018 전원 피해(1017) — 쓴 사람을 고름 (쓴 사람 ♥{t.Hp(t.Turn)})");

                    // 모두가 받는 피해 — 고를 사람이 없어 낼 수 없다
                    var s = new ChainTable();
                    Card none = s.Give(s.O1, Real(2018));
                    s.Play(s.Turn, ChainTable.Storm, null);
                    s.At(2);
                    Check(s.Code(s.Counter(s.O1, none, s.O2)) == RejectCode.CannotReact, "2018 후보가 없으면(모두가 받는 피해) 거절 9");
                }
                {
                    // 1005 드롭킥 — 쓴 사람을 고를 수 있다(반동은 조건부). 고른 사람이 쓰러져도 「쓰러뜨림」 이 아니다 → 반동
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    t.At(2);
                    var played = t.Counter(t.O1, t.Give(t.O1, Real(2018)), t.Turn);
                    t.Run(20);
                    Check(played.OfType<CardPlayed>().Any() && t.Hp(t.O1) == 51 && t.Hp(t.Turn) == 2, $"2018 + 1005 — 쓴 사람도 49 · 못 쓰러뜨려 반동 49 (♥{t.Hp(t.O1)} · 쓴 사람 ♥{t.Hp(t.Turn)})");
                    var s = new ChainTable();
                    s.SetHp(s.O2, 40);
                    s.PlayCard(s.Turn, s.Give(s.Turn, Real(1005)), s.O1);
                    s.At(2);
                    s.Counter(s.O1, s.Give(s.O1, Real(2018)), s.O2);
                    s.Run(20);
                    Check(s.Hp(s.O2) <= 0 && s.Hp(s.O1) == 51 && s.Hp(s.Turn) == 51, $"2018 + 1005 — 고른 사람이 쓰러져도 쓰러뜨림이 아니다 · 반동 (쓴 사람 ♥{s.Hp(s.Turn)})");
                }
                {
                    // 고른 사람이 물귀신 위에 칼집(2016) — 같이 받는 피해 20 − 40 = 0, 1장 뽑음. 나는 그대로
                    var t = new ChainTable();
                    Card knife = t.Give(t.O2, Real(2016));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2018)), t.O2);
                    t.At(Second);
                    int hand = t.HandCount(t.O2);
                    var played = t.Counter(t.O2, knife);
                    t.Run(20);
                    Check(played.OfType<CardPlayed>().Any() && t.Hp(t.O1) == 80 && t.Hp(t.O2) == 100 && t.HandCount(t.O2) == hand, $"2018 위에 칼집 — 고른 사람은 피해 0 · 1장 뽑음 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)} · 손패 {t.HandCount(t.O2)})");
                }
                {
                    // 물귀신 위에 물귀신 — 고른 사람이 다시 쓴 사람을 끌어들인다
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2018)), t.O2);
                    t.At(Second);
                    t.Counter(t.O2, t.Give(t.O2, Real(2018)), t.Turn);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 80 && t.Hp(t.Turn) == 80, $"2018 위에 2018 — 세 사람 모두 20 (♥{t.Hp(t.Turn)} · ♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    // 1019 목표 조준 — 낼 수 있다. 피해를 고르면 같이, 전부 버리기를 고르면 없다
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    t.At(2);
                    var played = t.Counter(t.O1, t.Give(t.O1, Real(2018)), t.O2);
                    if (t.RunUntil<ChoiceStarted>(30) != null)
                        t.Server.Choose(t.O1, DiscardOrDamageEffect.TakeDamage);
                    var s = new ChainTable();
                    s.PlayCard(s.Turn, s.Give(s.Turn, Real(1019)), s.O1);
                    s.At(2);
                    s.Counter(s.O1, s.Give(s.O1, Real(2018)), s.O2);
                    if (s.RunUntil<ChoiceStarted>(30) != null)
                        s.Server.Choose(s.O1, DiscardOrDamageEffect.DiscardAll);
                    Check(played.OfType<CardPlayed>().Any() && t.Hp(t.O1) == 20 && t.Hp(t.O2) == 20, $"2018 + 1019 — 피해를 고르면 같이 80 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                    Check(s.Hp(s.O1) == 100 && s.Hp(s.O2) == 100 && s.HandCount(s.O1) == 0, $"2018 + 1019 — 전부 버리기를 고르면 같이 받는 피해도 없다 (♥{s.Hp(s.O2)})");
                }
                {
                    // 알아(2014)로 무효
                    var t = new ChainTable();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, t.Give(t.O1, Real(2018)), t.O2);
                    t.At(Second);
                    t.Counter(t.O2, t.Give(t.O2, Real(2014)));
                    t.Run(20);
                    Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 100, $"2018 이 무효되면 나만 받는다 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    // 봇 (18-bot 7-4) — 대상은 찹츄 → 손패 많은 → HP 낮은 상대. 2015 는 나를 고르지 않는다
                    CardPlayed BotCounter(int counterId, out ChainTable table)
                    {
                        double now = 0;
                        var t = new ChainTable(() => now);
                        table = t;
                        t.Hand(t.O1).RemoveAll(c => c.Type == CardType.Counter);
                        t.Give(t.O1, Real(counterId));
                        t.Give(t.O2, Real(1001));
                        t.Give(t.O2, Real(1001)); // O2 의 손패가 가장 많다
                        var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => now);
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                        var events = new List<GameEvent>();
                        for (now = 0; now < ChainTable.Reveal + ReactionSeconds - 0.1; now += 0.1)
                            events.AddRange(driver.Tick());
                        return events.OfType<CardPlayed>().FirstOrDefault();
                    }
                    CardPlayed ghost = BotCounter(2018, out ChainTable g), grab = BotCounter(2015, out ChainTable r);
                    Check(ghost != null && ghost.CardId == 2018 && ghost.Targets.SequenceEqual(new[] { g.O2 }), "봇의 2018 물귀신 — 손패가 많은 상대를 고름");
                    Check(grab != null && grab.CardId == 2015 && grab.Targets.SequenceEqual(new[] { r.O2 }), "봇의 2015 가로채기 — 손패가 많은 상대에게 (나를 고르지 않는다)");

                    // 나에게 이로운 효과뿐인 카드(1006 회복)에는 카운터를 내지 않는다 — 피해 카드에는 그대로 낸다 (18-bot 7-4, 사용자 2026-11-24)
                    bool BotCounters(int actionId)
                    {
                        double now = 0;
                        var t = new ChainTable(() => now);
                        t.Hand(t.O1).RemoveAll(c => c.Type == CardType.Counter);
                        t.Give(t.O1, Real(2002));
                        t.Give(t.O1, Real(2021));
                        t.SetHp(t.O1, 50);
                        var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => now);
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(actionId)), t.O1);
                        var events = new List<GameEvent>();
                        for (now = 0; now < ChainTable.Reveal + ReactionSeconds - 0.1; now += 0.1)
                            events.AddRange(driver.Tick());
                        return events.OfType<CardPlayed>().Any(e => e.Actor == t.O1);
                    }
                    Check(!BotCounters(1006) && BotCounters(1001), "봇 — 회복뿐인 카드(1006)에는 반사 · 으르렁을 내지 않고, 피해 카드(1001)에는 낸다");

                    // 반사만 쥔 봇 — 1011(HP 바꾸기)에는 내지 않고, 1005(반동)는 쓴 사람이 그 49 로 쓰러질 때만 낸다 (18-bot 7-4, 사용자 2026-11-26)
                    bool BotReflects(int actionId, int userHp)
                    {
                        double now = 0;
                        var t = new ChainTable(() => now);
                        t.Hand(t.O1).RemoveAll(c => c.Type == CardType.Counter);
                        t.Give(t.O1, Real(2002));
                        t.SetHp(t.Turn, userHp);
                        var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => now);
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(actionId)), t.O1);
                        var events = new List<GameEvent>();
                        for (now = 0; now < ChainTable.Reveal + ReactionSeconds - 0.1; now += 0.1)
                            events.AddRange(driver.Tick());
                        return events.OfType<CardPlayed>().Any(e => e.Actor == t.O1 && e.CardId == 2002);
                    }
                    bool swap = BotReflects(1011, 30), kickHigh = BotReflects(1005, 100), kickLow = BotReflects(1005, 49), bite = BotReflects(1001, 100);
                    Check(!swap && !kickHigh && kickLow && bite, $"봇의 2002 반사 — 1011 안 냄({!swap}) · 1005 는 쓴 사람 ♥100 이면 안 냄({!kickHigh}) · ♥49 면 냄({kickLow}) · 1001 냄({bite})");
                }

                {
                    // 2021 으르렁 — 아래 카드를 쓴 사람이 반응 시간 중에 나가도 낼 수 있다: 무효 · 대가는 그대로, 벌칙만 없다 (사용자 2026-11-25)
                    var t = new ChainTable();
                    int hand = t.HandCount(t.O1);
                    Card growl = t.Give(t.O1, Real(2021));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Server.RemovePlayer(t.Turn);
                    List<GameEvent> events = t.Counter(t.O1, growl).ToList();
                    bool played = events.OfType<CardPlayed>().Any(e => e.CardId == 2021 && e.Targets.Length == 0);
                    events.AddRange(t.Run(30));
                    Check(played && events.OfType<CardNegated>().Any(e => e.CardId == 1001) && t.Hp(t.O1) == 100 && t.HandCount(t.O1) == hand - 1,
                        $"2021 쓴 사람이 나간 뒤에도 낼 수 있다 — 무효 · 대가 1장, 벌칙 없음 (♥{t.Hp(t.O1)} · 손패 {hand}→{t.HandCount(t.O1)})");
                }

                // ── 카운터 묶음 8 (11-card-list 1-3 · 03-turn 7절) — 2013 새치기 ──
                {
                    // 차례인 사람이 1001 을 O1 에게 내고 taker 가 새치기 — until 초까지 돌린 사건 (새치기를 낸 요청의 사건부터)
                    List<GameEvent> Cut(ChainTable x, int taker, double until)
                    {
                        Card cut = x.Give(taker, Real(2013));
                        x.PlayCard(x.Turn, x.Give(x.Turn, Real(1001)), x.O1);
                        x.At(2);
                        List<GameEvent> events = x.Counter(taker, cut).ToList();
                        events.AddRange(x.Run(until));
                        return events;
                    }
                    string Starts(List<GameEvent> events) => string.Join(" ", events.OfType<TurnStarted>().Take(3).Select(e => e.Actor));
                    {
                        // 막고 · 대가 1장 · 바로 내 턴 · 그 뒤는 내 다음 사람부터 (원래 순서로 돌아가지 않는다)
                        var t = new ChainTable();
                        int hand = t.HandCount(t.O2);
                        List<GameEvent> events = Cut(t, t.O2, 70);
                        Check(events.OfType<CardPlayed>().Any(e => e.CardId == 2013) && events.OfType<CardNegated>().Any(e => e.CardId == 1001) && t.Hp(t.O1) == 100,
                            $"2013 새치기 — 행동 카드를 막는다 (대상 ♥{t.Hp(t.O1)})");
                        Check(events.OfType<ChoiceStarted>().Any(e => e.Actor == t.O2 && e.Kind == ChoiceKind.HandCard) && t.HandCount(t.O2) == hand - 1,
                            $"2013 대가 — 손패 1장을 골라 버린다 (손패 {hand}→{t.HandCount(t.O2)})");
                        Check(Starts(events) == $"{t.O2} {t.Turn} {t.O1}", $"2013 다음 턴은 새치기를 낸 사람 · 그 뒤는 그 사람 다음부터 (턴 {Starts(events)} · 기대 {t.O2} {t.Turn} {t.O1})");
                    }
                    {
                        // 건너뛴 사람의 「다음 턴」 상태는 쓰이지 않고 남는다 — 다음 실제 차례에 쓰인다
                        var t = new ChainTable();
                        t.State(t.O1).SkipNextTurn = true;
                        t.State(t.O1).BlockActionNextTurn = true;
                        List<GameEvent> events = Cut(t, t.O2, 25);
                        bool kept = t.Server.CurrentTurnActor == t.O2 && t.State(t.O1).SkipNextTurn && t.State(t.O1).BlockActionNextTurn && !events.OfType<TurnSkipped>().Any();
                        events = t.Run(70);
                        Check(kept && events.OfType<TurnSkipped>().Any(e => e.Actor == t.O1) && !t.State(t.O1).SkipNextTurn && t.State(t.O1).BlockActionNextTurn,
                            "2013 건너뛴 사람의 턴 스킵 · 행동 금지는 남는다 — 스킵은 다음 실제 차례에 쓰인다");
                    }
                    {
                        // 바로 다음 사람이 내면 순서 그대로 · 역방향에서도 낸 사람의 다음부터
                        var t = new ChainTable();
                        List<GameEvent> events = Cut(t, t.O1, 70);
                        var s = new ChainTable();
                        typeof(GameServer).GetField("_reversed", Private).SetValue(s.Server, true); // 역방향 — 보통이면 Turn → O2 → O1
                        List<GameEvent> back = Cut(s, s.O1, 70);
                        Check(Starts(events) == $"{t.O1} {t.O2} {t.Turn}", $"2013 바로 다음 사람이 내면 순서 그대로 (턴 {Starts(events)})");
                        Check(Starts(back) == $"{s.O1} {s.Turn} {s.O2}", $"2013 역방향 — 새치기를 낸 사람부터 역방향으로 (턴 {Starts(back)} · 기대 {s.O1} {s.Turn} {s.O2})");
                    }
                    {
                        // 새치기 턴은 보통 턴 — 대가로 딱 10장이 되면 찹츄 선언, 내 턴이 시작되며 승리
                        var t = new ChainTable();
                        t.SetHandSize(t.O2, GameServer.ChapchuHandCount + 1); // + 새치기 → 내고 11장 → 대가 뒤 10장
                        List<GameEvent> events = Cut(t, t.O2, 25);
                        GameEnded ended = events.OfType<GameEnded>().FirstOrDefault();
                        Check(ended != null && ended.Winner == t.O2 && ended.Reason == GameEndReason.Chapchu && !events.OfType<TurnStarted>().Any(),
                            $"2013 새치기 턴의 시작에 찹츄 판정 — 손패 10장이면 승리 (승자 P{ended?.Winner})");
                    }
                    {
                        // 낸 사람에게 턴 스킵 — 새치기 턴이 건너뛰어지고(스킵은 쓰인다) 그 다음 사람에게
                        var t = new ChainTable();
                        t.State(t.O2).SkipNextTurn = true;
                        List<GameEvent> events = Cut(t, t.O2, 25);
                        Check(events.OfType<TurnSkipped>().Any(e => e.Actor == t.O2) && !t.State(t.O2).SkipNextTurn && Starts(events) == $"{t.Turn}",
                            $"2013 낸 사람에게 턴 스킵 — 건너뛰고 그 다음 사람 (턴 {Starts(events)} · 기대 {t.Turn})");
                    }
                    {
                        // 새치기가 무효되면 버리기도 내 턴도 없다 · 내 카드 · 카운터 위에는 못 낸다
                        var t = new ChainTable();
                        int hand = t.HandCount(t.O2);
                        Card cut = t.Give(t.O2, Real(2013)), mine = t.Give(t.Turn, Real(2013)), late = t.Give(t.Turn, Real(2013));
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                        t.At(2);
                        int own = t.Code(t.Counter(t.Turn, mine));
                        List<GameEvent> events = t.Counter(t.O2, cut).ToList();
                        t.At(2 + ChainTable.Reveal + 0.1);
                        int onCounter = t.Code(t.Counter(t.Turn, late));
                        events.AddRange(t.Counter(t.O1, ChainTable.Negate));
                        events.AddRange(t.Run(40));
                        Check(own == RejectCode.CannotReact && onCounter != 0, $"2013 내 카드 · 카운터 위에는 거절 (코드 {own} · {onCounter})");
                        Check(events.OfType<CardNegated>().Any(e => e.CardId == 2013) && !events.OfType<ChoiceStarted>().Any() && t.HandCount(t.O2) == hand && t.Hp(t.O1) == 80
                              && Starts(events).StartsWith($"{t.O1}"), $"2013 이 무효되면 버리기도 내 턴도 없다 (손패 {t.HandCount(t.O2)} · 대상 ♥{t.Hp(t.O1)} · 턴 {Starts(events)})");
                    }
                    {
                        // 손패 0장이면 대가 없이 · 낸 사람이 턴 전에 나가면 그 자리의 다음 사람
                        var t = new ChainTable();
                        t.SetHandSize(t.O2, 0);
                        List<GameEvent> events = Cut(t, t.O2, 25);
                        var s = new ChainTable();
                        Card cut = s.Give(s.O2, Real(2013));
                        s.PlayCard(s.Turn, s.Give(s.Turn, Real(1001)), s.O1);
                        s.At(2);
                        s.Counter(s.O2, cut);
                        bool choosing = s.RunUntil<ChoiceStarted>(30) != null;
                        List<GameEvent> left = s.Server.RemovePlayer(s.O2).ToList();
                        left.AddRange(s.Run(s.Now + 15));
                        Check(!events.OfType<ChoiceStarted>().Any() && Starts(events).StartsWith($"{t.O2}"), $"2013 손패 0장 — 대가 없이 내 턴 (턴 {Starts(events)})");
                        Check(choosing && Starts(left).StartsWith($"{s.Turn}"), $"2013 낸 사람이 턴 전에 나감 — 그 자리의 다음 사람 (턴 {Starts(left)} · 기대 {s.Turn})");
                    }
                    {
                        // 막힌 사람의 턴 끝 처리(피폭)는 보통대로 — 그 뒤 새치기 턴
                        var t = new ChainTable();
                        typeof(GameServer).GetField("_restartCount", Private).SetValue(t.Server, 1); // 대소동 1번 뒤 — 턴이 끝날 때마다 피폭
                        List<GameEvent> events = Cut(t, t.O2, 25);
                        int fallout = events.FindIndex(e => e is FalloutChanged), start = events.FindIndex(e => e is TurnStarted);
                        Check(fallout >= 0 && fallout < start && Starts(events) == $"{t.O2}" && new[] { t.Turn, t.O1, t.O2 }.All(a => t.Hp(a) == 99),
                            $"2013 막힌 사람의 턴 끝 피폭 뒤에 새치기 턴 (턴 {Starts(events)} · ♥{t.Hp(t.Turn)} · ♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                    }
                    {
                        // 개판 — 실제로 진행된 턴만 센다: O1 의 턴 · 새치기 턴(Turn) · O1 의 턴으로 한 바퀴 (O2 는 그 바퀴에 턴이 없다)
                        var t = new ChainTable();
                        t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                        t.RunUntilTurn(t.O1, t.Now + 30);
                        Card cut = t.Give(t.Turn, Real(2013));
                        t.PlayCard(t.O1, t.Give(t.O1, Real(1001)), t.O2);
                        t.At(t.Now + 2);
                        List<GameEvent> events = t.Counter(t.Turn, cut).ToList();
                        for (double end = t.Now + 80; t.Now < end && !events.OfType<GaepanChanged>().Any(g => g.Left == 4);)
                            events.AddRange(t.Run(t.Now + 0.1));
                        int lap = events.FindIndex(e => e is GaepanChanged g && g.Left == 4);
                        string before = string.Join(" ", events.Take(lap < 0 ? 0 : lap).OfType<TurnStarted>().Select(e => e.Actor));
                        Check(lap >= 0 && before == $"{t.Turn} {t.O1}", $"2013 + 1021 — 실제로 진행된 턴만 센다 (한 바퀴까지 시작된 턴 {before} · 기대 {t.Turn} {t.O1})");
                    }
                    {
                        // 스킵으로 건너뛰어진 새치기 턴도 개판에서 1턴 — O1 의 턴 · 스킵된 새치기 턴 · O2 의 턴으로 한 바퀴
                        var t = new ChainTable();
                        t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                        t.RunUntilTurn(t.O1, t.Now + 30);
                        t.State(t.Turn).SkipNextTurn = true;
                        Card cut = t.Give(t.Turn, Real(2013));
                        t.PlayCard(t.O1, t.Give(t.O1, Real(1001)), t.O2);
                        t.At(t.Now + 2);
                        List<GameEvent> events = t.Counter(t.Turn, cut).ToList();
                        for (double end = t.Now + 80; t.Now < end && !events.OfType<GaepanChanged>().Any(g => g.Left == 4);)
                            events.AddRange(t.Run(t.Now + 0.1));
                        int lap = events.FindIndex(e => e is GaepanChanged g && g.Left == 4);
                        string before = string.Join(" ", events.Take(lap < 0 ? 0 : lap).OfType<TurnStarted>().Select(e => e.Actor));
                        Check(lap >= 0 && events.OfType<TurnSkipped>().Any(e => e.Actor == t.Turn) && before == $"{t.O1}",
                            $"2013 + 1021 — 스킵된 새치기 턴도 1턴으로 센다 (한 바퀴까지 시작된 턴 {before} · 기대 {t.O1})");
                    }
                }

                // ── 2002 반사 (11-card-list 1-3, 사용자 2026-11-26) — 아래 행동 카드를 무효로 하고, 반사한 사람이 쓴 사람에게 다시 쓴다 ──
                {
                    var statsField = typeof(GameServer).GetField(ServerStatsField, Private);
                    int Stat(ChainTable x, int actor, StatKind kind) => ((Dictionary<int, int[]>)statsField.GetValue(x.Server))[actor][(int)kind];
                    int Dealt(ChainTable x, int actor) => Stat(x, actor, StatKind.Dealt);
                    // 차례인 사람이 action 을 내고(대상 = targets) O1 이 반사 — 20초까지 돌린다. 낸 직후의 손패 장수를 돌려준다 (쓴 사람 · O1 · O2 · 반사가 나갔으면 1)
                    int[] Reflected(ChainTable x, int actionId, params int[] targets)
                    {
                        Card mirror = x.Give(x.O1, Real(2002));
                        x.PlayCard(x.Turn, x.Give(x.Turn, Real(actionId)), targets);
                        x.At(2);
                        bool ok = x.Counter(x.O1, mirror).OfType<CardPlayed>().Any();
                        int[] hands = { x.HandCount(x.Turn), x.HandCount(x.O1), x.HandCount(x.O2), ok ? 1 : 0 };
                        x.Run(20);
                        return hands;
                    }
                    {
                        // 피해 — 반사한 사람이 쓴 사람에게 다시 쓴다. 대상이 아닌 사람 · 내 카드에는 못 낸다. 낸 순간에는 아무 일도 없다 (체인에서 기다린다)
                        var t = new ChainTable();
                        Card mirror = t.Give(t.O1, Real(2002)), other = t.Give(t.O2, Real(2002)), mine = t.Give(t.Turn, Real(2002));
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                        t.At(2);
                        Check(t.Code(t.Counter(t.O2, other)) == RejectCode.CannotReact && t.Code(t.Counter(t.Turn, mine)) == RejectCode.CannotReact, "2002 대상이 아닌 사람 · 내 카드에는 거절 9");
                        List<GameEvent> resolved = t.Counter(t.O1, mirror).ToList();
                        Check(t.Hp(t.Turn) == 100 && t.Hp(t.O1) == 100 && !resolved.OfType<CardResolved>().Any(), "2002 를 낸 순간에는 효과가 없다 — 체인에서 반응 시간을 기다린다");
                        resolved.AddRange(t.Run(20));
                        CardResolved[] shown = resolved.OfType<CardResolved>().Where(e => e.CardId == 1001).ToArray();
                        Check(t.Hp(t.Turn) == 80 && t.Hp(t.O1) == 100, $"2002 반사 — 1001 을 내가 쓴 사람에게 다시 쓴다 (쓴 사람 ♥{t.Hp(t.Turn)} · 나 ♥{t.Hp(t.O1)})");
                        Check(Dealt(t, t.O1) == 20 && Dealt(t, t.Turn) == 0, $"2002 다시 쓴 피해를 준 사람 = 반사한 사람 (가한 피해 {Dealt(t, t.O1)} · 쓴 사람 {Dealt(t, t.Turn)})");
                        Check(shown.Length == 1 && shown[0].Actor == t.O1 && shown[0].Targets.SequenceEqual(new[] { t.Turn }) && !resolved.OfType<CardNegated>().Any(),
                            $"2002 처리 알림 — 1001 이 한 번, 쓴 사람 = 반사한 사람 · 대상 = 원래 쓴 사람 (알림 {shown.Length}번)");
                        Check(Stat(t, t.O1, StatKind.Actions) == 0 && Stat(t, t.Turn, StatKind.Actions) == 1, $"2002 다시 쓴 것은 행동 카드 사용에 세지 않는다 (반사한 사람 {Stat(t, t.O1, StatKind.Actions)} · 쓴 사람 {Stat(t, t.Turn, StatKind.Actions)})");
                        ChainDiscarded pile = resolved.OfType<ChainDiscarded>().FirstOrDefault();
                        Check(pile != null && pile.CardIds.Count(id => id == 1001) == 1, "2002 반사된 카드는 버림 더미에 한 장만 간다");
                    }
                    {
                        // 빼앗기 — 내가 쓴 사람의 카드를. 전원 카드는 반사한 사람 기준 전원
                        var t = new ChainTable();
                        int[] h = Reflected(t, 1002, t.O1);
                        var s = new ChainTable();
                        int[] g = Reflected(s, 1014);
                        var u = new ChainTable();
                        int[] k = Reflected(u, 1015, u.O1);
                        Check(h[3] == 1 && t.HandCount(t.Turn) == h[0] - 1 && t.HandCount(t.O1) == h[1] + 1, $"2002 + 1002 — 내가 쓴 사람의 1장을 가져온다 (쓴 사람 {h[0]}→{t.HandCount(t.Turn)} · 나 {h[1]}→{t.HandCount(t.O1)})");
                        Check(g[3] == 1 && s.HandCount(s.Turn) == g[0] - 1 && s.HandCount(s.O1) == g[1] + 2 && s.HandCount(s.O2) == g[2] - 1, $"2002 + 1014 — 내가 나를 뺀 모두에게서 1장씩 (쓴 사람 {g[0]}→{s.HandCount(s.Turn)} · 나 {g[1]}→{s.HandCount(s.O1)} · O2 {g[2]}→{s.HandCount(s.O2)})");
                        Check(k[3] == 1 && u.Hp(u.Turn) == 90 && u.Hp(u.O1) == 100 && u.HandCount(u.O1) == k[1] + 1 && u.HandCount(u.Turn) == k[0] - 1, $"2002 + 1015 — 쓴 사람이 10 피해 · 내가 1장 (쓴 사람 ♥{u.Hp(u.Turn)} · 나 손패 {u.HandCount(u.O1)})");
                    }
                    {
                        // 전원 대상 — 반사한 사람 기준 전원 (쓴 사람 + 나머지). 준 사람은 모두 반사한 사람
                        var t = new ChainTable();
                        Reflected(t, 1017);
                        var s = new ChainTable();
                        int[] g = Reflected(s, 1013);
                        Check(t.Hp(t.Turn) == 60 && t.Hp(t.O1) == 100 && t.Hp(t.O2) == 60 && Dealt(t, t.O1) == 80 && Dealt(t, t.Turn) == 0,
                            $"2002 + 1017 — 내가 나를 뺀 모두에게 40 (♥{t.Hp(t.Turn)} · ♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)} · 내가 준 피해 {Dealt(t, t.O1)} · 쓴 사람 {Dealt(t, t.Turn)})");
                        Check(g[3] == 1 && s.HandCount(s.Turn) == 5 && s.HandCount(s.O1) == g[1] && s.HandCount(s.O2) == 5, $"2002 + 1013 — 나를 뺀 모두가 다시 뽑고 나는 그대로 (쓴 사람 {s.HandCount(s.Turn)} · 나 {s.HandCount(s.O1)} · O2 {s.HandCount(s.O2)})");
                    }
                    {
                        // 턴 건너뛰기 · 행동 금지 · 회복 — 자동 대상 카드도 원래 쓴 사람에게
                        var t = new ChainTable();
                        Card mirror = t.Give(t.O1, Real(2002));
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1020)));
                        t.At(2);
                        bool played = t.Counter(t.O1, mirror).OfType<CardPlayed>().Any();
                        bool marked = t.Run(2 + ChainTable.Reveal + 6).OfType<TurnSkipMarked>().Any(e => e.Actor == t.Turn);
                        var s = new ChainTable();
                        Card mirror2 = s.Give(s.O1, Real(2002));
                        s.PlayCard(s.Turn, s.Give(s.Turn, Real(1007)), s.O1);
                        s.At(2);
                        s.Counter(s.O1, mirror2);
                        s.Run(2 + ChainTable.Reveal + 5.5);
                        var u = new ChainTable();
                        u.SetHp(u.Turn, 50);
                        u.SetHp(u.O1, 50);
                        Reflected(u, 1006, u.O1);
                        Check(played && marked && !t.State(t.O1).SkipNextTurn, "2002 + 1020 — 쓴 사람이 1턴 건너뛴다");
                        Check(s.State(s.Turn).BlockActionNextTurn && !s.State(s.O1).BlockActionNextTurn && !s.State(s.O1).ActionBlocked, "2002 + 1007 — 쓴 사람이 다음 턴 행동 카드를 못 쓴다");
                        Check(u.Hp(u.Turn) == 70 && u.Hp(u.O1) == 50, $"2002 + 1006 — 회복도 쓴 사람이 (쓴 사람 ♥{u.Hp(u.Turn)} · 나 ♥{u.Hp(u.O1)})");
                    }
                    {
                        // 1005 드롭킥 — 반동(불이익)은 다시 쓴 사람(반사한 사람)이 진다. 쓴 사람이 쓰러지면 반동 없음
                        var t = new ChainTable();
                        Reflected(t, 1005, t.O1);
                        var s = new ChainTable();
                        s.SetHp(s.Turn, 40);
                        Reflected(s, 1005, s.O1);
                        Check(t.Hp(t.Turn) == 51 && t.Hp(t.O1) == 51 && Dealt(t, t.O1) == 49 && Dealt(t, t.Turn) == 0, $"2002 + 1005 — 쓴 사람이 49 · 못 쓰러뜨려 반동 49 는 반사한 사람이 (쓴 사람 ♥{t.Hp(t.Turn)} · 나 ♥{t.Hp(t.O1)} · 내가 준 피해 {Dealt(t, t.O1)})");
                        Check(s.Hp(s.Turn) <= 0 && s.Hp(s.O1) == 100 && Dealt(s, s.O1) == 40, $"2002 + 1005 — 쓴 사람이 쓰러지면 반동 없음 (쓴 사람 ♥{s.Hp(s.Turn)} · 나 ♥{s.Hp(s.O1)} · 준 피해 {Dealt(s, s.O1)})");
                    }
                    {
                        // 1011 운명 뒤집기 — 다시 써도 같은 두 사람이 바꾼다 (예외 없음)
                        var t = new ChainTable();
                        t.SetHp(t.Turn, 30);
                        t.SetHp(t.O1, 90);
                        int[] h = Reflected(t, 1011, t.O1);
                        Check(h[3] == 1 && t.Hp(t.Turn) == 90 && t.Hp(t.O1) == 30, $"2002 + 1011 — HP 가 바뀐다 (쓴 사람 ♥{t.Hp(t.Turn)} · 나 ♥{t.Hp(t.O1)})");
                    }
                    {
                        // 1018 무야호 — 대상(손패 최다)이 나일 때만. 쓴 사람이 나눠 주는 사람이 된다 · 받는 쪽은 못 낸다
                        var t = new ChainTable();
                        t.SetHandSize(t.O1, 9);
                        Card mirror = t.Give(t.O1, Real(2002)), other = t.Give(t.O2, Real(2002));
                        var played = t.PlayCard(t.Turn, t.Give(t.Turn, Real(1018)));
                        int turnHand = t.HandCount(t.Turn), myHand = t.HandCount(t.O1) - 1, otherHand = t.HandCount(t.O2);
                        t.At(2);
                        bool aimed = played.OfType<CardPlayed>().Any(e => e.Targets.SequenceEqual(new[] { t.O1 }));
                        Check(aimed && t.Code(t.Counter(t.O2, other)) == RejectCode.CannotReact, "2002 + 1018 — 받는 쪽은 거절 9 (대상이 아니다)");
                        t.Counter(t.O1, mirror);
                        t.Run(20);
                        Check(t.HandCount(t.Turn) == turnHand - 2 && t.HandCount(t.O1) == myHand + 1 && t.HandCount(t.O2) == otherHand + 1, $"2002 + 1018 — 쓴 사람이 모두에게 1장씩 (쓴 사람 {turnHand}→{t.HandCount(t.Turn)} · 나 {myHand}→{t.HandCount(t.O1)} · O2 {otherHand}→{t.HandCount(t.O2)})");
                    }
                    {
                        // 1019 목표 조준 — 쓴 사람이 고른다
                        var t = new ChainTable();
                        Card mirror = t.Give(t.O1, Real(2002));
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                        t.At(2);
                        t.Counter(t.O1, mirror);
                        ChoiceStarted asked = t.RunUntil<ChoiceStarted>(30);
                        bool refused = asked != null && t.Code(t.Server.Choose(t.O1, DiscardOrDamageEffect.TakeDamage)) != 0;
                        if (asked != null)
                            t.Server.Choose(t.Turn, DiscardOrDamageEffect.TakeDamage);
                        Check(asked != null && asked.Actor == t.Turn && refused && t.Hp(t.Turn) == 20 && t.Hp(t.O1) == 100, $"2002 + 1019 — 쓴 사람이 고른다 · 80 피해 (고르는 사람 {asked?.Actor} · 쓴 사람 ♥{t.Hp(t.Turn)})");
                    }
                    {
                        // 반사는 행동 카드에만 낸다 — 나를 노린 카운터(으르렁의 벌칙 · 물귀신 · 가로채기가 고른 사람) 위에는 거절 9
                        var t = new ChainTable();
                        Card mirror = t.Give(t.Turn, Real(2002));
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                        t.At(2);
                        t.Counter(t.O1, t.Give(t.O1, Real(2021)));
                        t.At(Second);
                        int growl = t.Code(t.Counter(t.Turn, mirror));
                        var s = new ChainTable();
                        s.PlayCard(s.Turn, s.Give(s.Turn, Real(1001)), s.O1);
                        s.At(2);
                        s.Counter(s.O1, s.Give(s.O1, Real(2018)), s.O2);
                        s.At(Second);
                        int ghost = s.Code(s.Counter(s.O2, s.Give(s.O2, Real(2002))));
                        var u = new ChainTable();
                        u.PlayCard(u.Turn, u.Give(u.Turn, Real(1001)), u.O1);
                        u.At(2);
                        u.Counter(u.O1, u.Give(u.O1, Real(2015)), u.O2);
                        u.At(Second);
                        int grab = u.Code(u.Counter(u.O2, u.Give(u.O2, Real(2002))));
                        Check(growl == RejectCode.CannotReact && ghost == RejectCode.CannotReact && grab == RejectCode.CannotReact, $"2002 카운터 위에는 거절 9 (으르렁 {growl} · 물귀신 {ghost} · 가로채기 {grab})");
                    }
                    {
                        // 판 전체 상태를 바꾸는 카드에는 못 낸다
                        string codes = string.Join(" · ", new[] { 1012, 1021, 1023, 1045 }.Select(id =>
                        {
                            var b = new ChainTable();
                            Card mirror = b.Give(b.O1, Real(2002));
                            bool played = b.PlayCard(b.Turn, b.Give(b.Turn, Real(id))).OfType<CardPlayed>().Any();
                            b.At(2);
                            return $"{id}:{(played ? b.Code(b.Counter(b.O1, mirror)) : -1)}";
                        }));
                        Check(codes == $"1012:{RejectCode.CannotReact} · 1021:{RejectCode.CannotReact} · 1023:{RejectCode.CannotReact} · 1045:{RejectCode.CannotReact}", $"2002 판 전체 카드에는 거절 9 ({codes})");
                    }
                    {
                        // 반사가 무효되면 — 알아: 내가 그대로 받는다 / 따블: 내가 2배로 받는다
                        var t = new ChainTable();
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                        t.At(2);
                        t.Counter(t.O1, t.Give(t.O1, Real(2002)));
                        t.At(Second);
                        t.Counter(t.O2, t.Give(t.O2, Real(2014)));
                        t.Run(20);
                        var s = new ChainTable();
                        s.PlayCard(s.Turn, s.Give(s.Turn, Real(1001)), s.O1);
                        s.At(2);
                        s.Counter(s.O1, s.Give(s.O1, Real(2002)));
                        s.At(Second);
                        s.Counter(s.O2, s.Give(s.O2, Real(2005)));
                        s.Run(20);
                        Check(t.Hp(t.O1) == 80 && t.Hp(t.Turn) == 100, $"2002 가 알아로 무효되면 내가 받는다 (♥{t.Hp(t.O1)} · 쓴 사람 ♥{t.Hp(t.Turn)})");
                        Check(s.Hp(s.O1) == 60 && s.Hp(s.Turn) == 100, $"2002 가 따블로 막히면 내가 2배 (♥{s.Hp(s.O1)} · 쓴 사람 ♥{s.Hp(s.Turn)})");
                    }
                    {
                        // 반응 시간 중에 빠진 사람 — 반사한 사람이 빠지면 카드는 무효로만 끝난다 / 쓴 사람이 빠지면 다시 쓴 카드가 불발이다
                        var t = new ChainTable();
                        t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                        t.At(2);
                        List<GameEvent> gone = t.Counter(t.O1, t.Give(t.O1, Real(2002))).ToList();
                        gone.AddRange(t.Server.RemovePlayer(t.O1));
                        gone.AddRange(t.Run(20));
                        var s = new ChainTable();
                        s.PlayCard(s.Turn, s.Give(s.Turn, Real(1017)));
                        s.At(2);
                        s.Counter(s.O1, s.Give(s.O1, Real(2002)));
                        s.Server.RemovePlayer(s.Turn);
                        s.Run(20);
                        Check(gone.OfType<CardNegated>().Any(e => e.CardId == 1001) && !gone.OfType<CardResolved>().Any(e => e.CardId == 1001) && t.Hp(t.Turn) == 100 && t.Hp(t.O2) == 100,
                            $"2002 반사한 사람이 빠지면 — 카드는 무효로만 끝난다 (쓴 사람 ♥{t.Hp(t.Turn)})");
                        Check(s.Hp(s.O1) == 100 && s.Hp(s.O2) == 60, $"2002 + 1017, 쓴 사람이 빠지면 — 남은 사람만 받는다 (나 ♥{s.Hp(s.O1)} · O2 ♥{s.Hp(s.O2)})");
                    }
                }

                // ── 카운터 묶음 4 (11-card-list 1-3) — 2019 안 놔! ──
                {
                    // 빼앗기 1장 (1002) — 내 몫을 막고 1장 뽑는다. 손패가 안 주는 사람 · 내 카드에는 못 낸다
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 3);
                    Card hold = t.Give(t.O1, Real(2019));
                    Card other = t.Give(t.O2, Real(2019));
                    Card mine = t.Give(t.Turn, Real(2019));
                    Card steal = t.Give(t.Turn, Real(1002));
                    List<Card> kept = t.Hand(t.O1).Where(c => !c.Equals(hold)).ToList();
                    int turnHand = t.HandCount(t.Turn) - 1;
                    t.PlayCard(t.Turn, steal, t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O2, other)) == RejectCode.CannotReact && t.Code(t.Counter(t.Turn, mine)) == RejectCode.CannotReact, "2019 내 손패가 안 주는 사람 · 내 카드에는 거절 9");
                    t.Counter(t.O1, hold);
                    var events = t.Run(20);
                    Check(kept.All(c => t.Hand(t.O1).Contains(c)) && t.HandCount(t.O1) == 4 && t.HandCount(t.Turn) == turnHand && events.OfType<CardDrawn>().Count(e => e.Actor == t.O1) == 1,
                        $"2019 안 놔! — 1002 빼앗기를 막고 1장 뽑음 (손패 {t.HandCount(t.O1)} · 쓴 사람 {t.HandCount(t.Turn)})");
                }
                {
                    // 기억 삭제 (1013) — 버리기 · 다시 뽑기 모두에서 빠지고 손패 장수만큼(최대 3) 뽑는다 (사용자 2026-11-23)
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 5);
                    Card hold = t.Give(t.O1, Real(2019));
                    List<Card> kept = t.Hand(t.O1).Where(c => !c.Equals(hold)).ToList();
                    List<Card> o2Old = t.Hand(t.O2).ToList();
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1013)));
                    t.At(2);
                    t.Counter(t.O1, hold);
                    var events = t.Run(20);
                    Check(kept.All(c => t.Hand(t.O1).Contains(c)) && t.HandCount(t.O1) == 8 && events.OfType<CardDrawn>().Count(e => e.Actor == t.O1) == 3,
                        $"2019 1013 기억 삭제 — 손패 5장 그대로 · 다시 뽑기 없음 · 최대 3장만 뽑음 (손패 {t.HandCount(t.O1)})");
                    Check(t.HandCount(t.O2) == 5 && !o2Old.Any(c => t.Hand(t.O2).Contains(c)), $"2019 다른 사람은 그대로 받는다 — 전부 버리고 5장 (손패 {t.HandCount(t.O2)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 2);
                    Card hold = t.Give(t.O1, Real(2019));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1013)));
                    t.At(2);
                    t.Counter(t.O1, hold);
                    var events = t.Run(20);
                    Check(t.HandCount(t.O1) == 4 && events.OfType<CardDrawn>().Count(e => e.Actor == t.O1) == 2, $"2019 1013 — 손패 2장이면 2장 뽑음 (손패 {t.HandCount(t.O1)})");
                }
                {
                    // 전원 빼앗기 (1014) — 나만 빠진다
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 3);
                    Card hold = t.Give(t.O1, Real(2019));
                    int turnHand = t.HandCount(t.Turn), o2Hand = t.HandCount(t.O2);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1014)));
                    t.At(2);
                    t.Counter(t.O1, hold);
                    t.Run(20);
                    Check(t.HandCount(t.O1) == 4 && t.HandCount(t.O2) == o2Hand - 1 && t.HandCount(t.Turn) == turnHand + 1,
                        $"2019 1014 군침이 싹 도노 — 나만 빠짐 (나 {t.HandCount(t.O1)} · 다른 대상 {t.HandCount(t.O2)} · 쓴 사람 {t.HandCount(t.Turn)})");
                }
                {
                    // 피해 + 빼앗기 (1015) — 피해는 받고 빼앗기만 막는다
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 3);
                    Card hold = t.Give(t.O1, Real(2019));
                    int turnHand = t.HandCount(t.Turn);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, hold);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 90 && t.HandCount(t.O1) == 4 && t.HandCount(t.Turn) == turnHand, $"2019 1015 팍씨 — 피해 10 은 받고 빼앗기만 막음 (♥{t.Hp(t.O1)} · 손패 {t.HandCount(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    Card hold = t.Give(t.O1, Real(2019));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, hold)) == RejectCode.CannotReact, "2019 1019 목표 조준(대상이 고르는 선택지)에는 거절 9");
                }
                {
                    var t = new ChainTable();
                    Card hold = t.Give(t.O1, Real(2019));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, hold)) == RejectCode.CannotReact, "2019 손패가 안 주는 카드(1001)에는 거절 9");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 0);
                    Card hold = t.Give(t.O1, Real(2019));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1014)));
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, hold)) == RejectCode.CannotReact, "2019 안 놔! 말고 손패가 0장이면 거절 9");
                }
                {
                    // 무야호 (1018) — 내가 손패 최다라 나눠 주는 쪽. 막으면 아무도 못 받는다 (사용자 2026-11-23)
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 7);
                    Card hold = t.Give(t.O1, Real(2019));
                    int turnHand = t.HandCount(t.Turn), o2Hand = t.HandCount(t.O2);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1018)));
                    t.At(2);
                    t.Counter(t.O1, hold);
                    var events = t.Run(20);
                    Check(t.HandCount(t.O1) == 9 && t.HandCount(t.Turn) == turnHand && t.HandCount(t.O2) == o2Hand && !events.OfType<HandCardsMoved>().Any(),
                        $"2019 1018 무야호 — 나눠 줄 2장을 막고 2장 뽑음 · 아무도 못 받음 (나 {t.HandCount(t.O1)} · {t.HandCount(t.Turn)} · {t.HandCount(t.O2)})");
                }
                {
                    // 으르렁의 벌칙에 — 벌칙만 막힌다. 막기 · 대가는 그대로 (11-card-list 1-3 2021 예외)
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 4);
                    t.SetHandSize(t.O1, 2);
                    Card growl = t.Give(t.O1, Real(2021));
                    Card hold = t.Give(t.Turn, Real(2019));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, growl);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    bool played = t.Code(t.Counter(t.Turn, hold)) == 0;
                    ChoiceStarted asked = t.RunUntil<ChoiceStarted>(30);
                    Check(played && asked != null && asked.Actor == t.O1 && t.HandCount(t.Turn) == 6 && t.Hp(t.O1) == 100,
                        $"2019 으르렁 벌칙에 안 놔! — 벌칙 2장을 막고 2장 뽑음 · 막기(♥{t.Hp(t.O1)}) · 대가 고르기는 그대로 (쓴 사람 손패 {t.HandCount(t.Turn)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 1);
                    t.SetHandSize(t.O1, 2);
                    Card growl = t.Give(t.O1, Real(2021));
                    Card hold = t.Give(t.Turn, Real(2019));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, growl);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    t.Counter(t.Turn, hold);
                    t.RunUntil<ChoiceStarted>(30);
                    Check(t.HandCount(t.Turn) == 2, $"2019 으르렁 벌칙 — 손패 1장이면 1장만 뽑음 (손패 {t.HandCount(t.Turn)})");
                }
                {
                    // 알아로 무효 — 막기도 뽑기도 없다
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 3);
                    Card hold = t.Give(t.O1, Real(2019));
                    Card know = t.Give(t.Turn, Real(2014));
                    int turnHand = t.HandCount(t.Turn);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1002)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, hold);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    t.Counter(t.Turn, know);
                    var events = t.Run(30);
                    Check(t.HandCount(t.O1) == 2 && t.HandCount(t.Turn) == turnHand && !events.OfType<CardDrawn>().Any(e => e.Actor == t.O1),
                        $"2019 안 놔! 가 무효되면 막기 · 뽑기 없음 — 1002 적용 (손패 {t.HandCount(t.O1)} · 쓴 사람 {t.HandCount(t.Turn)})");
                }
                // ── 카운터 묶음 3 (11-card-list 1-3) — 2021 으르렁 ──
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 5); // + 낼 카드 → 낸 뒤 5장
                    t.SetHandSize(t.O1, 3);   // + 으르렁 → 낸 뒤 3장
                    Card growl = t.Give(t.O1, Real(2021));
                    Card other = t.Give(t.O2, Real(2021));
                    Card hit = t.Give(t.Turn, Real(1001));
                    t.PlayCard(t.Turn, hit, t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O2, other)) == RejectCode.CannotReact, "2021 나를 노리지 않은 카드에는 거절 9");
                    t.Counter(t.O1, growl);
                    ChoiceStarted asked = t.RunUntil<ChoiceStarted>(30);
                    Check(asked != null && asked.Actor == t.O1 && asked.Kind == ChoiceKind.HandCard && asked.HandCount == 3 && t.HandCount(t.Turn) == 3 && t.Hp(t.O1) == 100,
                        $"2021 으르렁 — 벌칙(쓴 사람 무작위 2장 버림, 손패 {t.HandCount(t.Turn)}) 뒤 대가 고르기가 열림 · 그동안 멈춤");
                    int keepA = t.Hand(t.O1)[0].InstanceId, drop = t.Hand(t.O1)[1].InstanceId;
                    Check(t.Code(t.Server.Choose(t.Turn, drop)) == RejectCode.NotChoosing && t.Code(t.Server.Choose(t.O1, hit.InstanceId)) == RejectCode.NotChoosing, "2021 남의 고르기 · 내 손패에 없는 카드 → 거절 20");
                    var chosen = t.Server.Choose(t.O1, drop).ToList();
                    chosen.AddRange(t.Run(t.Now + 10));
                    Check(chosen.OfType<ChoiceMade>().Any(c => c.Actor == t.O1 && c.Kind == ChoiceKind.HandCard && c.Option == 0 && !c.TimedOut)
                          && chosen.OfType<CardsDiscarded>().Any(d => d.Actor == t.O1 && d.CardInstanceIds.SequenceEqual(new[] { drop }))
                          && t.HandCount(t.O1) == 2 && t.Hand(t.O1).Any(c => c.InstanceId == keepA) && t.Hp(t.O1) == 100 && !t.Hand(t.Turn).Contains(hit) && chosen.OfType<TurnStarted>().Any(),
                        "2021 고른 1장을 버림 · 1001 무효(♥100 · 버림 더미로) · 처리를 마치고 다음 턴");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 1); // 낸 뒤 1장 — 2장 미만이면 전부
                    t.SetHandSize(t.O1, 2);
                    Card growl = t.Give(t.O1, Real(2021));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, growl);
                    bool asked = t.RunUntil<ChoiceStarted>(30) != null;
                    var events = t.Run(t.Now + 6); // 5초 안에 안 고름 → 무작위
                    Check(asked && t.HandCount(t.Turn) == 0 && t.HandCount(t.O1) == 1 && events.OfType<ChoiceMade>().Any(c => c.Actor == t.O1 && c.TimedOut && c.Kind == ChoiceKind.HandCard),
                        $"2021 쓴 사람 손패 1장이면 전부 버림 · 대가를 안 고르면 5초 뒤 무작위 1장 (손패 {t.HandCount(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 0);
                    Card growl = t.Give(t.O1, Real(2021));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, growl);
                    var events = t.Run(20);
                    Check(!events.OfType<ChoiceStarted>().Any() && t.Hp(t.O1) == 100 && events.OfType<TurnStarted>().Any(), "2021 내 손패 0장이면 대가 없이 처리");
                }
                {
                    var t = new ChainTable();
                    Card growl = t.Give(t.O1, Real(2021));
                    Card know = t.Give(t.Turn, Real(2014));
                    int turnHand = t.HandCount(t.Turn), o1Hand = t.HandCount(t.O1) - 1;
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, growl);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    t.Counter(t.Turn, know);
                    var events = t.Run(30);
                    Check(!events.OfType<ChoiceStarted>().Any() && t.Hp(t.O1) == 80 && t.HandCount(t.Turn) == turnHand - 1 && t.HandCount(t.O1) == o1Hand, "2021 으르렁이 무효되면 막기 · 벌칙 · 대가 없음 (♥80)");
                }
                {
                    var t = new ChainTable();
                    int[] aimed = { 1001, 1002, 1014, 1013, 1017 }, wide = { 1012, 1021, 1023, 1045 };
                    bool Wide(int cardId) => mainCards.First(c => c.CardId == cardId).Effects.Any(e => (bool)typeof(CardEffect).GetProperty("BoardWide", Private).GetValue(e));
                    Check(wide.All(Wide) && !aimed.Any(Wide), "2021 판 전체 상태 카드 = 1012 대소동 · 1021 개판 오분전 · 1023 기다려! · 1045 뒤로 돌아 (사용자 2026-11-21)");
                    Card growl = t.Give(t.O1, Real(2021));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1045)));
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, growl)) == RejectCode.CannotReact, "2021 판 전체 상태 카드(1045 뒤로 돌아)에는 거절 9");
                }
                {
                    // 전원 대상 카드 — 카드 전체가 무효
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 1);
                    Card growl = t.Give(t.O1, Real(2021));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)));
                    t.At(2);
                    t.Counter(t.O1, growl);
                    t.RunUntil<ChoiceStarted>(30);
                    t.Server.Choose(t.O1, t.Hand(t.O1)[0].InstanceId);
                    t.Run(t.Now + 10);
                    Check(t.Hp(t.O1) == 100 && t.Hp(t.O2) == 100, $"2021 전원 대상 카드(1017)도 전체 무효 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    // 봇 — 대가로 버릴 카드: 함정 → 카운터 → 행동 순으로 있는 종류의 뒤쪽 카드 (18-bot 7-4)
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 0);
                    Card a = t.Give(t.O1, Real(1001)), c1 = t.Give(t.O1, Real(2003)), c2 = t.Give(t.O1, Real(2016)), b = t.Give(t.O1, Real(1002));
                    var pick = new ChooseDiscardEffect();
                    int picked = (int)typeof(ChooseDiscardEffect).GetMethod("BotPick", Private).Invoke(pick, new object[] { t.Server, t.O1 });
                    Check(picked == c2.InstanceId, "2021 봇이 대가로 버릴 카드 — 카운터(뒤쪽) 먼저");
                }
                // ── 카운터 묶음 3 (11-card-list 1-3) — 2020 내 거야! ──
                {
                    var t = new ChainTable();
                    Card grab = t.Give(t.O2, Real(2020)); // 대상이 아닌 사람도 낼 수 있다
                    Card steal = t.Give(t.Turn, Real(1002));
                    int o1Hand = t.HandCount(t.O1), turnHand = t.HandCount(t.Turn) - 1, o2Hand = t.HandCount(t.O2);
                    t.PlayCard(t.Turn, steal, t.O1);
                    t.At(2);
                    t.Counter(t.O2, grab);
                    var events = t.Run(20);
                    Check(t.HandCount(t.O1) == o1Hand && t.HandCount(t.Turn) == turnHand && t.Hand(t.O2).Contains(steal) && t.HandCount(t.O2) == o2Hand
                          && events.OfType<CardReturnedToHand>().Any(e => e.Actor == t.O2 && e.CardInstanceId == steal.InstanceId),
                        "2020 내 거야! — 1002 무효(아무도 안 빼앗김) · 막은 카드는 낸 사람 손패로 (낸 1장 + 받은 1장)");
                    Check(t.Hp(t.O2) == 80, $"2020 대가 20 피해 (♥{t.Hp(t.O2)})");
                }
                {
                    var t = new ChainTable();
                    Card grab = t.Give(t.O1, Real(2020));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, grab)) == RejectCode.CannotReact, "2020 손패 + 가 없는 카드(1001)에는 거절 9");
                }
                {
                    var t = new ChainTable();
                    int[] gain = { 1002, 1009, 1013, 1014, 1015, 1016 }, none = { 1001, 1004, 1005, 1006, 1011, 1017, 1018, 1019, 1020, 1045 };
                    bool Gains(int cardId) => mainCards.First(c => c.CardId == cardId).Effects.Any(e => (bool)typeof(CardEffect).GetProperty("GainsHand", Private).GetValue(e));
                    Check(gain.All(Gains) && !none.Any(Gains), "2020 「손패 +」 카드 = 1002 · 1009 · 1013 · 1014 · 1015 · 1016 (11-card-list 5-3) — 1018 무야호(손패 −)는 아니다");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 20);
                    Card grab = t.Give(t.O1, Real(2020));
                    Card know = t.Give(t.Turn, Real(2014));
                    Card steal = t.Give(t.Turn, Real(1002));
                    int o1Hand = t.HandCount(t.O1) - 1;
                    t.PlayCard(t.Turn, steal, t.O1);
                    t.At(2);
                    t.Counter(t.O1, grab);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    t.Counter(t.Turn, know);
                    t.Run(30);
                    Check(t.Hp(t.O1) == 20 && !t.Hand(t.O1).Contains(steal) && t.HandCount(t.O1) == o1Hand - 1, $"2020 내 거야! 가 무효되면 가져오기 · 대가 없음 · 1002 적용 (♥{t.Hp(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 20);
                    Card grab = t.Give(t.O1, Real(2020));
                    Card steal = t.Give(t.Turn, Real(1002));
                    t.PlayCard(t.Turn, steal, t.O1);
                    t.At(2);
                    t.Counter(t.O1, grab);
                    var events = t.Run(2 + ChainTable.Reveal + 8);
                    Check(t.Hp(t.O1) == 0 && t.State(t.O1).Life == LifeState.DeathPending && events.OfType<CardReturnedToHand>().Any(e => e.Actor == t.O1), "2020 대가로 HP 0 → 사망 대기 · 막은 카드는 받는다");
                }
                // ── 카운터 묶음 2 (11-card-list 1-3) — 2001 침착 ──
                {
                    var t = new ChainTable();
                    t.SetHp(t.O2, 95);
                    Card calm = t.Give(t.O2, Real(2001)); // 대상이 아닌 사람도 낼 수 있다
                    Card mine = t.Give(t.Turn, Real(2001));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.Turn, mine)) == RejectCode.CannotReact, "2001 내 카드에는 거절 9");
                    t.Counter(t.O2, calm);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 100 && t.Hp(t.O2) == 100, $"2001 침착 — 20 − 20 = 0 피해 · 낸 사람 95 → 100 (최대 100) (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 50);
                    Card calm = t.Give(t.O1, Real(2001));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, calm);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 40, $"2001 침착 — 40 − 20 = 20 피해 뒤 10 회복 (50 → 40) (♥{t.Hp(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 10);
                    Card calm = t.Give(t.O1, Real(2001));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, calm);
                    var events = t.Run(20);
                    Check(t.Hp(t.O1) == 0 && events.Any(e => e.GetType().Name.Contains("DeathPending")) && !events.OfType<HpChanged>().Any(e => e.Actor == t.O1 && e.Hp > 0),
                        $"2001 침착 — HP 10 · 20 피해 → 0 이면 회복 없이 사망 대기 (♥{t.Hp(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 21);
                    Card calm = t.Give(t.O1, Real(2001));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1017)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, calm);
                    var events = t.Run(20);
                    Check(t.Hp(t.O1) == 11 && !events.Any(e => e.GetType().Name.Contains("DeathPending")), $"2001 침착 — HP 21 · 20 피해 → 1 → 10 회복 (♥{t.Hp(t.O1)})");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O2, 50);
                    Card calm = t.Give(t.O2, Real(2001));
                    Card know = t.Give(t.Turn, Real(2014));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    t.Counter(t.O2, calm);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    t.Counter(t.Turn, know);
                    t.Run(30);
                    Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 50, $"2001 침착이 무효되면 감소 · 회복 없음 (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                {
                    var t = new ChainTable();
                    Card calm = t.Give(t.O1, Real(2001));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1002)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, calm)) == RejectCode.CannotReact, "2001 피해 없는 카드(1002)에는 거절 9");
                }
                {
                    // 고르기로 멈췄다 이어 가는 카드 — 고른 피해도 줄고, 회복은 그 카드가 끝난 뒤
                    var t = new ChainTable();
                    Card calm = t.Give(t.O1, Real(2001));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, calm);
                    bool asked = t.RunUntil<ChoiceStarted>(30) != null;
                    int paused = t.Hp(t.O1);
                    t.Server.Choose(t.O1, 1);
                    t.Run(t.Now + 1);
                    Check(asked && paused == 100 && t.Hp(t.O1) == 50, $"2001 침착 — 1019 고르는 동안 회복 없음(♥{paused}) · 80 − 20 = 60 피해 뒤 10 회복 (♥{t.Hp(t.O1)})");
                }
                {
                    // 받는 사람 모두 — 전원 피해 메모리 카드에 실제 침착
                    var t = new ChainTable();
                    t.SetHp(t.O1, 90);
                    Card calm = t.Give(t.O1, Real(2001));
                    Card wave = t.Give(t.Turn, id => new Card(id, 902, CardType.Action, TargetType.AllOthers, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 30 } }, null, ChainTable.Reveal, ChainTable.Resolve));
                    t.PlayCard(t.Turn, wave);
                    t.At(2);
                    t.Counter(t.O1, calm);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 90 && t.Hp(t.O2) == 90, $"2001 침착 — 전원 30 피해를 받는 사람 모두 20 씩 감소 (낸 사람 90 − 10 + 10 · 다른 사람 100 − 10) (♥{t.Hp(t.O1)} · ♥{t.Hp(t.O2)})");
                }
                // ── 카운터 묶음 1 (11-card-list 1-3) — 2003 진짜 할거야? · 2014 알아 · 2016 칼집 ──
                {
                    var t = new ChainTable();
                    Card c2003 = t.Give(t.O2, Real(2003)); // 대상이 아닌 사람도 낼 수 있다 (조건 없음)
                    Card mine = t.Give(t.Turn, Real(2003));
                    Card hit = t.Give(t.Turn, Real(1001));
                    t.PlayCard(t.Turn, hit, t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.Turn, mine)) == RejectCode.CannotReact, "2003 내 카드에는 카운터 거절 9 (04 7절 사용 조건 5)");
                    t.Counter(t.O2, c2003);
                    var events = t.Run(20);
                    Check(t.Hp(t.O1) == 100 && t.Hand(t.Turn).Contains(hit) && events.OfType<CardReturnedToHand>().Any(e => e.CardInstanceId == hit.InstanceId),
                        "2003 진짜 할거야? — 행동 카드 무효 · 쓴 사람 손패로");
                }
                {
                    var t = new ChainTable();
                    Card c2003 = t.Give(t.O1, Real(2003));
                    Card c2014 = t.Give(t.Turn, Real(2014));
                    Card late = t.Give(t.O1, Real(2014));
                    Card hit = t.Give(t.Turn, Real(1001));
                    t.PlayCard(t.Turn, hit, t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O1, late)) == RejectCode.CannotReact, "2014 행동 카드에는 거절 9 (카운터에만 반응)");
                    t.Counter(t.O1, c2003);
                    t.At(2 + ChainTable.Reveal + 0.1);
                    IReadOnlyList<GameEvent> played = t.Counter(t.Turn, c2014);
                    Check(!(played[0] is RequestRejected) && !played.OfType<CountersUsable>().Any(), "2014 알아 — 카운터에 냄 · 아무에게도 묻지 않는다 (막을 수 없음)");
                    t.At(2 + 2 * ChainTable.Reveal + 0.2);
                    int code = t.Code(t.Counter(t.O1, late));
                    Check(code == RejectCode.CannotReact || code == RejectCode.NotReactionTime, $"2014 알아 위에는 카운터 거절 ({code})");
                    t.Run(30);
                    Check(t.Hp(t.O1) == 80 && !t.Hand(t.Turn).Contains(hit), "2014 알아 — 2003 무효 → 1001 적용 (♥80) · 반환 없음");
                }
                {
                    var t = new ChainTable();
                    Card c2016 = t.Give(t.O1, Real(2016));
                    Card other = t.Give(t.O2, Real(2016));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    t.At(2);
                    Check(t.Code(t.Counter(t.O2, other)) == RejectCode.CannotReact, "2016 피해를 받지 않는 사람은 거절 9");
                    int before = t.HandCount(t.O1);
                    t.Counter(t.O1, c2016);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 100 && t.HandCount(t.O1) == before, "2016 칼집 — 20 − 40 = 0 피해 · 1장 뽑기 (낸 1장 + 뽑은 1장)");
                }
                {
                    var t = new ChainTable();
                    Card c2016 = t.Give(t.O1, Real(2016));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    t.At(2);
                    t.Counter(t.O1, c2016);
                    t.Run(20);
                    Check(t.Hp(t.O1) == 91, $"2016 칼집 — 1005 드롭킥 49 − 40 = 9 (♥{t.Hp(t.O1)})");
                }

                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1001)), t.O1);
                    Check(t.Hp(t.O1) == 80, "1001 물어뜯기 대상 20 피해");
                }
                {
                    var t = new ChainTable();
                    int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1002)), t.O1);
                    Check(t.HandCount(t.Turn) == mine + 1 && t.HandCount(t.O1) == theirs - 1, "1002 낚아채기 대상 1장 → 나");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 0);
                    Card c = t.Give(t.Turn, Real(1002));
                    Check(t.Code(t.PlayCard(t.Turn, c, t.O1)) == RejectCode.InvalidTarget && t.Hand(t.Turn).Contains(c), "1002 손패 0장 대상 → 거절 5, 카드는 손패에");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.Turn, 50);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1006)), t.Turn);
                    Check(t.Hp(t.Turn) == 70, "1006 핥아주기 나에게 (OneAny) 50 → 70");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 60);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1006)), t.O1);
                    Check(t.Hp(t.O1) == 80, "1006 핥아주기 남에게 60 → 80");
                }
                {
                    var t = new ChainTable();
                    Card c = t.Give(t.Turn, Real(1006));
                    Check(t.Code(t.PlayCard(t.Turn, c, t.Turn)) == RejectCode.InvalidTarget, "1006 체력이 가득 찬 사람 → 거절 5");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O2, 0);
                    int mine = t.HandCount(t.Turn), o1 = t.HandCount(t.O1);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1014)));
                    Check(t.HandCount(t.Turn) == mine + 1 && t.HandCount(t.O1) == o1 - 1 && t.HandCount(t.O2) == 0, "1014 군침이 싹 도노 나를 뺀 모두에게서 1장씩 (빈 손패는 건너뜀)");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1017)));
                    Check(t.Hp(t.O1) == 60 && t.Hp(t.O2) == 60 && t.Hp(t.Turn) == 100, "1017 지옥참마도 상대 모두에게 40 피해");
                }

                // ── 묶음 2 ──
                {
                    var t = new ChainTable();
                    t.GiveTrap(t.O1, ChainTable.TrapHit);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1004)), t.O1);
                    var t2 = new ChainTable(); // 메인 행동은 턴에 한 번 — 거절은 새 판에서
                    Card c = t2.Give(t2.Turn, Real(1004));
                    int left = t.TrapCount(t.O1), code = t2.Code(t2.Server.PlayCard(t2.Turn, c.InstanceId, new[] { t2.O2 }));
                    Check(left == 0 && code == RejectCode.InvalidTarget, $"1004 파헤치기 대상 함정 1장 파괴 · 함정 없는 사람 → 거절 5 (함정 {left} · 코드 {code})");
                }
                {
                    var t = new ChainTable();
                    Card old = t.PutInDiscard(ChainTable.Hit);
                    int hand = t.HandCount(t.Turn);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1009)));
                    Check(t.Hand(t.Turn).Contains(old) && t.HandCount(t.Turn) == hand + 1 && t.Discard.Count == 1 && t.Discard[0].CardId == 1009
                          && events.OfType<DiscardTaken>().Any(e => e.CardInstanceId == old.InstanceId && e.TopCardId == 0),
                        "1009 아 맞다! 체인 전 버림 더미 맨 위를 손패로 · 이 카드는 체인이 끝난 뒤 쌓인다");
                }
                {
                    var t = new ChainTable();
                    t.Discard.Clear();
                    int hand = t.HandCount(t.Turn);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1009)));
                    Check(t.HandCount(t.Turn) == hand && t.Discard.Count == 1 && !events.OfType<DiscardTaken>().Any(), "1009 버림 더미가 비면 불발 (자기 자신은 못 가져옴)");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.Turn, 30);
                    t.SetHp(t.O1, 90);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1011)), t.O1);
                    Check(t.Hp(t.Turn) == 90 && t.Hp(t.O1) == 30, "1011 운명 뒤집기 HP 맞바꿈 30 ↔ 90");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 9);
                    t.SetHandSize(t.O2, 1);
                    int mine = t.HandCount(t.Turn);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1013)));
                    Check(t.HandCount(t.O1) == 5 && t.HandCount(t.O2) == 5 && t.HandCount(t.Turn) == mine, "1013 기억 삭제 나를 뺀 모두 손패 버리고 5장 (나는 그대로)");
                }
                {
                    var t = new ChainTable();
                    int hand = t.HandCount(t.Turn);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1016)));
                    int drew = t.HandCount(t.Turn) - hand;
                    Check(drew >= 1 && drew <= 10, $"1016 상상도 못한 정체 1 ~ 10장 뽑음 ({drew}장)");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 4);
                    t.SetHandSize(t.O1, 8);
                    t.SetHandSize(t.O2, 2);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1018)));
                    bool targeted = events.OfType<CardPlayed>().Any(e => e.CardId == 1018 && e.Targets.SequenceEqual(new[] { t.O1 }));
                    Check(targeted && t.HandCount(t.O1) == 6 && t.HandCount(t.Turn) == 5 && t.HandCount(t.O2) == 3, "1018 무야호 손패 최다(자동 공개) 1명에게서 나머지가 1장씩");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.Turn, 0);
                    t.SetHandSize(t.O1, 1);
                    t.SetHandSize(t.O2, 0);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1018)));
                    Check(t.HandCount(t.Turn) == 1 && t.HandCount(t.O1) == 0 && t.HandCount(t.O2) == 0, "1018 카드가 모자라면 쓴 사람부터 턴 순서대로 · 떨어지면 끝");
                }
                {
                    var t = new ChainTable();
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1045)));
                    events.AddRange(t.Run(t.Now + 3));
                    TurnStarted next = events.OfType<TurnStarted>().FirstOrDefault();
                    Check(events.OfType<TurnDirectionChanged>().Any(e => e.Reversed && e.ExtraTurn == -1) && next != null && next.Actor == t.O2,
                        $"1045 뒤로 돌아 3인 — 역방향, 다음 턴은 이전 사람 (P{next?.Actor} · 기대 P{t.O2})");
                }
                {
                    var t = new ChainTable();
                    t.State(t.O2).Life = LifeState.Dead;
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1045)));
                    events.AddRange(t.Run(t.Now + 3));
                    TurnStarted next = events.OfType<TurnStarted>().FirstOrDefault();
                    Check(events.OfType<TurnDirectionChanged>().Any(e => e.ExtraTurn == t.Turn) && next != null && next.Actor == t.Turn,
                        "1045 생존자 2명 — 다음 사람을 건너뛰어 내 턴이 다시 (03-turn 7절)");
                }

                // ── 묶음 3 — 턴 · 상태 (1007 · 1020 · 1023). 「다음 턴」 은 실제로 오는 턴 — 스킵된 턴은 세지 않는다 (03-turn 7절, 2026-11-13) ──
                {
                    var t = new ChainTable();
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1007)), t.O1);
                    bool marked = events.OfType<ActionBlockChanged>().Any(e => e.Actor == t.O1 && e.State == ActionBlock.NextTurn);
                    events = t.Run(t.Now + 3);
                    bool blocked = t.Server.CurrentTurnActor == t.O1 && t.State(t.O1).ActionBlocked
                                   && events.OfType<ActionBlockChanged>().Any(e => e.Actor == t.O1 && e.State == ActionBlock.ThisTurn);
                    Card hit = t.Give(t.O1, Real(1001));
                    int code = t.Code(t.PlayCard(t.O1, hit, t.O2));
                    Check(marked && blocked && code == RejectCode.ActionBlocked && t.Hand(t.O1).Contains(hit),
                        $"1007 짖기 — 대상의 다음 턴에 행동 카드 → 거절 19, 카드는 손패에 (코드 {code})");
                    var drawn = t.Server.Draw(t.O1);
                    Check(!(drawn[0] is RequestRejected) && !t.State(t.O1).ActionBlocked && drawn.OfType<ActionBlockChanged>().Any(e => e.Actor == t.O1 && e.State == ActionBlock.None),
                        "1007 금지된 턴에도 뽑기는 된다 · 그 턴이 끝나면 해제");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1007)), t.O1);
                    t.State(t.O1).SkipNextTurn = true; // 1020 얼음! 이 함께 걸린 것과 같다
                    t.Run(t.Now + 3);
                    bool waitingOnSkip = t.Server.CurrentTurnActor == t.O2 && t.State(t.O1).BlockActionNextTurn && !t.State(t.O1).ActionBlocked;
                    t.Server.Draw(t.O2);
                    t.Server.Draw(t.Turn);
                    Check(waitingOnSkip && t.Server.CurrentTurnActor == t.O1 && t.State(t.O1).ActionBlocked,
                        "1007 + 턴 스킵 — 금지는 건너뛴 턴이 아니라 실제로 오는 다음 턴에");
                }
                {
                    var t = new ChainTable();
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1020)));
                    bool target = events.OfType<CardPlayed>().Any(e => e.CardId == 1020 && e.Targets.SequenceEqual(new[] { t.O1 }));
                    events.AddRange(t.Run(t.Now + 3));
                    TurnStarted next = events.OfType<TurnStarted>().FirstOrDefault();
                    Check(target && events.OfType<TurnSkipped>().Any(e => e.Actor == t.O1) && next != null && next.Actor == t.O2,
                        $"1020 얼음! 다음 차례(자동) 1명의 턴을 건너뛴다 (다음 P{next?.Actor} · 기대 P{t.O2})");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1045)));
                    t.Run(t.Now + 3); // 역방향 — 이제 O2 의 턴, 그다음은 O1 (정방향이면 Turn)
                    var events = t.PlayAndResolve(t.O2, t.Give(t.O2, Real(1020)));
                    Check(events.OfType<CardPlayed>().Any(e => e.CardId == 1020 && e.Targets.SequenceEqual(new[] { t.O1 })),
                        "1020 역방향이면 반대쪽 다음 사람이 대상");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, GameServer.ChapchuHandCount);
                    t.State(t.O1).IsChapchu = true;
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1023)));
                    bool started = events.OfType<WaitChanged>().Any(e => e.Casters.SequenceEqual(new[] { t.Turn }));
                    events = t.Run(t.Now + 3);
                    Check(started && !t.Server.IsGameOver && events.OfType<ChapchuWinHeld>().Any(e => e.Actor == t.O1) && t.Server.CurrentTurnActor == t.O1 && t.State(t.O1).IsChapchu,
                        "1023 기다려! — 찹츄 상태로 턴이 와도 이기지 않는다 · 찹츄는 그대로");
                    events = t.Run(t.Now + 31); // O1 시간 초과 → O2
                    events.AddRange(t.Run(t.Now + 31)); // O2 시간 초과 → 건 사람
                    int endAt = events.FindIndex(e => e is WaitChanged w && w.Casters.Length == 0);
                    int turnAt = events.FindIndex(e => e is TurnStarted ts && ts.Actor == t.Turn);
                    Check(endAt >= 0 && turnAt > endAt, "1023 건 사람의 다음 턴이 시작되기 전에 끝난다");
                    GameEnded ended = t.Run(t.Now + 31).OfType<GameEnded>().FirstOrDefault();
                    Check(ended != null && ended.Winner == t.O1 && ended.Reason == GameEndReason.Chapchu, "1023 끝난 뒤 다음 자기 턴에 다시 판정 → 찹츄 승리");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1023)));
                    t.State(t.Turn).SkipNextTurn = true;
                    var events = t.Run(t.Now + 3);
                    t.Server.Draw(t.O1);
                    events.AddRange(t.Server.Draw(t.O2)); // 건 사람의 턴은 스킵 → 다시 O1
                    Check(events.OfType<TurnSkipped>().Any(e => e.Actor == t.Turn) && t.Server.CurrentTurnActor == t.O1 && !events.OfType<WaitChanged>().Any(),
                        "1023 건 사람의 턴이 스킵되면 기다려는 실제로 오는 다음 턴까지 이어진다");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1023)));
                    t.Run(t.Now + 3);
                    t.SetHp(t.Turn, 10);
                    var events = t.PlayAndResolve(t.O1, t.Give(t.O1, Real(1001)), t.Turn);
                    events.AddRange(t.Run(t.Now + 10)); // 사망 대기 5초 (06 8절)
                    int died = events.FindIndex(e => e is PlayerDied d && d.Actor == t.Turn);
                    int ended = events.FindIndex(e => e is WaitChanged w && w.Casters.Length == 0);
                    Check(died >= 0 && ended > died, "1023 건 사람이 최종 사망하면 즉시 끝");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1023)));
                    Check(t.Server.RemovePlayer(t.Turn).OfType<WaitChanged>().Any(e => e.Casters.Length == 0), "1023 건 사람이 나가면 즉시 끝");
                }
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.State(t.Turn).ActionBlocked = true;
                    t.SetHandSize(t.Turn, 6); // 낼 카드가 있어도 행동 카드 금지 턴이라 뽑기만 남는다
                    var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
                    var events = new List<GameEvent>();
                    for (now = 0; now < BotTurnSeconds; now += 0.1)
                        events.AddRange(driver.Tick());
                    Check(!events.OfType<RequestRejected>().Any() && !events.OfType<CardPlayed>().Any() && events.OfType<CardDrawn>().Any(),
                        "봇 — 행동 카드 금지 턴에는 뽑기만 (거절 없이)");
                }

                // ── 묶음 4 — 1015 팍씨: 피해의 사망 · 함정 처리를 마친 뒤 강탈 (11-card-list 1015 · 06 5절) ──
                Func<int, Card> Guard(ReactCondition condition, params CardEffect[] effects) =>
                    id => new Card(id, 901, CardType.Counter, TargetType.None, effects, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve, TrapTrigger.None, 0, condition);
                {
                    var t = new ChainTable();
                    int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    t.Run(t.Now + 3);
                    Check(t.Hp(t.O1) == 90 && t.HandCount(t.O1) == theirs - 1 && t.HandCount(t.Turn) == mine + 1, "1015 팍씨 — 10 피해 + 무작위 1장 강탈");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 10);
                    int mine = t.HandCount(t.Turn);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    bool together = t.HandCount(t.Turn) == mine + 1 && events.OfType<DeathPending>().Any(e => e.Actor == t.O1);
                    events = t.Run(t.Now + 10);
                    Check(together && events.OfType<PlayerDied>().Any(e => e.Actor == t.O1) && events.OfType<TurnStarted>().Any(),
                        "1015 대상이 HP 0 이 돼도 — 피해와 강탈이 함께 (멈추지 않는다) → 사망 대기 → 최종 사망 · 다음 턴");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 10);
                    t.SetTrapCard(t.O1, id => new Card(id, 902, CardType.Trap, TargetType.Self, new CardEffect[] { new SurviveAtEffect { hp = 10, now = true } }, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve, TrapTrigger.SelfDeathPending, 0));
                    int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    var events = t.Run(t.Now + 30);
                    Check(events.OfType<TrapActivated>().Any(e => e.Actor == t.O1) && t.Hp(t.O1) == 10 && t.HandCount(t.Turn) == mine + 1 && t.HandCount(t.O1) == theirs - 1 && !events.OfType<PlayerDied>().Any(),
                        "1015 HP 0 → 체인 끝에 「본인 사망 대기」 함정 자동 발동 · HP 10 생존 · 강탈은 그대로");
                }
                {
                    var t = new ChainTable();
                    t.SetTrapCard(t.O1, id => new Card(id, 902, CardType.Trap, TargetType.TriggerSource, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 10 } }, null, ChainTable.Reveal, ChainTable.Resolve, TrapTrigger.AfterDamaged, 0));
                    int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    var events = t.Run(t.Now + 30);
                    bool fired = events.OfType<TrapActivated>().Any(e => e.Actor == t.O1);
                    int hitAt = events.FindIndex(e => e is HpChanged h && h.Actor == t.Turn);
                    int stealAt = events.FindIndex(e => e is HandCardsMoved m && m.From == t.O1 && m.To == t.Turn);
                    Check(fired && t.Hp(t.Turn) == 90 && t.HandCount(t.Turn) == mine + 1 && t.HandCount(t.O1) == theirs - 1 && stealAt >= 0 && hitAt > stealAt,
                        $"1015 「피해 받은 후」 함정 — 강탈까지 끝난 뒤 자동 반격 (강탈 {stealAt} · 반격 {hitAt})");
                }
                {
                    var t = new ChainTable();
                    Card guard = t.Give(t.O1, Guard(ReactCondition.DamagesMe, new ReduceDamageEffect { amount = 10 }));
                    int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    t.At(t.Now + 2);
                    bool countered = !(t.Counter(t.O1, guard)[0] is RequestRejected);
                    t.Run(t.Now + 20);
                    Check(countered && t.Hp(t.O1) == 100 && t.HandCount(t.Turn) == mine + 1 && t.HandCount(t.O1) == theirs - 2, "1015 피해만 감소(0) — 강탈은 진행");
                }
                {
                    var t = new ChainTable();
                    Card negate = t.Give(t.O1, Guard(ReactCondition.Any, new NegateEffect()));
                    int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    t.At(t.Now + 2);
                    t.Counter(t.O1, negate);
                    t.Run(t.Now + 20);
                    Check(t.Hp(t.O1) == 100 && t.HandCount(t.Turn) == mine && t.HandCount(t.O1) == theirs - 1, "1015 카드 전체 무효 — 둘 다 미적용");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 0);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1015)), t.O1);
                    events.AddRange(t.Run(t.Now + 3));
                    Check(t.Hp(t.O1) == 90 && t.HandCount(t.O1) == 0 && events.OfType<TurnStarted>().Any(), "1015 대상 손패 없음 — 피해만, 강탈 불발");
                }

                // ── 묶음 5 — 1012 대소동: 새 판 · 피폭 k^n (11-card-list 1012) ──
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 30);
                    t.State(t.O2).Life = LifeState.Dead; // 죽은 사람도 다시 들어온다
                    t.State(t.Turn).IsChapchu = true;
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1012)));
                    events.AddRange(t.Run(t.Now + 3));
                    GameRestarted restarted = events.OfType<GameRestarted>().FirstOrDefault();
                    int[] all = { t.Turn, t.O1, t.O2 };
                    bool fresh = restarted != null && restarted.Count == 1 && restarted.User == t.Turn && restarted.Order.OrderBy(a => a).SequenceEqual(all.OrderBy(a => a))
                                 && all.All(a => t.Hp(a) == 100 && t.HandCount(a) == 5 && t.State(a).Life == LifeState.Alive && !t.State(a).IsChapchu)
                                 && t.Server.CurrentTurnActor == restarted.Order[0]
                                 && events.OfType<FalloutChanged>().Any(f => f.N == 1 && f.Turns == 0)
                                 && !events.OfType<HpChanged>().Any(h => h.Hp < 100 && events.IndexOf(h) > events.IndexOf(restarted));
                    Check(fresh, "1012 대소동 — 쓴 턴이 끝나면 새 판: 죽은 사람 포함 전원 HP 100 · 손패 5 · 찹츄 해제 · 새 순서 첫 사람 · 쓴 턴은 피폭 없음");

                    int first = restarted?.Order[0] ?? t.Turn, second = restarted?.Order[1] ?? t.O1, third = restarted?.Order[2] ?? t.O2;
                    t.State(second).SkipNextTurn = true;
                    events = t.Server.Draw(first).ToList();
                    bool k1 = events.OfType<FalloutChanged>().Any(f => f.Turns == 1) && all.All(a => t.Hp(a) == 99);
                    events = t.Server.Draw(t.Server.CurrentTurnActor).ToList(); // 스킵 뒤 third 의 턴
                    bool k2 = t.Server.CurrentTurnActor != second && events.OfType<FalloutChanged>().Any(f => f.Turns == 2) && all.All(a => t.Hp(a) == 97);
                    Check(k1 && k2, $"1012 피폭 — 턴이 끝날 때마다 모두 k^1 (−1 · −2), 스킵된 턴은 피폭 없음 · k 도 그대로 (HP {t.Hp(first)})");
                }
                {
                    var t = new ChainTable();
                    t.Server.RemovePlayer(t.O2);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1012)));
                    events.AddRange(t.Run(t.Now + 3));
                    GameRestarted restarted = events.OfType<GameRestarted>().FirstOrDefault();
                    Check(restarted != null && !restarted.Order.Contains(t.O2) && restarted.Order.Length == 2, "1012 나간 사람은 빼고 새 판 (봇으로 채우지 않는다)");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1012)));
                    t.Run(t.Now + 3);
                    int user = t.Server.CurrentTurnActor;
                    var events = t.PlayAndResolve(user, t.Give(user, Real(1012)));
                    events.AddRange(t.Run(t.Now + 3));
                    GameRestarted again = events.OfType<GameRestarted>().FirstOrDefault();
                    int first = again?.Order[0] ?? user;
                    t.Server.Draw(first);
                    t.Server.Draw(t.Server.CurrentTurnActor);
                    Check(again != null && again.Count == 2 && t.Hp(first) == 100 - 1 - 4, $"1012 두 번째 — n 2, 피폭 1 · 4 · 9 … (HP {t.Hp(first)})");
                }
                {
                    var t = new ChainTable();
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1012)));
                    events.AddRange(t.Run(t.Now + 3));
                    GameRestarted restarted = events.OfType<GameRestarted>().FirstOrDefault();
                    foreach (int a in new[] { t.Turn, t.O1, t.O2 })
                        t.SetHp(a, 1);
                    events = t.Server.Draw(restarted?.Order[0] ?? t.Turn).ToList();
                    events.AddRange(t.Run(t.Now + 10));
                    GameEnded ended = events.OfType<GameEnded>().FirstOrDefault();
                    Check(events.OfType<DeathPending>().Count() == 3 && ended != null && ended.Reason == GameEndReason.Draw && !events.OfType<TurnStarted>().Any(),
                        "1012 피폭으로 남은 사람이 동시에 모두 죽으면 무승부 · 다음 턴 없음");
                }
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.SetHandSize(t.Turn, 0);
                    for (int i = 0; i < 4; i++)
                        t.Give(t.Turn, Real(1023)); // 지금은 쓸 까닭이 없는 카드 — 1012 의 조건만 본다
                    t.Give(t.Turn, Real(1012));
                    t.SetHp(t.Turn, 40);
                    var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
                    var events = new List<GameEvent>();
                    for (now = 0; now < BotTurnSeconds; now += 0.1)
                        events.AddRange(driver.Tick());
                    bool used = events.OfType<CardPlayed>().Any(e => e.CardId == 1012);

                    double now2 = 0;
                    var t2 = new ChainTable(() => now2);
                    t2.SetHandSize(t2.Turn, 0);
                    for (int i = 0; i < 4; i++)
                        t2.Give(t2.Turn, Real(1023));
                    t2.Give(t2.Turn, Real(1012));
                    t2.SetHp(t2.O1, 40);
                    var driver2 = new BotDriver(t2.Server, new[] { t2.Turn }, new System.Random(1), () => now2);
                    var events2 = new List<GameEvent>();
                    for (now2 = 0; now2 < BotTurnSeconds; now2 += 0.1)
                        events2.AddRange(driver2.Tick());
                    Check(used && !events2.OfType<CardPlayed>().Any(e => e.CardId == 1012), "봇 — 1012 는 내 HP 가 꼴찌이거나 찹츄 상대가 있을 때만");
                }

                // ── 묶음 6 — 1005 드롭킥: 못 쓰러뜨리면 나도 49 — 피해로 HP 0 이 된 순간에 판정 (11-card-list 4절 #2) ──
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 40);
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    events.AddRange(t.Run(t.Now + 10));
                    Check(t.Hp(t.Turn) == 100 && events.OfType<PlayerDied>().Any(e => e.Actor == t.O1), "1005 드롭킥 — 쓰러뜨리면 반동 없음");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    t.Run(t.Now + 3);
                    Check(t.Hp(t.O1) == 51 && t.Hp(t.Turn) == 51, "1005 못 쓰러뜨리면 — 대상 49 · 나도 49");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 40);
                    t.SetTrapCard(t.O1, id => new Card(id, 902, CardType.Trap, TargetType.Self, new CardEffect[] { new SurviveAtEffect { hp = 10, now = true } }, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve, TrapTrigger.SelfDeathPending, 0));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    var events = t.Run(t.Now + 30);
                    Check(events.OfType<TrapActivated>().Any(e => e.Actor == t.O1) && t.Hp(t.O1) == 10 && t.Hp(t.Turn) == 100 && !events.OfType<PlayerDied>().Any(), "1005 HP 0 뒤 함정으로 살아나도 — 쓰러뜨린 것, 반동 없음");
                }
                {
                    var t = new ChainTable();
                    Card guard = t.Give(t.O1, Guard(ReactCondition.DamagesMe, new ReduceDamageEffect { amount = 49 }));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    t.At(t.Now + 2);
                    bool countered = !(t.Counter(t.O1, guard)[0] is RequestRejected);
                    t.Run(t.Now + 20);
                    Check(countered && t.Hp(t.O1) == 100 && t.Hp(t.Turn) == 51, "1005 피해 감소로 0 — 못 쓰러뜨림, 반동 49");
                }
                {
                    var t = new ChainTable();
                    Card negate = t.Give(t.O1, Guard(ReactCondition.Any, new NegateEffect()));
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1005)), t.O1);
                    t.At(t.Now + 2);
                    t.Counter(t.O1, negate);
                    t.Run(t.Now + 20);
                    Check(t.Hp(t.O1) == 100 && t.Hp(t.Turn) == 100, "1005 카드 전체 무효 — 피해 · 반동 둘 다 없음");
                }
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.SetHandSize(t.Turn, 4);
                    t.SetHandSize(t.O2, 9); // 손패는 O2 가 많다 — 그래도 쓰러뜨릴 수 있는 O1 을 노리는지
                    t.Give(t.Turn, Real(1005));
                    t.SetHp(t.O1, 40);
                    var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
                    var events = new List<GameEvent>();
                    for (now = 0; now < BotTurnSeconds; now += 0.1)
                        events.AddRange(driver.Tick());
                    bool killFirst = events.OfType<CardPlayed>().Any(e => e.CardId == 1005 && e.Targets.SequenceEqual(new[] { t.O1 }));

                    double now2 = 0;
                    var t2 = new ChainTable(() => now2);
                    t2.SetHandSize(t2.Turn, 4);
                    t2.Give(t2.Turn, Real(1005));
                    t2.SetHp(t2.Turn, 49);
                    var driver2 = new BotDriver(t2.Server, new[] { t2.Turn }, new System.Random(1), () => now2);
                    var events2 = new List<GameEvent>();
                    for (now2 = 0; now2 < BotTurnSeconds; now2 += 0.1)
                        events2.AddRange(driver2.Tick());
                    Check(killFirst && !events2.OfType<CardPlayed>().Any(e => e.CardId == 1005), "봇 — 1005 는 쓰러뜨릴 수 있으면 먼저 그 상대에게 · 못 쓰러뜨리는데 내 HP ≤ 49 면 안 씀");
                }

                // ── 묶음 7 — 1021 개판 오분전: 쓴 턴 뒤부터 생존자 수만큼 턴이면 한 바퀴 · 5바퀴 뒤 반반 기준 꼴찌 HP 0 (11-card-list 1021) ──
                // 턴은 시간 초과로 넘긴다 (뽑기로 넘기면 손패가 늘어 꼴찌가 바뀐다). until 이 참이 되거나 시간이 다하면 멈춘다
                List<GameEvent> RunWhile(ChainTable t, Func<List<GameEvent>, bool> until, double seconds)
                {
                    var all = new List<GameEvent>();
                    double end = t.Now + seconds;
                    while (t.Now < end && !until(all))
                        all.AddRange(t.Run(t.Now + 0.1));
                    return all;
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O2, 10);
                    t.SetHandSize(t.O2, 0); // HP · 손패 둘 다 꼴찌 — 어느 기준이든 O2
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                    bool started = events.OfType<GaepanChanged>().Any(g => g.Caster == t.Turn && g.Left == 5 && !g.Again);
                    events = RunWhile(t, all => all.OfType<GaepanJudged>().Any(), 2400);
                    int StartsBefore(int index) => events.Take(index < 0 ? 0 : index).OfType<TurnStarted>().Count();
                    int round1 = StartsBefore(events.FindIndex(e => e is GaepanChanged g && g.Left == 4)); // 쓴 턴 끝(안 셈) 뒤 3턴
                    GaepanJudged judged = events.OfType<GaepanJudged>().FirstOrDefault();
                    int turns = StartsBefore(events.IndexOf(judged));
                    bool listed = judged != null && judged.Actors.Length == 3 && judged.Values[System.Array.IndexOf(judged.Actors, t.O2)] == judged.Value;
                    bool held = t.Run(t.Now + 3.9).OfType<HpChanged>().All(h => h.Actor != t.O2) && t.Hp(t.O2) == 10;
                    var after = t.Run(t.Now + 0.5);
                    bool zero = t.Hp(t.O2) == 0 && after.OfType<DeathPending>().Any(e => e.Actor == t.O2);
                    after = t.Run(t.Now + 20);
                    Check(started && round1 == 3 && judged != null && judged.Loser == t.O2 && turns == 15 && listed && held && zero
                          && after.OfType<PlayerDied>().Any(e => e.Actor == t.O2) && after.OfType<TurnStarted>().Any(),
                        $"1021 개판 — 쓴 턴 뒤 3턴에 한 바퀴 · 5바퀴(15턴) 뒤 판정 (생존자 3명 · 값 목록) · 4.17초 공개 뒤 꼴찌 HP 0 → 사망 대기 → 다음 턴 (첫 바퀴 {round1}턴 · 판정까지 {turns}턴)");
                }
                {
                    var t = new ChainTable();
                    var events = t.PlayCard(t.Turn, t.Give(t.Turn, Real(1021))).ToList();
                    t.State(t.NextOf(t.Turn)).SkipNextTurn = true;
                    events = RunWhile(t, all => all.OfType<GaepanChanged>().Any(g => g.Left == 4), 400);
                    int started = events.Take(events.FindIndex(e => e is GaepanChanged g && g.Left == 4)).OfType<TurnStarted>().Count();
                    Check(events.OfType<TurnSkipped>().Any() && started == 2, $"1021 스킵된 턴도 1턴으로 센다 (한 바퀴까지 시작된 턴 {started})");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                    RunWhile(t, all => all.OfType<GaepanChanged>().Any(g => g.Left == 1), 2400);
                    int y = t.Server.CurrentTurnActor, z = t.NextOf(y), x = t.NextOf(z);
                    t.State(x).SkipNextTurn = true; // 마지막(15번째) 턴이 x 의 스킵된 턴
                    t.SetHp(z, 10);
                    t.SetHandSize(z, 0);
                    var events = RunWhile(t, all => all.OfType<GaepanJudged>().Any(), 200);
                    int judgedAt = events.FindIndex(e => e is GaepanJudged);
                    bool skippedFirst = events.FindIndex(e => e is TurnSkipped s && s.Actor == x) is int k && k >= 0 && k < judgedAt;
                    events.AddRange(t.Run(t.Now + 25));
                    TurnStarted next = events.Skip(judgedAt + 1).OfType<TurnStarted>().FirstOrDefault();
                    Check(judgedAt >= 0 && skippedFirst && t.State(z).Life == LifeState.Dead && next != null && next.Actor == y,
                        $"1021 스킵된 턴 끝에 판정 — 사망 처리 뒤 스킵 다음 사람부터 (다음 턴 P{next?.Actor} · 기대 P{y})");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                    RunWhile(t, all => all.OfType<GaepanChanged>().Any(g => g.Left == 4), 400);
                    int user = t.Server.CurrentTurnActor;
                    var events = t.PlayAndResolve(user, t.Give(user, Real(1021)));
                    Check(events.OfType<GaepanChanged>().Any(g => g.Caster == user && g.Left == 5 && g.Again), "1021 개판 중에 다시 쓰면 5바퀴부터 다시");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                    RunWhile(t, all => all.OfType<TurnStarted>().Any(), 60);
                    t.Server.RemovePlayer(t.Turn); // 건 사람이 나가도 이어진다 · 바퀴는 남은 2명으로
                    var events = RunWhile(t, all => all.OfType<GaepanChanged>().Any(g => g.Left == 4), 400);
                    Check(events.OfType<GaepanChanged>().Any(g => g.Caster == t.Turn && g.Left == 4), "1021 건 사람이 나가도 개판은 이어진다");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 10);
                    t.SetHp(t.O2, 10);
                    t.SetHandSize(t.O1, 0);
                    t.SetHandSize(t.O2, 0);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                    var events = RunWhile(t, all => all.OfType<GaepanJudged>().Any(), 2400);
                    GaepanJudged judged = events.OfType<GaepanJudged>().FirstOrDefault();
                    Check(judged != null && judged.Tied.Length == 2 && judged.Tied.Contains(judged.Loser) && judged.Loser != t.Turn,
                        $"1021 동점이면 그중 무작위 1명 (꼴찌 P{judged?.Loser} · 동점 {judged?.Tied.Length})");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1021)));
                    RunWhile(t, all => all.OfType<TurnStarted>().Any(), 60);
                    int user = t.Server.CurrentTurnActor;
                    var events = t.PlayAndResolve(user, t.Give(user, Real(1012)));
                    events.AddRange(t.Run(t.Now + 3));
                    Check(events.OfType<GameRestarted>().Any() && events.OfType<GaepanChanged>().Any(g => g.Caster == -1 && g.Left == 0) && !t.Server.IsGaepanActive,
                        "1021 개판은 1012 대소동 새 판에서 지워진다");
                }
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.SetHandSize(t.Turn, 0);
                    for (int i = 0; i < 10; i++)
                        t.Give(t.Turn, Real(1023)); // 지금은 쓸 까닭이 없는 카드로 11장 — 뽑으면 10장에서 더 멀어지고, 한 장 내면 10장
                    t.Give(t.Turn, Real(1021));
                    t.SetHp(t.O1, 70);
                    var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
                    var events = new List<GameEvent>();
                    for (now = 0; now < BotTurnSeconds; now += 0.1)
                        events.AddRange(driver.Tick());
                    bool used = events.OfType<CardPlayed>().Any(e => e.CardId == 1021);

                    double now2 = 0;
                    var t2 = new ChainTable(() => now2);
                    t2.SetHandSize(t2.Turn, 0);
                    for (int i = 0; i < 10; i++)
                        t2.Give(t2.Turn, Real(1023));
                    t2.Give(t2.Turn, Real(1021));
                    t2.SetHp(t2.O1, 90);
                    var driver2 = new BotDriver(t2.Server, new[] { t2.Turn }, new System.Random(1), () => now2);
                    var events2 = new List<GameEvent>();
                    for (now2 = 0; now2 < BotTurnSeconds; now2 += 0.1)
                        events2.AddRange(driver2.Tick());
                    Check(used && !events2.OfType<CardPlayed>().Any(e => e.CardId == 1021), "봇 — 1021 은 개판이 없고 HP 가 상대 최저보다 20 · 손패가 상대 최소보다 2장 이상 많을 때만");
                }

                // ── 묶음 8 — 1019 목표 조준: 대상이 5초 안에 고른다 (버리기 / 80 피해), 안 고르면 무작위 · 그동안 멈춤 (11-card-list 1019) ──
                {
                    var t = new ChainTable();
                    var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    ChoiceStarted started = events.OfType<ChoiceStarted>().FirstOrDefault();
                    var waiting = t.Run(t.Now + 4);
                    bool paused = !waiting.OfType<ChoiceMade>().Any() && !waiting.OfType<TurnStarted>().Any() && t.Hp(t.O1) == 100;
                    var rejected = t.Server.Choose(t.Turn, 1); // 고르는 사람이 아니다
                    var chosen = t.Server.Choose(t.O1, 1).ToList();
                    chosen.AddRange(t.Run(t.Now + 3));
                    Check(started != null && started.Actor == t.O1 && started.Damage == 80 && System.Math.Abs(started.Deadline - started.StartsAt - 5) < 0.01 && paused
                          && rejected.OfType<RequestRejected>().Any(r => r.Code == RejectCode.NotChoosing)
                          && chosen.OfType<ChoiceMade>().Any(c => c.Actor == t.O1 && c.Option == 1 && !c.TimedOut) && t.Hp(t.O1) == 20 && chosen.OfType<TurnStarted>().Any(),
                        "1019 목표 조준 — 대상이 고르는 동안 멈춤 · 남의 고르기 거절(코드 20) · 피해 받기 80 → 다음 턴");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    var events = t.Server.Choose(t.O1, 0).ToList();
                    Check(t.HandCount(t.O1) == 0 && t.Hp(t.O1) == 100 && events.OfType<CardsDiscarded>().Any(), "1019 손패 전부 버리기 — 피해 없음");
                }
                {
                    var t = new ChainTable();
                    t.SetHandSize(t.O1, 0);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    var events = t.Server.Choose(t.O1, 0).ToList();
                    events.AddRange(t.Run(t.Now + 3));
                    Check(t.HandCount(t.O1) == 0 && t.Hp(t.O1) == 100 && events.OfType<TurnStarted>().Any(), "1019 손패 0장이어도 버리기를 고를 수 있다 — 아무 손해 없이");
                }
                {
                    var t = new ChainTable();
                    int hand = t.HandCount(t.O1);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    var events = t.Run(t.Now + 8);
                    ChoiceMade made = events.OfType<ChoiceMade>().FirstOrDefault();
                    bool applied = made != null && (made.Option == 0 ? t.HandCount(t.O1) == 0 && t.Hp(t.O1) == 100 : t.Hp(t.O1) == 20 && t.HandCount(t.O1) == hand);
                    Check(made != null && made.TimedOut && applied, $"1019 5초 안에 안 고르면 방장이 무작위 (고름 {made?.Option})");
                }
                {
                    var t = new ChainTable();
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    var events = t.Server.RemovePlayer(t.O1).ToList();
                    events.AddRange(t.Run(t.Now + 3));
                    Check(events.OfType<ChoiceMade>().Any(c => c.Actor == t.O1 && c.Option == -1) && events.OfType<TurnStarted>().Any(), "1019 고르는 사람이 나가면 불발 · 처리를 마치고 다음 턴");
                }
                {
                    var t = new ChainTable();
                    t.SetHp(t.O1, 50);
                    t.PlayAndResolve(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    var events = t.Server.Choose(t.O1, 1).ToList();
                    events.AddRange(t.Run(t.Now + 15));
                    Check(events.OfType<DeathPending>().Any(e => e.Actor == t.O1) && events.OfType<PlayerDied>().Any(e => e.Actor == t.O1) && events.OfType<TurnStarted>().Any(),
                        "1019 피해 받기로 HP 0 — 사망 대기 → 최종 사망 → 다음 턴");
                }
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.SetHp(t.O1, 70);
                    t.SetHandSize(t.O1, 0);
                    for (int i = 0; i < 3; i++)
                        t.Give(t.O1, Real(1023)); // 카운터 없는 손패 — 봇이 1019 를 무효화하지 않게
                    var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => now);
                    t.PlayCard(t.Turn, t.Give(t.Turn, Real(1019)), t.O1);
                    var events = new List<GameEvent>();
                    for (now = 0; now < 25 && !events.OfType<ChoiceMade>().Any(); now += 0.1)
                    {
                        events.AddRange(t.Server.Tick());
                        events.AddRange(driver.Tick());
                    }
                    bool lowDiscard = events.OfType<ChoiceMade>().Any(c => c.Actor == t.O1 && c.Option == 0 && !c.TimedOut);

                    double now2 = 0;
                    var t2 = new ChainTable(() => now2);
                    t2.SetHandSize(t2.O1, 0);
                    for (int i = 0; i < 6; i++)
                        t2.Give(t2.O1, Real(1023));
                    var driver2 = new BotDriver(t2.Server, new[] { t2.O1 }, new System.Random(1), () => now2);
                    t2.PlayCard(t2.Turn, t2.Give(t2.Turn, Real(1019)), t2.O1);
                    var events2 = new List<GameEvent>();
                    for (now2 = 0; now2 < 25 && !events2.OfType<ChoiceMade>().Any(); now2 += 0.1)
                    {
                        events2.AddRange(t2.Server.Tick());
                        events2.AddRange(driver2.Tick());
                    }
                    Check(lowDiscard && events2.OfType<ChoiceMade>().Any(c => c.Actor == t2.O1 && c.Option == 1 && !c.TimedOut),
                        "봇 대상 — HP ≤ 80 이면 버리기 · HP 높고 손패 5장 이상이면 피해 받기 (시간 안에)");
                }
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.SetHandSize(t.Turn, 0);
                    t.Give(t.Turn, Real(1019));
                    for (int i = 0; i < 6; i++)
                        t.Give(t.Turn, Real(1023)); // 지금은 쓸 까닭이 없는 카드로 손패 7장
                    t.SetHandSize(t.O1, 3);
                    t.SetHandSize(t.O2, 2);
                    t.SetHp(t.O2, 50); // O1 3×3 = 9 · O2 2×3 + 40 = 46
                    var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
                    var events = new List<GameEvent>();
                    for (now = 0; now < BotTurnSeconds; now += 0.1)
                        events.AddRange(driver.Tick());
                    Check(events.OfType<CardPlayed>().Any(e => e.CardId == 1019 && e.Targets.SequenceEqual(new[] { t.O2 })), "봇 — 1019 대상은 손패 × 3 + (HP ≤ 80 이면 40) 가 가장 큰 상대");
                }

                // 자동 대상이 무작위로 갈리는 때 (7-2) — 1018 무야호에서 상대끼리 손패 최다 동률이면 대상을 비워 내고, 로그에 「중 무작위」
                {
                    double now = 0;
                    var t = new ChainTable(() => now);
                    t.SetHandSize(t.Turn, 0);
                    t.Give(t.Turn, Real(1018));
                    for (int i = 0; i < 4; i++)
                        t.Give(t.Turn, Real(1007));
                    t.SetHandSize(t.O1, 10);
                    t.SetHandSize(t.O2, 10);
                    t.State(t.O1).IsChapchu = true;
                    var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now, _ => BotLevel.Hard);
                    var events = new List<GameEvent>();
                    var logs = new List<string>();
                    for (now = 0; now < 7; now += 0.1)
                    {
                        events.AddRange(driver.Tick());
                        logs.AddRange(driver.TakeLogs());
                    }
                    Check(events.OfType<CardPlayed>().Any(e => e.CardId == 1018) && !events.OfType<RequestRejected>().Any() && logs.Any(l => l.Contains("중 무작위")),
                        "봇 — 1018 손패 최다가 동률이면 동률인 사람들의 평균 점수로 내고 대상은 방장이 고른다 (" + (logs.LastOrDefault(l => l.Contains("7-1")) ?? "로그 없음") + ")");
                }
            }
        }

        // ⑥ 뜸 (4절 · 7-7) — 함정은 한 장씩 사이를 두고, 그 뒤에 메인 행동. 생각 시간은 쉬움 > 보통 > 어려움이고 메인 행동은 턴 시작 11초 안
        {
            double now = 0;
            var t = new ChainTable(() => now);
            t.GiveCard(t.Turn, ChainTable.TrapHit);
            t.GiveCard(t.Turn, ChainTable.TrapHit);
            var driver = new BotDriver(t.Server, new[] { t.Turn }, new System.Random(1), () => now);
            var setAt = new List<double>();
            double actedAt = -1;
            for (now = 0; now < BotTurnSeconds + 3 && actedAt < 0; now += 0.1)
            {
                List<GameEvent> tick = driver.Tick().ToList();
                setAt.AddRange(tick.OfType<TrapSet>().Select(_ => now));
                if (tick.Any(e => e is CardDrawn || e is CardPlayed)) actedAt = now;
            }
            Check(setAt.Count == 2 && setAt[1] - setAt[0] >= 0.5 && actedAt - setAt[1] >= 0.5,
                $"봇 — 함정은 한 장씩 사이를 두고 놓은 뒤 메인 행동 (설치 {string.Join(" · ", setAt.Select(s => $"{s:0.0}"))}초 · 행동 {actedAt:0.0}초)");
        }
        DeckData paceDeck = AssetDatabase.LoadAssetAtPath<DeckData>(MainDeckPath);
        if (paceDeck != null)
        {
            var think = new Dictionary<BotLevel, List<double>>();
            foreach (BotLevel level in new[] { BotLevel.Easy, BotLevel.Normal, BotLevel.Hard })
            {
                think[level] = new List<double>();
                for (int game = 0; game < 10; game++)
                {
                    double now = 0;
                    int[] actors = { 1001, 1002, 1003, 1004 };
                    var server = new GameServer(new System.Random(game), () => now);
                    server.StartGame(actors, paceDeck.CreateCards(), actors);
                    var driver = new BotDriver(server, actors, new System.Random(game + 7919), () => now, _ => level);
                    while (!server.IsGameOver && now < 3600)
                    {
                        now += 0.1;
                        driver.Observe(server.Tick());
                        driver.Observe(driver.Tick());
                        foreach (string log in driver.TakeLogs())
                        {
                            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(log, @" 7-1 .* · 생각 ([0-9.]+)초");
                            if (match.Success)
                                think[level].Add(double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                }
            }
            double easy = think[BotLevel.Easy].DefaultIfEmpty(0).Average(), normal = think[BotLevel.Normal].DefaultIfEmpty(0).Average(), hard = think[BotLevel.Hard].DefaultIfEmpty(0).Average();
            double slowest = think.Values.SelectMany(v => v).DefaultIfEmpty(99).Max();
            Check(think.Values.All(v => v.Count > 0) && easy > normal && normal > hard && slowest <= GameServer.TurnTimeLimit - 4,
                $"봇 — 생각 시간 평균 쉬움 {easy:0.0} > 보통 {normal:0.0} > 어려움 {hard:0.0}초 · 가장 긴 {slowest:0.0}초 ≤ 11초 (10판씩)");
        }

        // ⑤ 난이도 (7-7) — 같은 보통 봇 한 자리가 쉬움 셋 사이에서는 더 자주, 어려움 셋 사이에서는 덜 이긴다. 시드가 고정이라 결과는 늘 같다
        DeckData arenaDeck = AssetDatabase.LoadAssetAtPath<DeckData>(MainDeckPath);
        if (arenaDeck == null)
            sb.AppendLine("  SKIP 난이도 — 게임 덱이 없다 (" + MainDeckPath + ")");
        else
        {
            const int Games = 100;
            BotLevel[] amongEasy = { BotLevel.Normal, BotLevel.Easy, BotLevel.Easy, BotLevel.Easy };
            BotLevel[] amongHard = { BotLevel.Normal, BotLevel.Hard, BotLevel.Hard, BotLevel.Hard };
            int vsEasy = 0, vsHard = 0;
            for (int game = 0; game < Games; game++)
            {
                if (ArenaWinnerSeat(arenaDeck, amongEasy, game) == 0) vsEasy++;
                if (ArenaWinnerSeat(arenaDeck, amongHard, game) == 0) vsHard++;
            }
            Check(vsEasy > vsHard, $"난이도 — 보통 한 자리의 승률: 쉬움 셋 사이 {100 * vsEasy / Games}% > 어려움 셋 사이 {100 * vsHard / Games}% ({Games}판씩)");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    // 봇끼리 game 번째 판(시드 고정)을 끝까지 — 이긴 자리(0 ~). 무승부 · 한 시간 안에 안 끝나면 −1
    private static int ArenaWinnerSeat(DeckData deck, BotLevel[] seats, int game)
    {
        double now = 0;
        int[] actors = Enumerable.Range(1001, seats.Length).ToArray();
        var server = new GameServer(new System.Random(game), () => now);
        server.StartGame(actors, deck.CreateCards(), actors);
        var driver = new BotDriver(server, actors, new System.Random(game + 7919), () => now, actor => seats[actor - 1001]);
        while (now < 3600)
        {
            now += 0.1;
            IReadOnlyList<GameEvent> ticked = server.Tick();
            driver.Observe(ticked);
            IReadOnlyList<GameEvent> acted = driver.Tick();
            driver.Observe(acted);
            driver.TakeLogs();
            GameEnded ended = ticked.Concat(acted).OfType<GameEnded>().FirstOrDefault();
            if (ended != null)
                return ended.Winner >= 1001 ? ended.Winner - 1001 : -1;
        }

        return -1;
    }

    /// <summary>
    /// 봇끼리 게임 덱(Deck_Main)으로 여러 판 — 난이도별 승률 · 승리 방식 · 판 길이를 잰다 (18-bot.md 7-7 「난이도 확인」). 통과 · 실패를 가리지 않고 숫자만 낸다.
    /// levels = 자리마다 난이도 (E 쉬움 · N 보통 · H 어려움, 예: "HHEE"). 턴 순서는 판마다 규칙이 섞는다.
    /// </summary>
    public static string BotArena(string levels = "HHEE", string gamesArg = "200")
    {
        DeckData main = AssetDatabase.LoadAssetAtPath<DeckData>(MainDeckPath);
        if (main == null)
            return "FAIL 게임 덱이 없다 — " + MainDeckPath;

        int games = int.Parse(gamesArg);
        BotLevel[] seats = levels.Select(c => c == 'E' ? BotLevel.Easy : c == 'H' ? BotLevel.Hard : BotLevel.Normal).ToArray();
        int[] actors = Enumerable.Range(1001, seats.Length).ToArray();
        var wins = new Dictionary<BotLevel, int>();
        var reasons = new Dictionary<GameEndReason, int>();
        int unfinished = 0, turns = 0, declares = 0, broken = 0, plays = 0, draws = 0, idles = 0, mistakes = 0;
        double seconds = 0;
        var cardUse = new SortedDictionary<int, int>();

        for (int game = 0; game < games; game++)
        {
            double now = 0;
            var server = new GameServer(new System.Random(game), () => now);
            server.StartGame(actors, main.CreateCards(), actors);
            var driver = new BotDriver(server, actors, new System.Random(game + 7919), () => now, actor => seats[actor - 1001]);
            GameEnded ended = null;
            while (ended == null && now < 3600)
            {
                now += 0.1;
                IReadOnlyList<GameEvent> ticked = server.Tick();
                driver.Observe(ticked);
                IReadOnlyList<GameEvent> acted = driver.Tick();
                driver.Observe(acted);
                foreach (GameEvent gameEvent in ticked.Concat(acted))
                {
                    switch (gameEvent)
                    {
                        case TurnStarted _: turns++; break;
                        case ChapchuChanged chapchu: if (chapchu.IsChapchu) declares++; else broken++; break;
                        case CardPlayed played: plays++; cardUse[played.CardId] = cardUse.TryGetValue(played.CardId, out int used) ? used + 1 : 1; break;
                        case GameEnded end: ended = end; break;
                    }
                }
                foreach (string log in driver.TakeLogs())
                {
                    if (log.Contains("7-1 뽑기")) draws++;
                    if (log.Contains("7-1 아무것도") || log.Contains("7-1 손패 10장") || log.Contains("7-1 찹츄 유지")) idles++; // 「(다음 수: …)」 는 세지 않는다
                    if (log.Contains("실수")) mistakes++;
                }
            }

            if (ended == null)
            {
                unfinished++;
                continue;
            }

            seconds += now;
            reasons[ended.Reason] = reasons.TryGetValue(ended.Reason, out int count) ? count + 1 : 1;
            if (ended.Winner >= 1001)
            {
                BotLevel level = seats[ended.Winner - 1001];
                wins[level] = wins.TryGetValue(level, out int won) ? won + 1 : 1;
            }
        }

        int finished = games - unfinished;
        var sb = new StringBuilder();
        sb.AppendLine($"OK 자리 {levels} · {games}판 (끝까지 {finished})");
        foreach (BotLevel level in seats.Distinct())
        {
            int seatCount = seats.Count(s => s == level);
            wins.TryGetValue(level, out int won);
            sb.AppendLine($"  {level} × {seatCount} — {won}승 ({100.0 * won / Math.Max(1, finished):0}% · 자리당 {100.0 * won / Math.Max(1, finished) / seatCount:0}%)");
        }
        sb.AppendLine("  승리 방식 — " + string.Join(" · ", reasons.Select(pair => $"{pair.Key} {pair.Value}")));
        sb.AppendLine($"  한 판 평균 {seconds / Math.Max(1, finished):0}초 · {(double)turns / games:0.0}턴 · 카드 {(double)plays / games:0.0}장 · 뽑기 {(double)draws / games:0.0}번 · 기다림 {(double)idles / games:0.0}번 · 실수 {(double)mistakes / games:0.0}번");
        sb.AppendLine($"  찹츄 선언 {(double)declares / games:0.00}번 / 판 · 해제 {(double)broken / games:0.00}번 / 판");
        sb.Append("  카드별 사용 (판당) — " + string.Join(" ", cardUse.Select(pair => $"{pair.Key}:{(double)pair.Value / games:0.0}")));
        return sb.ToString();
    }

    /// <summary>
    /// 규칙 계층의 카드 효과 (11-card-list.md 5-1, 기능 9) — 효과 종류 · 새 대상 타입 · 카운터의 반응 조건 · 아래 카드 표시 · 「~한 후」 함정.
    /// 카드는 메모리에서 만든다 (수치는 시험용). 시계는 0.1초씩.
    /// </summary>
    public static string Effects()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        CardType[] action = { CardType.Action }, actionTrap = { CardType.Action, CardType.Trap }, trapOnly = { CardType.Trap };
        Func<int, Card> Act(TargetType t, params CardEffect[] e) => id => new Card(id, 900, CardType.Action, t, e, null, ChainTable.Reveal, ChainTable.Resolve);
        Func<int, Card> Ctr(TargetType t, CardType[] reacts, ReactCondition c, params CardEffect[] e) => id => new Card(id, 901, CardType.Counter, t, e, reacts, ChainTable.Reveal, ChainTable.Resolve, TrapTrigger.None, 0, c);
        Func<int, Card> Trp(TargetType t, TrapTrigger trig, int value, params CardEffect[] e) => id => new Card(id, 902, CardType.Trap, t, e, action, ChainTable.Reveal, ChainTable.Resolve, trig, value);
        DamageEffect Dmg(int n, EffectSubject s = EffectSubject.Target) => new DamageEffect { subject = s, amount = n };

        // ── 효과 ──
        {
            var t = new ChainTable();
            t.SetHp(t.Turn, 50);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.Self, new HealEffect { subject = EffectSubject.Self, amount = 30 })));
            Check(t.Hp(t.Turn) == 80, "Heal 50 → 80");
        }
        {
            var t = new ChainTable();
            t.SetHp(t.Turn, 90);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.Self, new HealEffect { subject = EffectSubject.Self, amount = 30 })));
            Check(t.Hp(t.Turn) == 100, "Heal 최대 100");
        }
        {
            var t = new ChainTable();
            int before = t.HandCount(t.Turn);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.Self, new DrawEffect { subject = EffectSubject.Self, count = 2 })));
            Check(t.HandCount(t.Turn) == before + 2, "Draw 2장 (낸 카드 빼고 +2)");
        }
        {
            var t = new ChainTable();
            int before = t.HandCount(t.O1), discard = t.DiscardCount;
            var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DiscardEffect { subject = EffectSubject.Target, count = 2 })), t.O1);
            Check(t.HandCount(t.O1) == before - 2 && t.DiscardCount == discard + 3 && events.OfType<CardsDiscarded>().Any(), "Discard 대상 2장 → 버림 더미 (+낸 카드)");
        }
        {
            var t = new ChainTable();
            int mine = t.HandCount(t.Turn), theirs = t.HandCount(t.O1);
            var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new StealEffect { count = 2 })), t.O1);
            Check(t.HandCount(t.Turn) == mine + 2 && t.HandCount(t.O1) == theirs - 2 && events.OfType<HandCardsMoved>().Any(), "Steal 대상 → 나 2장");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 3);
            t.SetHandSize(t.O1, 7);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SwapHandEffect())), t.O1);
            Check(t.HandCount(t.Turn) == 7 && t.HandCount(t.O1) == 3, "SwapHand 나 ↔ 대상");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 2);
            t.SetHandSize(t.O2, 8);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.TwoOthers, new SwapHandEffect { betweenTargets = true })), t.O1, t.O2);
            Check(t.HandCount(t.O1) == 8 && t.HandCount(t.O2) == 2, "SwapHand 대상 1 ↔ 대상 2");
        }
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            var events = t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DestroyTrapEffect { count = 1 })), t.O1);
            Check(t.TrapCount(t.O1) == 0 && events.OfType<TrapDestroyed>().Any(), "DestroyTrap → 버림 더미");
        }
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            int hand = t.HandCount(t.O1);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new ReturnTrapEffect { count = 1 })), t.O1);
            Check(t.TrapCount(t.O1) == 0 && t.HandCount(t.O1) == hand + 1, "ReturnTrap → 주인 손패");
        }
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new StealTrapEffect())), t.O1);
            Check(t.TrapCount(t.O1) == 0 && t.TrapCount(t.Turn) == 1, "StealTrap → 내 빈 칸");
        }
        {
            var t = new ChainTable();
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SkipTurnEffect { subject = EffectSubject.Target })), t.O1);
            var events = t.Run(t.Now + 5);
            Check(events.OfType<TurnSkipped>().Any(e => e.Actor == t.O1) && t.Server.CurrentTurnActor == t.O2, "SkipTurn → 대상 턴을 건너뛰고 다음 사람");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 10);
            t.State(t.O1).IsChapchu = true; // 선언해 둔 상태
            t.SetHandSize(t.O2, 10);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.TwoOthers, new MoveChapChuEffect())), t.O1, t.O2);
            Check(!t.State(t.O1).IsChapchu && t.State(t.O2).IsChapchu, "MoveChapChu 대상 1 → 대상 2");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 10);
            t.State(t.O1).IsChapchu = true; // 선언해 둔 상태
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.ChapchuPlayers, Dmg(20))));
            Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 100, "대상 ChapchuPlayers — 찹츄인 사람만");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 10);
            t.SetHandSize(t.O2, 2);
            t.SetHandSize(t.Turn, 6);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.MoreHandThanMe, Dmg(20))));
            Check(t.Hp(t.O1) == 80 && t.Hp(t.O2) == 100, "대상 MoreHandThanMe — 나보다 손패 많은 사람만");
        }
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DamagePerTrapEffect { amount = 7 })), t.O1);
            Check(t.Hp(t.O1) == 86, "DamagePerTrap 함정 2 × 7");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 8);
            t.SetHandSize(t.Turn, 5); // + 낼 카드 1장 → 낸 뒤 5
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new HandDiffDamageEffect { amount = 5 })), t.O1);
            Check(t.Hp(t.O1) == 85, "HandDiffDamage (8 − 5) × 5");
        }

        // ── 카운터: 반응 조건 · 아래 카드 표시 ──
        Func<int, Card> hit = Act(TargetType.OneOther, Dmg(20));
        {
            var t = new ChainTable();
            Card c01 = t.Give(t.O1, Ctr(TargetType.None, action, ReactCondition.Any, new ReturnToHandEffect()));
            t.GiveTrap(t.O2, ChainTable.TrapHit); // 반환 뒤 함정(T13)은 따로
            Card card = t.Give(t.Turn, hit);
            t.PlayCard(t.Turn, card, t.O1);
            t.At(2);
            t.Counter(t.O1, c01);
            var events = t.Run(20);
            Check(t.Hp(t.O1) == 100 && events.OfType<CardReturnedToHand>().Any(e => e.CardInstanceId == card.InstanceId) && t.Hand(t.Turn).Contains(card), "C01 손패로 반환 — 효과 없이 시전자 손패");
        }
        {
            var t = new ChainTable();
            Card c02 = t.Give(t.O1, Ctr(TargetType.OneOther, action, ReactCondition.ByOther, new RedirectEffect()));
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            t.At(2);
            t.Counter(t.O1, c02, t.O2);
            t.Run(20);
            Check(t.Hp(t.O1) == 100 && t.Hp(t.O2) == 80, "C02 대상 변경 → 고른 사람");
        }
        {
            var t = new ChainTable();
            Card c04 = t.Give(t.O1, Ctr(TargetType.None, action, ReactCondition.ByOther, new RedirectEffect { toCaster = true }));
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            t.At(2);
            t.Counter(t.O1, c04);
            t.Run(20);
            Check(t.Hp(t.O1) == 100 && t.Hp(t.Turn) == 80, "C04 대상 변경 → 시전자");
        }
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 50);
            Func<int, Card> c03 = Ctr(TargetType.None, action, ReactCondition.DamagesMe, new ConvertDamageToHealEffect());
            Card mine = t.Give(t.O1, c03), other = t.Give(t.O2, c03);
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            t.At(2);
            Check(t.Code(t.Counter(t.O2, other)) == RejectCode.CannotReact, "C03 조건 「나에게 피해」 — 안 맞는 사람 → 9");
            t.Counter(t.O1, mine);
            t.Run(20);
            Check(t.Hp(t.O1) == 70, "C03 피해 → 회복 (50 → 70)");
        }
        {
            var t = new ChainTable();
            t.SetHp(t.Turn, 50);
            Card c09 = t.Give(t.O1, Ctr(TargetType.None, action, ReactCondition.HealsOther, new ConvertHealToDamageEffect()));
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.Self, new HealEffect { subject = EffectSubject.Self, amount = 20 })));
            t.At(2);
            t.Counter(t.O1, c09);
            t.Run(20);
            Check(t.Hp(t.Turn) == 30, "C09 남의 회복 → 피해 (50 → 30)");
        }
        {
            var t = new ChainTable();
            Card c10 = t.Give(t.O1, Ctr(TargetType.None, actionTrap, ReactCondition.DamagesMe, new ReduceDamageEffect { amount = 5 }));
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            t.At(2);
            t.Counter(t.O1, c10);
            t.Run(20);
            Check(t.Hp(t.O1) == 85, "C10 피해 감소 20 − 5");
        }
        {
            var t = new ChainTable();
            Func<int, Card> c11 = Ctr(TargetType.None, actionTrap, ReactCondition.WouldKillMe, new SurviveAtEffect { hp = 1 });
            t.SetHp(t.O1, 20);
            Card save = t.Give(t.O1, c11);
            t.SetHp(t.O2, 50);
            Card notYet = t.Give(t.O2, c11);
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.AllOthers, Dmg(20))));
            t.At(2);
            Check(t.Code(t.Counter(t.O2, notYet)) == RejectCode.CannotReact, "C11 조건 「그 피해로 사망」 — HP 50 → 9");
            t.Counter(t.O1, save);
            var events = t.Run(20);
            Check(t.Hp(t.O1) == 1 && !events.OfType<DeathPending>().Any(), "C11 사망 방지 — HP 1 로 남고 사망 대기 없음");
        }
        {
            var t = new ChainTable();
            Card c13 = t.Give(t.O1, Ctr(TargetType.None, actionTrap, ReactCondition.SkipsMe, new NegateEffect { selfOnly = true }));
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SkipTurnEffect { subject = EffectSubject.Target })), t.O1);
            t.At(2);
            t.Counter(t.O1, c13);
            t.Run(20); // 처리 13.5 · 다음 턴 14.7 — O1 턴 30초 안
            Check(!t.State(t.O1).SkipNextTurn && t.Server.CurrentTurnActor == t.O1, "C13 내 턴 스킵 무효");
        }
        {
            var t = new ChainTable();
            Card c08 = t.Give(t.O1, Ctr(TargetType.None, actionTrap, ReactCondition.TakesMyHand, new NegateEffect { selfOnly = true }, new DrawEffect { subject = EffectSubject.Self, count = 1 }));
            int hand = t.HandCount(t.O1);
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new StealEffect { count = 2 })), t.O1);
            t.At(2);
            t.Counter(t.O1, c08);
            t.Run(20);
            Check(t.HandCount(t.O1) == hand - 1 + 1, "C08 훔치기 무효 + 1장 드로우 (카운터 1장 빠지고 1장)");
        }
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            Card c12 = t.Give(t.O1, Ctr(TargetType.None, action, ReactCondition.DestroysMyTrap, new ProtectTrapEffect()));
            int hand = t.HandCount(t.O1);
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DestroyTrapEffect { count = 1 })), t.O1);
            t.At(2);
            t.Counter(t.O1, c12);
            var events = t.Run(20);
            Check(t.TrapCount(t.O1) == 0 && t.HandCount(t.O1) == hand && events.OfType<TrapReturned>().Any() && !events.OfType<TrapDestroyed>().Any(), "C12 파괴 대신 손패로");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O2, 10);
            t.State(t.O2).IsChapchu = true; // 선언해 둔 상태
            Card c15 = t.Give(t.O1, Ctr(TargetType.None, actionTrap, ReactCondition.GivesMeChapchu, new NegateEffect { selfOnly = true })); // 카운터를 내면 6장 — 10장이면 자동 선언이 된다
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.TwoOthers, new MoveChapChuEffect())), t.O2, t.O1);
            t.At(2);
            Check(!(t.Counter(t.O1, c15)[0] is RequestRejected), "C15 조건 「나에게 찹츄 부여」 맞음");
            t.Run(20);
            Check(!t.State(t.O1).IsChapchu, "C15 나에게 오는 찹츄 무효");
        }
        // ── 「~한 후」 함정 ──
        void After(string name, Action<ChainTable> setup, Func<ChainTable, Card> trap, Action<ChainTable> act, Func<ChainTable, bool> result)
        {
            var t = new ChainTable();
            setup?.Invoke(t);
            Card card = trap(t);
            act(t);
            bool fired = t.Run(t.Now + 40).OfType<TrapActivated>().Any(e => e.Actor == t.O1 && e.CardInstanceId == card.InstanceId);
            Check(fired && result(t), name + (fired ? "" : " — 함정이 발동하지 않았다"));
        }

        After("T08 훔쳐진 후 → 되훔침", null,
            t => t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.AfterStolen, 0, new StealEffect { count = 1 })),
            t => t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new StealEffect { count = 2 })), t.O1),
            t => t.HandCount(t.O1) == 6 - 2 + 1);
        After("T09 버려진 후 → 버려진 장수만큼 드로우", null,
            t => t.SetTrapCard(t.O1, Trp(TargetType.Self, TrapTrigger.AfterDiscarded, 0, new DrawEffect { subject = EffectSubject.Self, useTriggerAmount = true })),
            t => t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DiscardEffect { subject = EffectSubject.Target, count = 3 })), t.O1),
            t => t.HandCount(t.O1) == 6);
        After("T10 함정 파괴된 후 → 반격", t => t.GiveTrap(t.O1, ChainTable.TrapReact),
            t => t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.AfterTrapDestroyed, 0, Dmg(10))),
            t => t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new DestroyTrapEffect { count = 1 })), t.O1),
            t => t.Hp(t.Turn) == 90 || t.TrapCount(t.O1) == 0); // 무작위로 반격 함정 자신이 파괴되면 발동하지 않는다 — 발동했다면 반격
        After("T13 카드 반환된 후 → 시전자 피해",
            t => t.SetHp(t.O2, 100),
            t => t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.AfterCardReturned, 0, Dmg(10))),
            t =>
            {
                Card c01 = t.Give(t.O2, Ctr(TargetType.None, action, ReactCondition.Any, new ReturnToHandEffect()));
                t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O2);
                t.At(2);
                t.Counter(t.O2, c01);
            },
            t => t.Hp(t.Turn) == 90);
        After("T14 여러 장 뽑은 후 → 버리게", null,
            t => t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.AfterDrewMany, 2, new DiscardEffect { subject = EffectSubject.Target, count = 1 })),
            t => t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.Self, new DrawEffect { subject = EffectSubject.Self, count = 2 }))),
            t => t.HandCount(t.Turn) == 6 + 2 - 1);
        After("T16 찹츄 부여된 후 → 준 사람에게도", t => { t.SetHandSize(t.O1, 10); t.SetHandSize(t.Turn, 10); }, // 낼 카드 1장이 더해졌다 빠져 10
            t => t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.AfterChapchuGiven, 0, new SetChapChuEffect { subject = EffectSubject.Target })),
            t => t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SetChapChuEffect { subject = EffectSubject.Target })), t.O1),
            t => t.State(t.O1).IsChapchu && t.State(t.Turn).IsChapchu);
        {
            // T15 — 턴 스킵된 뒤 → 다음 실제 턴에 추가 드로우
            var t = new ChainTable();
            t.SetTrapCard(t.O1, Trp(TargetType.Self, TrapTrigger.AfterTurnSkipped, 0, new DrawAtTurnStartEffect { subject = EffectSubject.Self, count = 2 }));
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SkipTurnEffect { subject = EffectSubject.Target })), t.O1);
            TrapActivated trap = t.RunUntil<TrapActivated>(80);
            bool fired = trap != null && trap.Actor == t.O1;
            int hand = t.HandCount(t.O1);
            t.RunUntilTurn(t.O1, 200);
            Check(fired && t.HandCount(t.O1) == hand + 2, "T15 턴 스킵된 후 → 다음 실제 턴 시작에 +2장");
        }
        {
            // T17 — 사망 대기 중 HP N 으로 생존 (손패 · 다른 함정 버림)
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            Card t17 = t.SetTrapCard(t.O1, Trp(TargetType.Self, TrapTrigger.SelfDeathPending, 0, new SurviveAtEffect { hp = 10, now = true, discardHandAndTraps = true }));
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            var events = t.Run(80);
            bool fired = events.OfType<TrapActivated>().Any(e => e.CardInstanceId == t17.InstanceId);
            Check(fired && t.Hp(t.O1) == 10 && t.HandCount(t.O1) == 0 && t.TrapCount(t.O1) == 0 && !events.OfType<PlayerDied>().Any(),
                "T17 HP 0 → 체인 끝에 자동 발동 · HP 10 생존 · 손패 · 다른 함정 버림 · 사망 없음");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 판 지표 집계 (07-win-condition.md 11-4) — 방장이 세는 가한 · 받은 피해, 회복, 킬, 빼앗은 · 빼앗긴 카드, 드로우, 행동 · 카운터 · 함정 사용, 카운터 성공, 찹츄 선언.
    /// 실제로 바뀐 만큼만 세는지, 세지 않기로 한 것(시작 손패 · 시작 HP · 손패 교환 · 대소동의 새 판)을 빼는지, 대소동을 넘어 이어지는지, 종료 요약에 실리는지 본다.
    /// </summary>
    // ── 방장 이주 (09-network 9.1, PM 2026-11-19) — 상태 담기 · 되살리기 · 이어 가기 ──
    // 되살린 상태가 원래와 같은지는 GameServer 의 필드를 리플렉션으로 모두 적어 견준다 (DumpState) — 새 원본 필드를 Capture · Restore 에 빠뜨리면 여기서 걸린다.
    public static string Migrate()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        double opens = ChainTable.Reveal;
        Card Choice(int id) => new Card(id, 1019, CardType.Action, TargetType.OneOther,
            new CardEffect[] { new DiscardOrDamageEffect { amount = 80, seconds = 5 } }, null, ChainTable.Reveal, ChainTable.Resolve);

        // 같은 난수 · 같은 시계로 되살린다 — 그 뒤 같은 요청을 주면 끝까지 같아야 한다
        GameServer Clone(ChainTable t, out bool restored, Func<double> clock = null)
        {
            GameSnapshot snapshot = t.Server.Capture();
            typeof(GameServer).GetField("_random", Private).SetValue(t.Server, new System.Random(7));
            var clone = new GameServer(new System.Random(7), clock ?? (() => t.Now));
            restored = clone.Restore(snapshot, CardsOf(t.Server));
            return clone;
        }
        void Same(ChainTable t, string what)
        {
            GameServer clone = Clone(t, out bool restored);
            string a = DumpState(t.Server), b = DumpState(clone);
            Check(restored && a == b, what + (restored ? a == b ? "" : " — 다른 곳: " + FirstDifference(a, b) : " — 되살리기 실패: " + clone.RestoreError));
        }

        // 1. 되살린 상태 = 원래 상태 (판의 여러 순간)
        {
            var t = new ChainTable();
            Same(t, "턴 시작 직후");

            t.GiveTrap(t.Turn, ChainTable.TrapLowHp);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.State(t.O2).SkipNextTurn = true;
            t.State(t.O1).TimeoutStreak = 2;
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            t.At(opens + 1);
            t.Server.SkipReaction(asked.First(a => a.Actor != t.O1).Actor);
            t.Server.HoldReaction(t.O1, asked.First(a => a.Actor == t.O1).CardInstanceIds[0]);
            Same(t, "반응 시간 중 — 체인 1장 · 스킵 1명 · 고민 연장 1명 · 함정 설치 · 턴 스킵 표시");

            t.Counter(t.O1, ChainTable.Negate);
            Same(t, "카운터가 오른 뒤 — 체인 2장 (아래 카드 연결)");

            // 이어서 끝까지 — 원래 방장과 되살린 방장이 같은 결과
            GameServer clone = Clone(t, out bool restored);
            double until = t.Now + opens + ReactionSeconds + ChainTable.Resolve + 1;
            for (double now = t.Now; now < until; now += 0.1)
            {
                t.At(now);
                t.Server.Tick();
                clone.Tick();
            }
            Check(restored && DumpState(t.Server) == DumpState(clone) && t.Hp(t.O1) == 100, "되살린 방장이 체인을 끝까지 처리 — 원래 방장과 같은 결과 (무효 → ♥100)"
                + (DumpState(t.Server) == DumpState(clone) ? "" : " — 다른 곳: " + FirstDifference(DumpState(t.Server), DumpState(clone))));
        }
        {
            var t = new ChainTable();
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Choice), t.O1);
            Same(t, "고르기 중 (1019) — 처리 중인 카드 · 고르는 사람");
            {
                // 아래 카드 표시 「피해 감소 (모두)」 · 「처리 직후 회복」 (2001 침착) 이 남은 채 고르기로 멈춘 순간
                var m = new ChainTable();
                Card calm = m.Give(m.O1, id => new Card(id, 2001, CardType.Counter, TargetType.None,
                    new CardEffect[] { new ReduceDamageEffect { amount = 20, everyone = true }, new HealAfterBelowEffect { amount = 10 } }, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve));
                m.PlayCard(m.Turn, m.Give(m.Turn, Choice), m.O1);
                m.At(2);
                m.Counter(m.O1, calm);
                Check(m.RunUntil<ChoiceStarted>(30) != null, "침착이 걸린 1019 — 고르기가 열림");
                Same(m, "고르기 중 — 아래 카드 표시 「피해 감소 (모두)」 · 「처리 직후 회복」 (2001)");
                GameServer copy = Clone(m, out _);
                copy.Choose(m.O1, 1);
                copy.Tick();
                int hp = ((PlayerState)((System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(copy))[m.O1]).Hp;
                Check(hp == 50, $"되살린 방장이 표시대로 처리 — 80 − 20 = 60 피해 뒤 10 회복 (♥{hp})");
            }
            {
                // 아래 카드 표시 「수치 2배」 (2005 묻고 따블로 가) 가 남은 채 고르기로 멈춘 순간
                var m = new ChainTable();
                Card Dbl(int id) => new Card(id, 2005, CardType.Counter, TargetType.None, new CardEffect[] { new NegateEffect(), new DoubleBelowEffect() }, new[] { CardType.Counter }, ChainTable.Reveal, ChainTable.Resolve);
                m.PlayCard(m.Turn, m.Give(m.Turn, Choice), m.O1);
                m.At(2);
                m.Counter(m.O2, ChainTable.Negate);
                m.At(2 + ChainTable.Reveal + 0.1);
                m.Counter(m.O1, m.Give(m.O1, Dbl));
                ChoiceStarted asked = m.RunUntil<ChoiceStarted>(30);
                Check(asked != null && asked.Damage == 160, $"2배가 걸린 1019 — 고르기가 열림 (피해 {asked?.Damage})");
                Same(m, "고르기 중 — 아래 카드 표시 「수치 2배」 (2005)");
                GameServer copy = Clone(m, out _);
                copy.Choose(m.O1, 1);
                Check(DumpHp(copy, m.O1) == 0, $"되살린 방장이 표시대로 처리 — 피해 160 (♥{DumpHp(copy, m.O1)})");

                // 2배가 된 대가 — 첫 장을 고르는 중 (한 번 더 남았다)
                var g = new ChainTable();
                g.SetHandSize(g.O1, 3);
                Card growl = g.Give(g.O1, id => new Card(id, 2021, CardType.Counter, TargetType.TriggerSource,
                    new CardEffect[] { new NegateEffect(), new DiscardEffect { subject = EffectSubject.Target, count = 2 }, new ChooseDiscardEffect { seconds = 5 } }, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve));
                g.PlayCard(g.Turn, g.Give(g.Turn, id => new Card(id, 1001, CardType.Action, TargetType.OneOther, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 20 } }, null, ChainTable.Reveal, ChainTable.Resolve)), g.O1);
                g.At(2);
                g.Counter(g.O1, growl);
                g.At(2 + (ChainTable.Reveal + 0.1));
                g.Counter(g.O2, g.Give(g.O2, Dbl));
                g.At(2 + (ChainTable.Reveal + 0.1) * 2);
                g.Counter(g.Turn, g.Give(g.Turn, Dbl));
                Check(g.RunUntil<ChoiceStarted>(40) != null, "2배가 된 으르렁 — 대가 고르기가 열림");
                Same(g, "고르기 중 — 2배가 된 대가의 첫 장 (한 번 더 남음)");
                GameServer copy2 = Clone(g, out _);
                Check(copy2.Choose(g.O1, g.Hand(g.O1)[0].InstanceId).OfType<ChoiceStarted>().Any(), "되살린 방장이 한 장 더 고르게 한다");
            }
            {
                // 아래 카드 표시 「손패 − 내 몫 무효」 (2019 안 놔!) 가 남은 채 고르기로 멈춘 순간 — 고르기 뒤의 빼앗기에서 내가 빠진다
                var m = new ChainTable();
                m.SetHandSize(m.O1, 3);
                Card hold = m.Give(m.O1, id => new Card(id, 2019, CardType.Counter, TargetType.None,
                    new CardEffect[] { new NegateHandLossEffect(), new DrawBlockedHandLossEffect { max = 3 } }, new[] { CardType.Action, CardType.Counter }, ChainTable.Reveal, ChainTable.Resolve, condition: ReactCondition.LosesMyHand));
                m.PlayCard(m.Turn, m.Give(m.Turn, id => new Card(id, 1900, CardType.Action, TargetType.OneOther,
                    new CardEffect[] { new DiscardOrDamageEffect { amount = 80, seconds = 5 }, new StealEffect { count = 1 } }, null, ChainTable.Reveal, ChainTable.Resolve)), m.O1);
                m.At(2);
                m.Counter(m.O1, hold);
                Check(m.RunUntil<ChoiceStarted>(30) != null && m.HandCount(m.O1) == 4, $"안 놔! 가 걸린 카드 — 1장 뽑은 뒤 고르기가 열림 (손패 {m.HandCount(m.O1)})");
                Same(m, "고르기 중 — 아래 카드 표시 「손패 − 내 몫 무효」 (2019)");
                GameServer copy = Clone(m, out _);
                copy.Choose(m.O1, 1);
                copy.Tick();
                Check(PlayersOf(copy)[m.O1].Hand.Count == 4 && DumpHp(copy, m.O1) == 20, $"되살린 방장이 표시대로 처리 — 피해 80 은 받고 빼앗기는 막힘 (손패 {PlayersOf(copy)[m.O1].Hand.Count} · ♥{DumpHp(copy, m.O1)})");
            }
            {
                // 아래 카드 표시 「피해 같이 받기」 (2018 물귀신) 가 남은 채 고르기로 멈춘 순간 — 표시가 물귀신 카드를 가리킨다
                var m = new ChainTable();
                Card Ghost(int id) => new Card(id, 2018, CardType.Counter, TargetType.OneNotDamaged, new CardEffect[] { new ShareDamageEffect() }, new[] { CardType.Action, CardType.Counter },
                    ChainTable.Reveal, ChainTable.Resolve, condition: ReactCondition.DamagesMe);
                m.PlayCard(m.Turn, m.Give(m.Turn, Choice), m.O1);
                m.At(2);
                m.Counter(m.O1, m.Give(m.O1, Ghost), m.O2);
                Check(m.RunUntil<ChoiceStarted>(30) != null, "물귀신이 걸린 1019 — 고르기가 열림");
                Same(m, "고르기 중 — 아래 카드 표시 「피해 같이 받기」 (2018)");
                GameServer copy = Clone(m, out _);
                copy.Choose(m.O1, 1);
                Check(DumpHp(copy, m.O1) == 20 && DumpHp(copy, m.O2) == 20, $"되살린 방장이 표시대로 처리 — 고른 사람도 80 (♥{DumpHp(copy, m.O1)} · ♥{DumpHp(copy, m.O2)})");
            }
            {
                // 반사(2002)한 사람이 다시 쓴 1019 가 고르기로 멈춘 순간 — 고르는 사람이 원래 쓴 사람이다. 같은 카드를 가리키는 체인 항목이 둘(원래 것 · 다시 쓴 것)이다
                var m = new ChainTable();
                Card Mirror(int id) => new Card(id, 2002, CardType.Counter, TargetType.None, new CardEffect[] { new ReflectEffect() }, new[] { CardType.Action },
                    ChainTable.Reveal, ChainTable.Resolve, condition: ReactCondition.AimedAtMe);
                m.PlayCard(m.Turn, m.Give(m.Turn, Choice), m.O1);
                m.At(2);
                m.Counter(m.O1, m.Give(m.O1, Mirror));
                ChoiceStarted asked = m.RunUntil<ChoiceStarted>(30);
                Check(asked != null && asked.Actor == m.Turn, $"반사가 걸린 1019 — 쓴 사람의 고르기가 열림 (고르는 사람 {asked?.Actor})");
                Same(m, "고르기 중 — 반사한 사람이 다시 쓴 카드 (2002)");
                GameServer copy = Clone(m, out _);
                copy.Choose(m.Turn, 1);
                Check(DumpHp(copy, m.Turn) == 20 && DumpHp(copy, m.O1) == 100, $"되살린 방장이 다시 쓴 카드를 이어서 처리 — 쓴 사람이 80 (♥{DumpHp(copy, m.Turn)} · ♥{DumpHp(copy, m.O1)})");
            }
            {
                // 턴 가로채기 (2013 새치기) 가 걸린 채 대가 고르기로 멈춘 순간 — 되살린 방장이 새치기 턴을 준다
                var m = new ChainTable();
                Card cut = m.Give(m.O2, id => new Card(id, 2013, CardType.Counter, TargetType.None,
                    new CardEffect[] { new NegateEffect(), new TakeTurnEffect(), new ChooseDiscardEffect { seconds = 5 } }, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve));
                m.PlayCard(m.Turn, m.Give(m.Turn, id => new Card(id, 1001, CardType.Action, TargetType.OneOther, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 20 } }, null, ChainTable.Reveal, ChainTable.Resolve)), m.O1);
                m.At(2);
                m.Counter(m.O2, cut);
                Check(m.RunUntil<ChoiceStarted>(30) != null, "새치기의 대가 고르기가 열림 (2013)");
                Same(m, "고르기 중 — 턴 가로채기가 걸림 (2013)");
                GameServer copy = Clone(m, out _);
                var after = copy.Choose(m.O2, m.Hand(m.O2)[0].InstanceId).ToList();
                for (double end = m.Now + 15; m.Now < end && !after.OfType<TurnStarted>().Any(); m.At(m.Now + 0.1))
                    after.AddRange(copy.Tick());
                TurnStarted next = after.OfType<TurnStarted>().FirstOrDefault();
                Check(next != null && next.Actor == m.O2, $"되살린 방장이 새치기 턴을 준다 (다음 턴 P{next?.Actor} · 기대 P{m.O2})");
            }
            {
                // 손패에서 1장 고르기 (2013 · 2021 의 대가) 로 멈춘 순간
                var m = new ChainTable();
                m.SetHandSize(m.O1, 2);
                Card growl = m.Give(m.O1, id => new Card(id, 2021, CardType.Counter, TargetType.TriggerSource,
                    new CardEffect[] { new NegateEffect(), new DiscardEffect { subject = EffectSubject.Target, count = 2 }, new ChooseDiscardEffect { seconds = 5 } }, new[] { CardType.Action }, ChainTable.Reveal, ChainTable.Resolve));
                m.PlayCard(m.Turn, m.Give(m.Turn, id => new Card(id, 1001, CardType.Action, TargetType.OneOther, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 20 } }, null, ChainTable.Reveal, ChainTable.Resolve)), m.O1);
                m.At(2);
                m.Counter(m.O1, growl);
                ChoiceStarted asked = m.RunUntil<ChoiceStarted>(30);
                Check(asked != null && asked.Kind == ChoiceKind.HandCard, "손패 고르기가 열림 (2021)");
                Same(m, "고르기 중 — 손패에서 1장 (2021 의 대가)");
                GameServer copy = Clone(m, out _);
                int drop = m.Hand(m.O1)[0].InstanceId;
                var after = copy.Choose(m.O1, drop);
                Check(after.OfType<CardsDiscarded>().Any(d => d.Actor == m.O1 && d.CardInstanceIds.SequenceEqual(new[] { drop })), "되살린 방장이 고른 카드를 버린다");
            }

            GameServer clone = Clone(t, out _);
            clone.Choose(t.O1, 1);
            Check(DumpHp(clone, t.O1) == 20, "되살린 방장에게 고르기 → 피해 80 (♥20)");
        }
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            t.PlayAndResolve(t.Turn, t.Find2(t.Turn, ChainTable.Hit), t.O1);
            t.Run(t.Now + ChainTable.Resolve + 0.2);
            Check(t.State(t.O1).Life == LifeState.DeathPending, "사망 대기까지 진행");
            Same(t, "사망 대기 중 — 버림 더미 · 「~한 후」 기록 · 지표");
        }

        // 2. 새 방장이 이어 간다 — 멈췄던 단계의 시간을 처음부터 (ResumeAfterMigration)
        {
            // 반응 시간: 이주에 10초가 걸려도 5초를 새로 준다 · 스킵은 유지 · 끝나면 원래대로 처리
            var t = new ChainTable();
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            int skipper = asked.First(a => a.Actor != t.O1).Actor;
            t.At(opens + 4);
            t.Server.SkipReaction(skipper);
            GameServer clone = Clone(t, out _);
            int[] deckBefore = DeckIds(clone);
            t.At(opens + 14);
            IReadOnlyList<GameEvent> resumed = clone.ResumeAfterMigration();
            Check(!resumed.OfType<CardResolved>().Any() && !clone.Tick().OfType<CardResolved>().Any(), "반응 시간 중 이주 — 마감(5초)이 지났어도 바로 처리하지 않는다");
            Check(!resumed.OfType<TurnResumed>().Any(), "메인 행동을 한 턴 — 턴 시간을 다시 알리지 않는다");
            int[] deckAfter = DeckIds(clone);
            Check(deckBefore.OrderBy(x => x).SequenceEqual(deckAfter.OrderBy(x => x)) && !deckBefore.SequenceEqual(deckAfter), "덱을 다시 섞는다 — 같은 카드 · 다른 순서");
            int skipCard = t.Hand(skipper).First(c => c.CardId == ChainTable.Negate).InstanceId;
            Check(t.Code(clone.PlayCounter(skipper, skipCard, new int[0])) == RejectCode.NotReactionTime, "스킵 기록 유지 — 스킵한 사람의 카운터 → 6");
            t.At(opens + 14 + ReactionSeconds - 0.1);
            Check(!clone.Tick().OfType<CardResolved>().Any(), "새 반응 시간 4.9초 — 아직");
            t.At(opens + 14 + ReactionSeconds);
            Check(clone.Tick().OfType<CardResolved>().Any() && DumpHp(clone, t.O1) == 80, "새 반응 시간 5초 → 처리 (♥80)");
        }
        {
            // 턴: 15초를 새로
            var t = new ChainTable();
            t.At(10);
            t.Server.Tick();
            GameServer clone = Clone(t, out _);
            t.At(40);
            TurnResumed turn = clone.ResumeAfterMigration().OfType<TurnResumed>().FirstOrDefault();
            Check(turn != null && turn.Actor == t.Turn && System.Math.Abs(turn.Deadline - 55) < 0.01, "턴 중 이주 — 턴 주인에게 15초를 새로 알린다");
            t.At(54.9);
            Check(clone.CurrentTurnActor == t.Turn && !clone.Tick().OfType<TurnStarted>().Any(), "새 마감 전 — 턴 유지");
            t.At(55.1);
            clone.Tick();
            Check(clone.CurrentTurnActor != t.Turn, "새 마감 → 턴 넘어감");
        }
        {
            // 사망 대기 · 고르기: 처음부터 다시
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            t.PlayAndResolve(t.Turn, t.Find2(t.Turn, ChainTable.Hit), t.O1);
            t.Run(t.Now + ChainTable.Resolve + 0.2);
            GameServer clone = Clone(t, out _);
            double at = t.Now + 30;
            t.At(at);
            DeathPending pending = clone.ResumeAfterMigration().OfType<DeathPending>().FirstOrDefault();
            Check(pending != null && pending.Actor == t.O1 && System.Math.Abs(pending.Deadline - (at + 5)) < 0.01, "사망 대기 중 이주 — 5초를 다시 센다");
            t.At(at + 4.9);
            Check(!clone.Tick().OfType<PlayerDied>().Any(), "4.9초 — 아직");
            t.At(at + 5);
            Check(clone.Tick().OfType<PlayerDied>().Any(), "5초 → 최종 사망");

            t = new ChainTable();
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Choice), t.O1);
            clone = Clone(t, out _);
            at = t.Now + 30;
            t.At(at);
            ChoiceStarted choice = clone.ResumeAfterMigration().OfType<ChoiceStarted>().FirstOrDefault();
            Check(choice != null && choice.Actor == t.O1 && System.Math.Abs(choice.Deadline - (at + 5)) < 0.01 && !clone.Tick().OfType<ChoiceMade>().Any(), "고르기 중 이주 — 고를 시간을 다시 준다");
            clone.Choose(t.O1, 0);
            Check(clone.Capture().Ints.Length > 0 && HandOf(clone, t.O1) == 0, "되살린 뒤 고르기 — 손패 전부 버리기");
        }

        // 3. 읽을 수 없는 스냅샷은 되살리지 않는다 — 새 방장은 판을 중지한다 (전송 층)
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            GameSnapshot good = t.Server.Capture();
            Card[] cards = CardsOf(t.Server);
            bool Bad(GameSnapshot snapshot, Card[] with) => !new GameServer(new System.Random(1), () => 0).Restore(snapshot, with);

            Check(!Bad(good, cards), "온전한 스냅샷 → 되살림");
            Check(Bad(new GameSnapshot(good.Ints.Take(good.Ints.Length - 3).ToArray(), good.Times), cards), "잘린 스냅샷 → 실패");
            Check(Bad(new GameSnapshot(good.Ints.Concat(new[] { 1 }).ToArray(), good.Times), cards), "값이 남는 스냅샷 → 실패");
            int[] wrongVersion = good.Ints.ToArray();
            wrongVersion[0] = 999;
            Check(Bad(new GameSnapshot(wrongVersion, good.Times), cards), "다른 버전 → 실패");
            Check(Bad(good, cards.Take(3).ToArray()), "이 덱에 없는 카드 → 실패");
            Check(Bad(new GameSnapshot(null, null), cards), "빈 스냅샷 → 실패");
        }

        // 4. 변경 번호 — 상태가 바뀔 때만 는다 (예비 방장에게 다시 보낼 때)
        {
            var t = new ChainTable();
            int before = t.Server.Revision;
            t.Server.Draw(t.O1); // 남의 턴 — 거절
            t.Server.Tick();
            Check(t.Server.Revision == before, "거절 · 변화 없는 Tick — 그대로");
            CountersUsable[] asked = t.Play(t.Turn, ChainTable.Hit, t.O1).OfType<CountersUsable>().ToArray();
            int played = t.Server.Revision;
            t.At(opens + 1);
            t.Server.SkipReaction(asked[0].Actor);
            Check(played > before && t.Server.Revision > played, "카드 사용 · 스킵(알림 없음) — 는다");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    private static int DumpHp(GameServer server, int actor) => PlayersOf(server)[actor].Hp;
    private static int HandOf(GameServer server, int actor) => PlayersOf(server)[actor].Hand.Count;
    private static Dictionary<int, PlayerState> PlayersOf(GameServer server) => (Dictionary<int, PlayerState>)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(server);
    private static int[] DeckIds(GameServer server) => ((List<Card>)typeof(GameServer).GetField(ServerDeckListField, Private).GetValue(server)).Select(c => c.InstanceId).ToArray();

    // 이 판에 있는 카드 전부 — 시험 판은 손으로 쥐여 준 카드가 섞여 있어 덱 에셋으로 만들 수 없다
    private static Card[] CardsOf(GameServer server)
    {
        var found = new Dictionary<int, Card>();
        void Visit(object value, int depth)
        {
            if (value == null || depth > 4 || value is string || value.GetType().IsPrimitive)
                return;
            if (value is Card card)
            {
                found[card.InstanceId] = card;
                return;
            }
            if (value is System.Collections.IDictionary map)
            {
                foreach (object item in map.Values)
                    Visit(item, depth + 1);
                return;
            }
            if (value is System.Collections.IEnumerable list)
            {
                foreach (object item in list)
                    Visit(item, depth + 1);
                return;
            }
            if (value.GetType().Assembly != typeof(GameServer).Assembly || value is CardEffect)
                return;
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                Visit(field.GetValue(value), depth + 1);
        }
        Visit(server, 0);
        return found.Values.ToArray();
    }

    // GameServer 의 원본 상태를 한 줄씩 적는다 — 필드 이름으로 찾지 않고 전부. 난수 · 시계 · 이번 요청의 알림 · 변경 번호는 상태가 아니다
    private static string DumpState(GameServer server)
    {
        var sb = new StringBuilder();
        foreach (FieldInfo field in typeof(GameServer).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(f => f.Name))
        {
            if (field.Name == "_random" || field.Name == "_clock" || field.Name == "_events" || field.Name.Contains("Revision") || field.Name.Contains("RestoreError"))
                continue;
            sb.Append(field.Name).Append(" = ").AppendLine(DumpValue(field.GetValue(server), 0));
        }
        return sb.ToString();
    }

    private static string DumpValue(object value, int depth)
    {
        if (value == null) return "null";
        if (value is double d) return d.ToString("R");
        if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return value.ToString();
        if (value is Card card) return "#" + card.InstanceId;
        if (value is CardEffect effect) return effect.GetType().Name;
        if (depth > 5) return "…";
        if (value is System.Collections.IDictionary map)
        {
            var pairs = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in map)
                pairs.Add(DumpValue(entry.Key, depth + 1) + ":" + DumpValue(entry.Value, depth + 1));
            return "{" + string.Join(", ", pairs.OrderBy(x => x, StringComparer.Ordinal)) + "}";
        }
        if (value is System.Collections.IEnumerable list)
        {
            var items = new List<string>();
            foreach (object item in list)
                items.Add(DumpValue(item, depth + 1));
            Type type = value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
                items.Sort(StringComparer.Ordinal); // 집합은 순서가 뜻이 없다
            return "[" + string.Join(" ", items) + "]";
        }

        // PlayerState · ChainEntry · AfterRecord — 필드를 모두. 체인의 「아래 카드」 는 그 카드만 적는다 (서로 물고 돌지 않게)
        var fields = new List<string>();
        foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(f => f.Name))
        {
            object inner = field.GetValue(value);
            fields.Add(field.Name + "=" + (inner is ChainEntryMarker ? "" : inner != null && inner.GetType() == value.GetType() ? "→" + DumpCardOf(inner) : DumpValue(inner, depth + 1)));
        }
        return "(" + string.Join(" ", fields) + ")";
    }

    private sealed class ChainEntryMarker { }

    // 같은 형의 안쪽 값(체인의 아래 카드)은 그 카드 번호로만
    private static string DumpCardOf(object entry)
    {
        PropertyInfo card = entry.GetType().GetProperty("Card");
        return card == null ? "?" : DumpValue(card.GetValue(entry), 9);
    }

    private static string FirstDifference(string a, string b)
    {
        string[] linesA = a.Split('\n'), linesB = b.Split('\n');
        for (int i = 0; i < Math.Min(linesA.Length, linesB.Length); i++)
        {
            if (linesA[i] != linesB[i])
                return "원래 「" + linesA[i].Trim() + "」 / 되살림 「" + linesB[i].Trim() + "」";
        }
        return "줄 수가 다르다";
    }

    public static string Stats()
    {
        var sb = new StringBuilder();
        bool failed = false;
        void Check(bool ok, string what)
        {
            failed |= !ok;
            sb.AppendLine((ok ? "  ✓ " : "FAIL ") + what);
        }

        FieldInfo statsField = typeof(GameServer).GetField(ServerStatsField, Private);
        int Stat(ChainTable t, int actor, StatKind kind) => ((Dictionary<int, int[]>)statsField.GetValue(t.Server))[actor][(int)kind];
        int Sum(ChainTable t, StatKind kind) => Stat(t, t.Turn, kind) + Stat(t, t.O1, kind) + Stat(t, t.O2, kind);
        Card Held(ChainTable t, int actor, int kind) => t.Hand(actor).First(c => c.CardId == kind);
        Func<int, Card> Act(TargetType target, params CardEffect[] e) => id => new Card(id, 900, CardType.Action, target, e, null, ChainTable.Reveal, ChainTable.Resolve);

        // 1. 피해 — 낸 사람의 가한 피해 · 맞은 사람의 받은 피해 · 행동 사용
        {
            var t = new ChainTable();
            Check(Enum.GetValues(typeof(StatKind)).Cast<StatKind>().All(kind => Sum(t, kind) == 0), "새 판 — 모든 지표 0 (시작 손패 · 시작 HP 는 세지 않는다)");
            t.PlayAndResolve(t.Turn, Held(t, t.Turn, ChainTable.Hit), t.O1);
            Check(Stat(t, t.Turn, StatKind.Dealt) == 20 && Stat(t, t.O1, StatKind.Taken) == 20 && Sum(t, StatKind.Dealt) == 20 && Sum(t, StatKind.Taken) == 20 && Stat(t, t.Turn, StatKind.Actions) == 1,
                "피해 20 → 낸 사람 가한 피해 20 · 맞은 사람 받은 피해 20 · 행동 사용 1");
        }

        // 2. 실제로 깎인 만큼만 · 킬은 최종 사망 때
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 5);
            t.PlayAndResolve(t.Turn, Held(t, t.Turn, ChainTable.Hit), t.O1);
            Check(Stat(t, t.Turn, StatKind.Dealt) == 5 && Stat(t, t.O1, StatKind.Taken) == 5, "HP 5 에 피해 20 → 실제로 깎인 5 만");
            Check(Sum(t, StatKind.Kills) == 0, "사망 대기 중엔 킬이 아니다");
            t.Run(t.Now + 8);
            Check(t.State(t.O1).Life == LifeState.Dead && Stat(t, t.Turn, StatKind.Kills) == 1 && Sum(t, StatKind.Kills) == 1, "최종 사망 → 당하게 한 사람 킬 1");
        }

        // 3. 자기 피해 — 받은 피해에만
        {
            var t = new ChainTable();
            t.PlayAndResolve(t.Turn, Held(t, t.Turn, ChainTable.Storm));
            Check(Stat(t, t.Turn, StatKind.Dealt) == 40 && Stat(t, t.Turn, StatKind.Taken) == 20 && Sum(t, StatKind.Taken) == 60, "전원 피해 20 → 가한 피해 40(남 둘) · 내가 받은 피해 20");
        }

        // 4. 회복 — 상한까지 실제로 찬 만큼
        {
            var t = new ChainTable();
            t.SetHp(t.Turn, 90);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.Self, new HealEffect { subject = EffectSubject.Self, amount = 30 })));
            Check(Stat(t, t.Turn, StatKind.Healed) == 10 && Sum(t, StatKind.Healed) == 10, "HP 90 에 회복 30 → 실제로 찬 10 만");
        }

        // 5. 빼앗은 · 빼앗긴 카드 — 훔침만. 손패 교환은 아니다
        {
            var t = new ChainTable();
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new StealEffect { count = 2 })), t.O1);
            Check(Stat(t, t.Turn, StatKind.Stolen) == 2 && Stat(t, t.O1, StatKind.Lost) == 2 && Sum(t, StatKind.Stolen) == 2 && Sum(t, StatKind.Lost) == 2, "훔침 2장 → 빼앗은 2 · 빼앗긴 2");
            var swap = new ChainTable();
            swap.PlayAndResolve(swap.Turn, swap.Give(swap.Turn, Act(TargetType.OneOther, new SwapHandEffect())), swap.O1);
            Check(Sum(swap, StatKind.Stolen) == 0 && Sum(swap, StatKind.Lost) == 0, "손패 교환은 빼앗은 · 빼앗긴 카드가 아니다");
        }

        // 6. 드로우 — 턴 뽑기 · 카드 효과로 실제 들어온 장수. 빈 덱은 0 이고 그 피해는 받은 피해에만
        {
            var t = new ChainTable();
            int before = t.HandCount(t.Turn);
            t.Server.Draw(t.Turn);
            int drew = t.HandCount(t.Turn) - before;
            Check(drew > 0 && Stat(t, t.Turn, StatKind.Drawn) == drew && Sum(t, StatKind.Drawn) == drew, "턴 뽑기 → 뽑은 장수 (" + drew + ")");

            var effect = new ChainTable();
            effect.PlayAndResolve(effect.Turn, effect.Give(effect.Turn, Act(TargetType.Self, new DrawEffect { subject = EffectSubject.Self, count = 2 })));
            Check(Stat(effect, effect.Turn, StatKind.Drawn) == 2, "카드 효과로 2장 → 드로우 2");

            var empty = new ChainTable();
            empty.EmptyDeck();
            empty.Server.Draw(empty.Turn);
            empty.Run(empty.Now + 3);
            int lost = 100 - empty.Hp(empty.Turn);
            Check(lost > 0 && Stat(empty, empty.Turn, StatKind.Drawn) == 0 && Stat(empty, empty.Turn, StatKind.Taken) == lost && Sum(empty, StatKind.Dealt) == 0,
                "빈 덱 — 드로우 0 · 빈 덱 피해 " + lost + " 은 받은 피해에만 (가한 사람 없음)");
        }

        // 7. 카운터 — 사용 · 성공. 무효된 카운터는 성공이 아니다
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(1.5);
            t.Counter(t.O1, ChainTable.Negate);
            t.At(100);
            t.Tick();
            Check(Stat(t, t.O1, StatKind.Counters) == 1 && Stat(t, t.O1, StatKind.CounterSuccess) == 1 && Stat(t, t.Turn, StatKind.Actions) == 1 && Sum(t, StatKind.Dealt) == 0,
                "카운터로 무효 → 카운터 사용 1 · 성공 1 · 무효된 행동 카드도 사용 1 · 피해 0");

            var twice = new ChainTable();
            twice.Play(twice.Turn, ChainTable.Hit, twice.O1);
            twice.At(1.5);
            twice.Counter(twice.O1, ChainTable.Negate);
            twice.At(1.5 + ChainTable.Reveal + 0.1);
            twice.Counter(twice.O2, ChainTable.Negate);
            twice.At(100);
            twice.Tick();
            Check(Stat(twice, twice.O1, StatKind.Counters) == 1 && Stat(twice, twice.O1, StatKind.CounterSuccess) == 0 && Stat(twice, twice.O2, StatKind.CounterSuccess) == 1
                  && Stat(twice, twice.Turn, StatKind.Dealt) == 20, "카운터의 카운터 → 무효된 카운터는 성공 0 · 무효한 카운터는 성공 1 · 피해는 들어감");
        }

        // 8. 함정 — 발동한 수. 함정 피해의 주인은 함정 주인
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            Check(Sum(t, StatKind.Traps) == 0, "설치만 한 함정은 세지 않는다");
            t.PlayAndResolve(t.Turn, Held(t, t.Turn, ChainTable.Hit), t.O1);
            t.Run(t.Now + 14);
            Check(Stat(t, t.O1, StatKind.Traps) == 1 && Sum(t, StatKind.Traps) == 1 && Stat(t, t.O1, StatKind.Dealt) == 10 && Stat(t, t.Turn, StatKind.Taken) == 10,
                "함정 발동 → 함정 사용 1 · 함정 피해 10 은 함정 주인의 가한 피해");
        }

        // 9. 찹츄 선언 — 자동 선언만 센다. 카드로 부여된 찹츄는 선언이 아니다
        {
            var t = new ChainTable();
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SetChapChuEffect { subject = EffectSubject.Target })), t.O1);
            Check(t.State(t.O1).IsChapchu && Sum(t, StatKind.ChapchuDeclared) == 0, "부여된 찹츄는 세지 않는다");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 8);
            t.Server.Draw(t.Turn);
            Check(Stat(t, t.Turn, StatKind.ChapchuDeclared) == 1 && Sum(t, StatKind.ChapchuDeclared) == 1, "뽑아 10장 → 찹츄 선언 1");
        }

        // 10. 112 대소동 — 새 판이 돼도 지표는 이어지고, 새 판의 HP 100 · 손패는 회복 · 드로우가 아니다
        {
            DeckData main = AssetDatabase.LoadAssetAtPath<DeckData>(MainDeckPath);
            Card big = main != null ? main.CreateCards().FirstOrDefault(c => c.CardId == 112) : null;
            Check(big != null, "Deck_Main 에 112 대소동이 있다");
            if (big != null)
            {
                var t = new ChainTable();
                t.PlayAndResolve(t.Turn, Held(t, t.Turn, ChainTable.Hit), t.O1);
                t.RunUntilTurn(t.O1, t.Now + 5);
                var events = t.PlayAndResolve(t.O1, t.Give(t.O1, id => new Card(id, big.CardId, big.Type, big.Target, big.Effects, big.ReactsTo, ChainTable.Reveal, ChainTable.Resolve,
                    big.Trigger, big.TriggerValue, big.Condition, big.TargetCondition)));
                events.AddRange(t.Run(t.Now + 3));
                Check(events.OfType<GameRestarted>().Any() && t.Hp(t.O1) == 100 && Stat(t, t.Turn, StatKind.Dealt) == 20 && Stat(t, t.O1, StatKind.Taken) == 20
                      && Stat(t, t.Turn, StatKind.Actions) == 1 && Stat(t, t.O1, StatKind.Actions) == 1, "112 대소동 — 새 판(HP 100)이 돼도 지표는 이어진다");
                Check(Sum(t, StatKind.Healed) == 0 && Sum(t, StatKind.Drawn) == 0, "새 판의 HP 100 · 다시 받은 손패는 회복 · 드로우가 아니다");
            }
        }

        // 11. 종료 요약 — 지표가 순위 순(Actors 와 같은 자리)으로 실린다
        {
            var t = new ChainTable();
            t.State(t.O1).IsChapchu = true;
            t.SetHandSize(t.O1, GameServer.ChapchuHandCount); // 턴 시작 판정은 정확히 10장일 때만 승리 (07 4.2)
            t.Play(t.Turn, ChainTable.Hit, t.O2);
            t.At(ChainTable.Reveal + ReactionSeconds);
            t.Tick();
            t.At(100);
            GameEnded ended = t.Tick().OfType<GameEnded>().FirstOrDefault();
            int n = GameServer.StatCount;
            int At(int actor, StatKind kind) => ended.Stats[ended.Actors.ToList().IndexOf(actor) * n + (int)kind];
            Check(ended != null && ended.Stats.Length == 3 * n && At(t.Turn, StatKind.Dealt) == 20 && At(t.O2, StatKind.Taken) == 20 && At(t.O1, StatKind.Dealt) == 0
                  && At(t.Turn, StatKind.Actions) == 1 && ended.EndTurn >= 1, "종료 요약 — 사람마다 지표 " + n + "개 · 끝난 턴");
            sb.AppendLine("  · " + Summary(ended));
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    // Chain 시나리오의 판 하나 — 3인, 시계를 직접 움직인다. 손패는 사람마다 모든 시험 카드 1장씩.
    // 게임 종료 요약 한 줄 — 순위 순으로 「순위:P번호 HP n · 탈락 턴 · 당한 사람 · 나감 · 지표(StatKind 순)」 + 끝난 턴
    private static string Summary(GameEnded ended) => ended == null
        ? "종료 없음"
        : string.Join(" / ", ended.Actors.Select((actor, i) => $"{ended.Ranks[i]}:P{actor} HP{ended.Hps[i]} T{ended.OutTurns[i]} by{ended.DownedBy[i]}{(ended.Left[i] ? " 나감" : "")}"
            + $" 지표[{string.Join(",", ended.Stats.Skip(i * GameServer.StatCount).Take(GameServer.StatCount))}]")) + $" · 끝난 턴 {ended.EndTurn}";

    private sealed class ChainTable
    {
        public const double Reveal = 1.46, Resolve = 1.28;
        public const int Hit = 1001, Wave = 1002, Storm = 1003, Negate = 2001, SelfOnly = 2002, TrapOnly = 2003;
        public const int TrapHit = 3001, TrapReact = 3002, TrapLowHp = 3003, TrapDying = 3004; // 손패에는 없다 — GiveCard · GiveTrap 으로

        public readonly GameServer Server;
        public readonly int Turn, O1, O2;
        private readonly int[] _order;
        private readonly System.Collections.IDictionary _players;
        private double _now;
        private int _nextInstance = 1000;

        public System.Collections.IDictionary States => _players;

        public ChainTable() : this(null)
        {
        }

        // clock 을 주면 그 시계를 쓴다 (봇 시험처럼 밖에서 시간을 돌릴 때). 그때 At() 은 쓰지 않는다
        // timeouts = 시간 초과 자동 뽑기 · 강제 퇴장을 켠다 (03-turn 4절). 기본은 끔 — 다른 시나리오는 턴을 시간 초과로 그냥 넘긴다 (봇과 같은 취급)
        public ChainTable(Func<double> clock, bool timeouts = false)
        {
            Server = new GameServer(new System.Random(1), clock ?? (() => _now));
            int[] actors = { 1, 2, 3 };
            var deck = Enumerable.Range(1, 30).Select(i => MakeCard(i, Hit)).ToList();
            Server.StartGame(actors, deck, timeouts ? null : actors);
            _players = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(Server);
            _order = ((List<int>)typeof(GameServer).GetField(ServerTurnOrderField, Private).GetValue(Server)).ToArray();

            foreach (int actor in actors)
            {
                List<Card> hand = ((PlayerState)_players[actor]).Hand;
                hand.Clear();
                foreach (int kind in new[] { Hit, Wave, Storm, Negate, SelfOnly, TrapOnly })
                    hand.Add(MakeCard(_nextInstance++, kind));
            }

            Turn = Server.CurrentTurnActor;
            O1 = NextOf(Turn);
            O2 = NextOf(O1);
        }

        public int NextOf(int actor) => _order[(Array.IndexOf(_order, actor) + 1) % _order.Length];
        public void At(double seconds) => _now = seconds;
        public IReadOnlyList<GameEvent> Tick() => Server.Tick();
        public int Hp(int actor) => ((PlayerState)_players[actor]).Hp;
        public void SetHp(int actor, int hp) => ((PlayerState)_players[actor]).Hp = hp;
        public int HandCount(int actor) => ((PlayerState)_players[actor]).Hand.Count;

        public void GiveCard(int actor, int kind) => ((PlayerState)_players[actor]).Hand.Add(MakeCard(_nextInstance++, kind));

        // 시험용 카드 (make = 인스턴스 ID → 카드)
        public Card Give(int actor, Func<int, Card> make) { Card c = make(_nextInstance++); ((PlayerState)_players[actor]).Hand.Add(c); return c; }
        public Card SetTrapCard(int actor, Func<int, Card> make) { Card c = make(_nextInstance++); ((PlayerState)_players[actor]).Traps.Add(c); RecordSetOrder(c); return c; }
        public PlayerState State(int actor) => (PlayerState)_players[actor];
        public List<Card> Hand(int actor) => State(actor).Hand;
        public int TrapCount(int actor) => State(actor).Traps.Count;

        public IReadOnlyList<GameEvent> PlayCard(int actor, Card card, params int[] targets) => Server.PlayCard(actor, card.InstanceId, targets);
        public IReadOnlyList<GameEvent> Counter(int actor, Card card, params int[] targets) => Server.PlayCounter(actor, card.InstanceId, targets);
        public double Now => _now;

        // 내고 반응 없이 끝까지 (체인 처리까지)
        public List<GameEvent> PlayAndResolve(int actor, Card card, params int[] targets)
        {
            var events = PlayCard(actor, card, targets).ToList();
            events.AddRange(Run(_now + Reveal + ReactionSeconds + 0.1));
            return events;
        }

        // 지금부터 until 초까지 0.1초씩 Tick
        public List<GameEvent> Run(double until)
        {
            var events = new List<GameEvent>();
            for (; _now < until; _now += 0.1)
                events.AddRange(Server.Tick());
            return events;
        }

        public T RunUntil<T>(double until) where T : GameEvent
        {
            for (; _now < until; _now += 0.1)
            {
                T found = Server.Tick().OfType<T>().FirstOrDefault();
                if (found != null)
                    return found;
            }
            return null;
        }

        public void RunUntilTurn(int actor, double until)
        {
            for (; _now < until && Server.CurrentTurnActor != actor; _now += 0.1)
                Server.Tick();
            Server.Tick();
        }
        public void GiveTrap(int actor, int kind)
        {
            Card c = MakeCard(_nextInstance++, kind);
            ((PlayerState)_players[actor]).Traps.Add(c);
            RecordSetOrder(c);
        }

        // SetTrap 처럼 설치 순서를 남긴다 — 여러 함정은 설치한 순서대로 발동 (04 6절)
        private void RecordSetOrder(Card c)
        {
            var order = (Dictionary<int, int>)typeof(GameServer).GetField(ServerTrapSetOrderField, Private).GetValue(Server);
            FieldInfo next = typeof(GameServer).GetField(ServerNextTrapSetOrderField, Private);
            int n = (int)next.GetValue(Server);
            order[c.InstanceId] = n;
            next.SetValue(Server, n + 1);
        }

        public IReadOnlyList<GameEvent> SetTrap(int actor, int kind) => Server.SetTrap(actor, Find(actor, kind));

        // 손패를 n 장으로 — 모자라면 Hit 을 더한다 (찹츄 시험)
        public List<Card> Discard => (List<Card>)typeof(GameServer).GetField(ServerDiscardField, Private).GetValue(Server);
        public Card PutInDiscard(int kind) { Card c = MakeCard(_nextInstance++, kind); Discard.Add(c); return c; }

        public void SetHandSize(int actor, int n)
        {
            List<Card> hand = ((PlayerState)_players[actor]).Hand;
            while (hand.Count > n) hand.RemoveAt(hand.Count - 1);
            while (hand.Count < n) hand.Add(MakeCard(_nextInstance++, Hit));
        }

        // 덱 · 버림 더미를 모두 비운다 (빈 덱 피해 시험)
        public void EmptyDeck()
        {
            ((List<Card>)typeof(GameServer).GetField(ServerDeckListField, Private).GetValue(Server)).Clear();
            ((List<Card>)typeof(GameServer).GetField(ServerDiscardField, Private).GetValue(Server)).Clear();
        }
        public int DiscardCount => ((List<Card>)typeof(GameServer).GetField(ServerDiscardField, Private).GetValue(Server)).Count;

        public IReadOnlyList<GameEvent> Play(int actor, int kind, int? target) => Server.PlayCard(actor, Find(actor, kind), target.HasValue ? new[] { target.Value } : new int[0]);
        public IReadOnlyList<GameEvent> Counter(int actor, int kind) => Server.PlayCounter(actor, Find(actor, kind), new int[0]);

        public int Code(IReadOnlyList<GameEvent> events) => events.Count == 1 && events[0] is RequestRejected r ? r.Code : 0;

        public Card Find2(int actor, int kind) => ((PlayerState)_players[actor]).Hand.Find(c => c.CardId == kind);

        private int Find(int actor, int kind)
        {
            Card card = ((PlayerState)_players[actor]).Hand.Find(c => c.CardId == kind);
            return card != null ? card.InstanceId : -1;
        }

        private static Card MakeCard(int instanceId, int kind)
        {
            switch (kind)
            {
                case Hit:      return new Card(instanceId, kind, CardType.Action, TargetType.OneOther, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 20 } }, null, Reveal, Resolve);
                case Wave:     return new Card(instanceId, kind, CardType.Action, TargetType.AllOthers, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 20 } }, null, Reveal, Resolve);
                case Storm:    return new Card(instanceId, kind, CardType.Action, TargetType.Everyone, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 20 } }, null, Reveal, Resolve);
                case Negate:   return new Card(instanceId, kind, CardType.Counter, TargetType.None, new CardEffect[] { new NegateEffect() }, new[] { CardType.Action, CardType.Counter }, Reveal, Resolve);
                case SelfOnly: return new Card(instanceId, kind, CardType.Counter, TargetType.None, new CardEffect[] { new NegateEffect { selfOnly = true } }, new[] { CardType.Action }, Reveal, Resolve);
                case TrapOnly: return new Card(instanceId, kind, CardType.Counter, TargetType.None, new CardEffect[] { new NegateEffect() }, new[] { CardType.Trap }, Reveal, Resolve);
                case TrapHit:  return new Card(instanceId, kind, CardType.Trap, TargetType.TriggerSource, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 10 } }, null, Reveal, Resolve, TrapTrigger.AfterDamaged);
                case TrapReact: return new Card(instanceId, kind, CardType.Trap, TargetType.TriggerSource, new CardEffect[] { new DamageEffect { subject = EffectSubject.Target, amount = 10 } }, new[] { CardType.Action }, Reveal, Resolve, TrapTrigger.ReactToCard);
                case TrapLowHp: return new Card(instanceId, kind, CardType.Trap, TargetType.Self, new CardEffect[0], null, Reveal, Resolve, TrapTrigger.HpAtMost, 20);
                default:       return new Card(instanceId, kind, CardType.Trap, TargetType.Self, new CardEffect[0], null, Reveal, Resolve, TrapTrigger.SelfDeathPending);
            }
        }
    }

    // ── 내부 ──

    private static void Call(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method, Private);
        if (info == null)
            throw new MissingMethodException(target.GetType().Name, method + " — 이름이 바뀌었으면 MultiTest.cs 의 표를 고친다");

        info.Invoke(target, null);
    }

    private static string ReadText(object target, string field)
    {
        string text = ((TMP_Text)target.GetType().GetField(field, Private).GetValue(target)).text;
        return System.Text.RegularExpressions.Regex.Replace(text, "</?nobr>", ""); // 줄 바꿈 막는 태그는 빼고 읽는다
    }

    private static bool TryGetTurnActor(out int actor)
    {
        actor = -1;
        if (PhotonNetwork.CurrentRoom == null || !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.TurnActor, out object value))
            return false;

        actor = (int)value;
        return true;
    }

    // 사람 + 봇 중 내 다음으로 살아 있는 사람 (봇은 사람 뒤 — 02-player.md 3절). 생존 상태는 방장이 쓴 공개 값으로 본다
    private static int NextActor(int me)
    {
        int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(a => a).Concat(BotRoster.Actors).ToArray();
        int index = Array.IndexOf(actors, me);
        for (int step = 1; step < actors.Length; step++)
        {
            int actor = actors[(index + step) % actors.Length];
            bool dead = BotRoster.TryGetPublic(actor, PlayerProps.LifeState, out object life) && life is byte b && b == (byte)LifeState.Dead;
            if (!dead)
                return actor;
        }

        return actors[(index + 1) % actors.Length];
    }

    // 규칙 계층만 돌릴 때 쓰는 덱. GameScene 의 PunGameServer 가 쓰는 덱과 같게 둔다.
    private static DeckData FindDeck()
    {
        return AssetDatabase.LoadAssetAtPath<DeckData>(RulesDeckPath);
    }

    private static bool IsActionWithEffect(DeckData deck, int cardId)
    {
        CardData data = deck != null ? deck.GetCard(cardId) : null;
        return data != null && data.type == CardType.Action && data.effects.Count > 0;
    }

    private static int ExpectedDamage(DeckData deck, int cardId)
    {
        return deck.GetCard(cardId).effects.OfType<Chapchu.Game.Effects.DamageEffect>()
            .Where(e => e.subject == Chapchu.Game.Effects.EffectSubject.Target).Sum(e => e.amount);
    }

    /// <summary>플레이 중인 게임 뷰를 PNG 로 찍는다 (끝 프레임에 쓰임 — 호출 뒤 파일이 생기길 기다린다). 화면 배치를 눈으로 볼 때.</summary>
    // 1021 개판 연출 — 시작 제목 · 자명종 · 마지막 바퀴 · 판정 팻말이 화면에 뜨는 순간을 에디터 안에서 지켜보다 찍는다 (CLI 부름 사이로는 순간을 놓친다).
    // prefix + "_title.png"(제목이 뜨고 1.6초) · "_hud.png"(자명종이 뜨고 0.6초) · "_hot.png"(마지막 바퀴 0.6초) · "_roll.png"(판정 1.3초 — 룰렛) · "_low.png"(판정 3.4초 — 꼴찌). 한 번씩.
    public static string ArmGaepanShots(string prefix)
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";
        var seen = new Dictionary<string, double>();
        var taken = new HashSet<string>();
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= tick;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            GaepanTitleView title = Object.FindFirstObjectByType<GaepanTitleView>(FindObjectsInactive.Include);
            GaepanHudView hud = Object.FindFirstObjectByType<GaepanHudView>(FindObjectsInactive.Include);
            GaepanJudgeView judge = Object.FindFirstObjectByType<GaepanJudgeView>(FindObjectsInactive.Include);
            bool hot = hud != null && hud.gameObject.activeInHierarchy
                       && ((TMP_Text)typeof(GaepanHudView).GetField(GaepanHudNumberField, Private).GetValue(hud)).text == "1";
            void Watch(string key, bool on, double after)
            {
                if (!on || taken.Contains(key)) return;
                if (!seen.ContainsKey(key)) seen[key] = now;
                if (now - seen[key] < after) return;
                ScreenCapture.CaptureScreenshot(prefix + "_" + key + ".png");
                taken.Add(key);
            }
            Watch("title", title != null && title.gameObject.activeInHierarchy, 1.6);
            Watch("hud", hud != null && hud.gameObject.activeInHierarchy && !hot, 0.6);
            Watch("hot", hot, 0.6);
            Watch("roll", judge != null && judge.gameObject.activeInHierarchy, 1.3);
            Watch("low", judge != null && judge.gameObject.activeInHierarchy, 3.4);
            if (taken.Count == 5) EditorApplication.update -= tick;
        };
        EditorApplication.update += tick;
        return "OK 개판 스크린샷 대기 → " + prefix + "_title · _hud · _hot · _roll · _low";
    }

    // 1021 자명종 — 보이면 「HUD n」(남은 바퀴), 없으면 「NONE」.
    public static string GaepanHud()
    {
        GaepanHudView hud = Object.FindFirstObjectByType<GaepanHudView>(FindObjectsInactive.Include);
        if (hud == null) return "WAIT 게임 화면 아님";
        if (!hud.gameObject.activeInHierarchy) return "OK NONE";
        return "OK HUD " + ((TMP_Text)typeof(GaepanHudView).GetField(GaepanHudNumberField, Private).GetValue(hud)).text;
    }

    public static string Shot(string path)
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        return "OK " + Screen.width + "x" + Screen.height + " → " + path;
    }

    /// <summary>
    /// 다음에 카드가 나오면(OnCardPlayed) 그 뒤 1.0초(3 단계 확대 중) · 2.6초(반응 시간 중) · 8초(multi 에서는 카운터가 끼어든 뒤의 반응 시간)에 게임 뷰를 찍는다 —
    /// prefix + "_zoom.png" · "_reaction.png" · "_later.png". 한 번만.
    /// 낸 카드 · 주인 태그 · 반응 타이머의 눈 확인용 (10-ui.md 2-1 「카드 연출 자리」).
    /// </summary>
    private static readonly double[] ShotTimes = { 1.0, 2.6, 8.0 };
    private static readonly string[] ShotNames = { "_zoom.png", "_reaction.png", "_later.png" };

    // ── 카드 연출 (연출 시험장 CardFxTestScene — run.sh fx). 게임 판 · 네트워크는 거치지 않는다 ──

    private const string FxSeenKey = "MultiTest.FxSeen";

    /// <summary>시험장에서 그 카드를 낸다 (CardFxTestBench.PlayCard). 시험장이 아직 준비 안 됐거나 다른 카드를 내는 중이면 WAIT.</summary>
    public static string FxPlay(string cardId)
    {
        if (!EditorApplication.isPlaying) return "WAIT 플레이 전";
        CardFxTestBench bench = Object.FindFirstObjectByType<CardFxTestBench>();
        if (bench == null) return "WAIT 연출 시험장 아님 (" + SceneManager.GetActiveScene().name + ")";
        return bench.PlayCard(int.Parse(cardId)) ? "OK 카드 " + cardId + " 냄" : "WAIT 준비 중 · 내는 중 · 덱에 없음";
    }

    /// <summary>
    /// 다음에 그 카드의 연출 3 단계가 시작되면(CardFxTestBench.PlayingCardId) 그 시작부터 ms 마다 게임 뷰를 찍는다 —
    /// prefix + "_0500.png" 처럼 (prefix "-" 면 찍지 않고 시작만 기록). msCsv = "300,800,1500". 3 단계가 끝난 뒤의 시각(4 단계)도 적을 수 있다. 한 번만.
    /// </summary>
    public static string ArmFxShots(string prefix, string cardId, string msCsv)
    {
        if (!EditorApplication.isPlaying) return "WAIT 플레이 전";
        CardFxTestBench bench = Object.FindFirstObjectByType<CardFxTestBench>();
        if (bench == null) return "WAIT 연출 시험장 아님";

        int id = int.Parse(cardId);
        int[] times = msCsv.Split(',').Select(t => int.Parse(t.Trim())).OrderBy(t => t).ToArray();
        double startedAt = -1;
        int taken = 0;
        SessionState.EraseString(FxSeenKey);
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!EditorApplication.isPlaying || bench == null)
            {
                EditorApplication.update -= tick;
                return;
            }

            if (startedAt < 0)
            {
                if (bench.PlayingCardId != id) return;
                startedAt = EditorApplication.timeSinceStartup - (Time.time - bench.PlayingSince) / Mathf.Max(0.01f, Time.timeScale);
                SessionState.SetString(FxSeenKey, cardId); // 짧은 3 단계는 CLI 로 물어보는 사이에 끝난다 — 본 것을 남긴다
            }

            if (taken >= times.Length)
            {
                EditorApplication.update -= tick;
                return;
            }

            if ((EditorApplication.timeSinceStartup - startedAt) * 1000 < times[taken] / Mathf.Max(0.01f, Time.timeScale)) return;
            if (prefix != "-")
                ScreenCapture.CaptureScreenshot(prefix + "_" + times[taken].ToString("0000") + ".png");
            taken++;
        };
        EditorApplication.update += tick;
        return "OK 연출 스크린샷 대기 → " + prefix + "_<ms>.png (" + msCsv + ")";
    }

    /// <summary>그 카드의 3 단계를 봤으면 SEEN (한 번), 카드를 내는 중이면 BUSY, 다 끝났으면 OK IDLE.</summary>
    public static string FxState(string cardId)
    {
        CardFxTestBench bench = Object.FindFirstObjectByType<CardFxTestBench>();
        if (bench == null) return "WAIT 연출 시험장 아님";
        if (SessionState.GetString(FxSeenKey, "") == cardId)
        {
            SessionState.EraseString(FxSeenKey);
            return "SEEN " + cardId;
        }
        return bench.IsBusy ? "BUSY" : "OK IDLE";
    }

    public static string ArmCardShots(string prefix)
    {
        if (!EditorApplication.isPlaying) return "FAIL 플레이 중이 아님";

        Action<int, int, int, int[], int, int> onPlayed = null;
        onPlayed = (actor, instanceId, cardId, targets, handCount, discardCount) =>
        {
            Chapchu.Network.Events.GameEvents.OnCardPlayed -= onPlayed;
            double startedAt = EditorApplication.timeSinceStartup;
            int taken = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                double elapsed = EditorApplication.timeSinceStartup - startedAt;
                if (taken >= ShotTimes.Length)
                {
                    EditorApplication.update -= tick;
                    return;
                }

                if (elapsed < ShotTimes[taken]) return;
                ScreenCapture.CaptureScreenshot(prefix + ShotNames[taken]);
                taken++;
            };
            EditorApplication.update += tick;
        };
        Chapchu.Network.Events.GameEvents.OnCardPlayed += onPlayed;
        return "OK 카드 스크린샷 대기 → " + prefix + "_zoom.png · _reaction.png · _later.png";
    }
}

// 시험이 고르기 요청을 「지금 열린 고르기의 답」 으로 보내게 한다 — GameServer.Choose 는 답하는 고르기의 마감을 함께 받는다 (앞 고르기에 보낸 늦은 요청 거절).
public static class MultiTestChoice
{
    public static double OpenChoiceDeadline(this GameServer server) =>
        (double)typeof(GameServer).GetField("_choiceDeadline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(server);

    public static IReadOnlyList<GameEvent> Choose(this GameServer server, int requester, int option) =>
        server.Choose(requester, option, server.OpenChoiceDeadline());
}
