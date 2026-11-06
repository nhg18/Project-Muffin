using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Chapchu.Cards;
using Chapchu.DebugTools;
using Chapchu.Game;
using Chapchu.Game.Effects;
using Chapchu.Network;
using Chapchu.UI.Lobby;
using Chapchu.UI.Popups;
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
    private const string ConnectClickedMethod = "HandleConnectClicked";    // TitleView
    private const string JoinRoomRequestedMethod = "HandleJoinRoomRequested"; // LobbyPresenter
    private const string CreateRoomRequestedMethod = "HandleCreateRoomRequested"; // LobbyPresenter
    private const string SubmitClickedMethod = "HandleSubmitClicked";      // InputPopup
    private const string OkClickedMethod = "HandleOkClicked";              // WarningPopup
    private const string LeaveClickedMethod = "HandleLeaveClicked";        // RoomView
    private const string MyHandField = "_myHand";                          // GameDebugInput — 카드 ID
    private const string MyHandInstancesField = "_myHandInstances";        // GameDebugInput — 인스턴스 ID
    private const string GameEndedField = "_gameEnded";                    // GameDebugInput — 게임 종료를 받았다
    private const string PunServerField = "_server";                       // PunGameServer — 규칙 원본 (방장)
    private const string ServerDeckField = "deck";                         // PunGameServer — 게임에 쓰는 덱
    private const string ServerPlayersField = "_players";                  // GameServer — 플레이어 원본 상태
    private const string ServerTurnOrderField = "_turnOrder";              // GameServer — 턴 순서
    private const string ServerDiscardField = "_discardPile";              // GameServer — 버림 더미
    private const string ServerDeckListField = "_deck";                    // GameServer — 덱
    private const string RulesDeckPath = "Assets/Cards/Decks/Deck_Default.asset";

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // ActIfMyTurn 이 마지막으로 행동한 턴의 마감 시각 (에디터마다 따로). script.run 은 부를 때마다 새로 컴파일돼 static 이 남지 않아 SessionState 에 둔다
    private const string ActedTurnKey = "MultiTest.ActedTurnDeadline";

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

        if (!PhotonNetwork.IsConnectedAndReady)
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
        if (SessionState.GetString(ActedTurnKey, "") == turnKey)
            return "SKIP P" + me + " 처리 중";
        SessionState.SetString(ActedTurnKey, turnKey);

        var hand = (List<int>)typeof(GameDebugInput).GetField(MyHandField, Private).GetValue(input);
        var instances = (List<int>)typeof(GameDebugInput).GetField(MyHandInstancesField, Private).GetValue(input);
        var deck = (DeckData)typeof(PunGameServer).GetField(ServerDeckField, Private).GetValue(server);
        int index = hand.FindIndex(cardId => IsActionWithEffect(deck, cardId));

        if (index < 0)
        {
            server.RequestDraw();
            return "OK P" + me + " 뽑기";
        }

        // 방장은 자기에게 보낸 RPC 를 그 자리에서 실행해 손패 목록이 바로 줄어든다 — 카드 번호는 요청 전에 읽는다
        int target = NextActor(me);
        int cardId = hand[index];
        server.RequestPlayCard(instances[index], new[] { target });
        return "OK P" + me + " 카드 " + cardId + " → P" + target;
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
        server.RequestPlayCounter(instances[Math.Max(counter, 0)], new int[0]);
        return "OK P" + me + (counter >= 0 ? " 반응 시간에 카운터 (카운터 카드)" : " 반응 시간에 카운터 (행동 카드)");
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

    public static string Ui(string step)
    {
        string scene = SceneManager.GetActiveScene().name;
        switch (step)
        {
            case "title":
            {
                TitleView view = Object.FindFirstObjectByType<TitleView>();
                if (view == null)
                    return "WAIT 타이틀 아님 (" + scene + ")";

                view.SetNickname("테스터");
                Call(view, ConnectClickedMethod);
                return "OK 접속 요청";
            }
            case "bad-code":
            {
                LobbyPresenter presenter = Object.FindFirstObjectByType<LobbyPresenter>();
                if (presenter == null || !PhotonNetwork.IsConnectedAndReady)
                    return "WAIT 로비 아님 (" + scene + ")";

                Call(presenter, JoinRoomRequestedMethod);
                InputPopup popup = Object.FindFirstObjectByType<InputPopup>();
                if (popup == null)
                    return "FAIL 입력 팝업이 안 열림";

                ((TMP_InputField)typeof(InputPopup).GetField("inputField", Private).GetValue(popup)).text = "ZZZZ";
                Call(popup, SubmitClickedMethod);
                return "OK 없는 방 코드 ZZZZ 제출";
            }
            case "warning":
            {
                WarningPopup popup = Object.FindFirstObjectByType<WarningPopup>();
                if (popup == null)
                    return "WAIT 경고 팝업 없음";

                string text = ReadText(popup, "mainText") + " / " + ReadText(popup, "subText");
                Call(popup, OkClickedMethod);
                PopupManager.Instance.ShowToast("토스트 확인");
                return "OK 경고 \"" + text + "\" 확인 · 토스트 띄움";
            }
            case "create":
            {
                LobbyPresenter presenter = Object.FindFirstObjectByType<LobbyPresenter>();
                if (presenter == null)
                    return "WAIT 로비 아님 (" + scene + ")";

                Call(presenter, CreateRoomRequestedMethod);
                return "OK 방 만들기";
            }
            case "leave":
            {
                RoomView view = Object.FindFirstObjectByType<RoomView>();
                if (view == null)
                    return "WAIT 방 아님 (" + scene + ")";

                string log = ReadText(view, "logText");
                Call(view, LeaveClickedMethod);
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
            now += card.RevealSeconds + 5;
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
            t.At(ChainTable.Reveal + 4.9);
            Check(t.Tick().Count == 0 && t.Hp(t.O1) == 100, "마감 전 — 효과 없음");
            Check(t.Code(t.Server.Draw(t.Turn)) == RejectCode.MainActionUsed, "처리 중 뽑기 → 7");
            Check(t.Code(t.Play(t.Turn, ChainTable.Hit, t.O2)) == RejectCode.MainActionUsed, "처리 중 행동 카드 → 7");
            t.At(ChainTable.Reveal + 5);
            IReadOnlyList<GameEvent> resolved = t.Tick();
            Check(resolved.OfType<CardResolved>().Count() == 1 && t.Hp(t.O1) == 80 && !resolved.OfType<TurnStarted>().Any(), "마감 — 효과 적용 · 턴은 아직");
            t.At(ChainTable.Reveal + 5 + ChainTable.Resolve);
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
            Check(reaction != null && Math.Abs(reaction.OpensAt - (1.5 + ChainTable.Reveal)) < 1e-6 && Math.Abs(reaction.Deadline - (1.5 + ChainTable.Reveal + 5)) < 1e-6, "카운터 → 반응 시간 다시 (연출 뒤 5초)");
            t.At(ChainTable.Reveal + 5);
            Check(t.Tick().Count == 0, "처음 마감이 지나도 처리 안 함");
            t.At(1.5 + ChainTable.Reveal + 5);
            List<GameEvent> events = t.Tick().ToList();
            int resolvedAt = events.FindIndex(e => e is CardResolved);
            int negatedAt = events.FindIndex(e => e is CardNegated);
            Check(resolvedAt >= 0 && negatedAt > resolvedAt && t.Hp(t.O1) == 100, "위에서부터 — 카운터 처리 → 행동 카드 무효 · 피해 없음");
            Check(t.DiscardCount == 2, "무효된 카드도 버림 더미로");
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
            t.At(ChainTable.Reveal + 5);
            Check(t.Code(t.Counter(t.O1, ChainTable.Negate)) == RejectCode.NotReactionTime, "마감 시각 → 6");
        }

        // 6. 처리 중 턴 제한 시간은 멈춘다 — 19초에 내도 20초에 넘어가지 않는다 (03-turn.md 4절)
        {
            var t = new ChainTable();
            t.At(19);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(20.5);
            Check(!t.Tick().OfType<TurnStarted>().Any() && t.Server.CurrentTurnActor == t.Turn, "20초가 지나도 턴 유지");
            t.At(19 + ChainTable.Reveal + 5);
            t.Tick();
            Check(t.Hp(t.O1) == 80, "체인 처리");
        }

        // 7. 카드를 낸 사람이 나감 — 체인은 끝까지, 그 뒤 그 사람 다음 차례로
        {
            var t = new ChainTable();
            int user = t.Turn;
            t.Play(user, ChainTable.Hit, t.O1);
            Check(!t.Server.RemovePlayer(user).OfType<TurnStarted>().Any(), "나가도 처리 중엔 턴 유지");
            t.At(ChainTable.Reveal + 5);
            t.Tick();
            t.At(100);
            t.Tick();
            Check(t.Hp(t.O1) == 80 && t.Server.CurrentTurnActor == t.NextOf(user), "끝까지 처리 → 다음 차례 P" + t.Server.CurrentTurnActor);
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
            t.At(ChainTable.Reveal + 5);
            DeathPending pending = t.Tick().OfType<DeathPending>().FirstOrDefault();
            double startsAt = ChainTable.Reveal + 5 + ChainTable.Resolve;
            Check(pending != null && pending.Actor == t.O1 && Math.Abs(pending.StartsAt - startsAt) < 1e-6 && Math.Abs(pending.Deadline - (startsAt + 5)) < 1e-6,
                "HP 0 → 사망 대기 (피해 연출 뒤부터 5초)");
            t.At(startsAt + 1);
            Check(!t.Tick().OfType<TurnStarted>().Any(), "사망 대기 중엔 다음 턴 없음");
            int discardBefore = t.DiscardCount;
            t.At(startsAt + 5);
            List<GameEvent> events = t.Tick().ToList();
            PlayerDied died = events.OfType<PlayerDied>().FirstOrDefault();
            Check(died != null && died.Actor == t.O1 && t.HandCount(t.O1) == 0 && t.DiscardCount == discardBefore + 6, "마감 → 최종 사망 · 손패 6장 버림 더미로");
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
            t.At(ChainTable.Reveal + 5);
            DeathPending[] pendings = t.Tick().OfType<DeathPending>().ToArray();
            Check(pendings.Length == 2 && pendings[0].Deadline == pendings[1].Deadline, "동시 HP 0 → 둘 다 사망 대기 · 같은 마감");
            t.At(100);
            Check(t.Tick().OfType<PlayerDied>().Count() == 2, "함께 최종 사망");
        }

        // 3. 빈 덱 피해 1 · 2 · 3 … (사람마다), 드로우는 턴 종료
        {
            var t = new ChainTable();
            t.EmptyDeck();
            IReadOnlyList<GameEvent> first = t.Server.Draw(t.Turn);
            EmptyDeckDamaged damage = first.OfType<EmptyDeckDamaged>().FirstOrDefault();
            Check(damage != null && damage.Amount == 1 && t.Hp(t.Turn) == 99 && first.OfType<TurnStarted>().Any(), "빈 덱 첫 드로우 → 피해 1 · 턴 종료");
            t.Server.Draw(t.O1);
            t.Server.Draw(t.O2);
            t.Server.Draw(t.Turn);
            Check(t.Hp(t.Turn) == 97 && t.Hp(t.O1) == 99, "두 번째 → 피해 2 (사람마다 따로)");
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
            Check(left != null && left.DiscardCount == before + 6 && t.HandCount(t.O2) == 0, "나감 → 손패 6장 버림 더미로");
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

        // 1. 찹츄 선언 조건 → 다음 자기 턴 시작에 승리
        {
            var t = new ChainTable();
            Check(t.Code(t.Server.DeclareChapchu(t.O1)) == RejectCode.NotTenCards, "손패 5장 선언 → 11");
            t.SetHandSize(t.O1, 10);
            Check(t.Server.DeclareChapchu(t.O1).OfType<ChapchuChanged>().Any(e => e.IsChapchu), "10장 · 남의 턴에 선언 → 찹츄");
            Check(t.Code(t.Server.DeclareChapchu(t.O1)) == RejectCode.AlreadyChapchu, "다시 선언 → 12");
            t.Play(t.Turn, ChainTable.Hit, t.O2);
            t.At(ChainTable.Reveal + 5);
            t.Tick();
            t.At(100);
            List<GameEvent> events = t.Tick().ToList();
            GameEnded ended = events.OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.O1 && ended.Reason == GameEndReason.Chapchu && !events.OfType<TurnStarted>().Any(), "자기 턴이 오는 순간 찹츄 승리 (턴 시작 없음)");
            Check(t.Code(t.Server.Draw(t.O1)) == RejectCode.GameOver && t.Tick().Count == 0, "끝난 뒤 요청 → 13 · Tick 은 아무 일 없음");
        }

        // 2. 10장이 아니게 되면 즉시 해제 — 승리 없음
        {
            var t = new ChainTable();
            t.SetHandSize(t.Turn, 10);
            t.Server.DeclareChapchu(t.Turn);
            IReadOnlyList<GameEvent> played = t.Play(t.Turn, ChainTable.Hit, t.O1);
            Check(played.OfType<ChapchuChanged>().Any(e => !e.IsChapchu), "카드를 내 9장 → 즉시 해제");
            t.At(ChainTable.Reveal + 5);
            t.Tick();
            t.At(100);
            t.Tick();
            Check(!t.Server.IsGameOver && t.Server.CurrentTurnActor == t.O1, "해제된 사람은 이기지 않는다");
        }

        // 3. 처치 승리 — 마지막 1인, 사망 대기가 끝난 뒤
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 10);
            t.SetHp(t.O2, 10);
            t.Play(t.Turn, ChainTable.Wave, null);
            t.At(ChainTable.Reveal + 5);
            Check(!t.Tick().OfType<GameEnded>().Any(), "사망 대기 중엔 판정 안 함");
            t.At(100);
            List<GameEvent> events = t.Tick().ToList();
            GameEnded ended = events.OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.Turn && ended.Reason == GameEndReason.Elimination && !events.OfType<TurnStarted>().Any(), "남은 1인 처치 승리");
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
        }

        // 5. 나가서 1명 — 체인이 없으면 바로, 체인 중이면 처리 뒤
        {
            var t = new ChainTable();
            t.Server.RemovePlayer(t.O1);
            GameEnded ended = t.Server.RemovePlayer(t.O2).OfType<GameEnded>().FirstOrDefault();
            Check(ended != null && ended.Winner == t.Turn, "다른 사람이 모두 나감 → 바로 처치 승리");
        }
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.Server.RemovePlayer(t.O1);
            Check(!t.Server.RemovePlayer(t.O2).OfType<GameEnded>().Any(), "체인 중에 모두 나감 → 아직");
            t.At(ChainTable.Reveal + 5);
            Check(t.Tick().OfType<GameEnded>().Any(), "체인 처리 뒤 처치 승리");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    // Chain 시나리오의 판 하나 — 3인, 시계를 직접 움직인다. 손패는 사람마다 모든 시험 카드 1장씩.
    private sealed class ChainTable
    {
        public const double Reveal = 1.46, Resolve = 1.28;
        public const int Hit = 101, Wave = 102, Storm = 103, Negate = 201, SelfOnly = 202, TrapOnly = 203;

        public readonly GameServer Server;
        public readonly int Turn, O1, O2;
        private readonly int[] _order;
        private readonly System.Collections.IDictionary _players;
        private double _now;
        private int _nextInstance = 1000;

        public ChainTable()
        {
            Server = new GameServer(new System.Random(1), () => _now);
            int[] actors = { 1, 2, 3 };
            var deck = Enumerable.Range(1, 30).Select(i => MakeCard(i, Hit)).ToList();
            Server.StartGame(actors, deck);
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

        // 손패를 n 장으로 — 모자라면 Hit 을 더한다 (찹츄 시험)
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
                default:       return new Card(instanceId, kind, CardType.Counter, TargetType.None, new CardEffect[] { new NegateEffect() }, new[] { CardType.Trap }, Reveal, Resolve);
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
        return ((TMP_Text)target.GetType().GetField(field, Private).GetValue(target)).text;
    }

    private static bool TryGetTurnActor(out int actor)
    {
        actor = -1;
        if (PhotonNetwork.CurrentRoom == null || !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.TurnActor, out object value))
            return false;

        actor = (int)value;
        return true;
    }

    private static int NextActor(int me)
    {
        int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(a => a).ToArray();
        return actors[(Array.IndexOf(actors, me) + 1) % actors.Length];
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
}
