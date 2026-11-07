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
    private const string MyTrapsField = "_myTraps";                        // GameDebugInput — 설치한 함정 인스턴스 ID
    private const string TrapWindowOpensField = "_trapWindowOpensAt";      // GameDebugInput — 함정 시간
    private const string TrapWindowDeadlineField = "_trapWindowDeadline";
    private const string HitByField = "_hitBy";                            // GameDebugInput — 마지막으로 나를 맞힌 사람
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
        if (SessionState.GetString(ActedTurnKey, "") == turnKey)
            return "SKIP P" + me + " 처리 중";
        SessionState.SetString(ActedTurnKey, turnKey);

        var hand = (List<int>)typeof(GameDebugInput).GetField(MyHandField, Private).GetValue(input);
        var instances = (List<int>)typeof(GameDebugInput).GetField(MyHandInstancesField, Private).GetValue(input);
        var deck = (DeckData)typeof(PunGameServer).GetField(ServerDeckField, Private).GetValue(server);

        // 함정은 메인 행동이 아니다 — 빈 칸이 있으면 손패의 함정 하나를 먼저 설치한다 (04-card.md 6절)
        var traps = (List<int>)typeof(GameDebugInput).GetField(MyTrapsField, Private).GetValue(input);
        int trapIndex = hand.FindIndex(cardId => deck.GetCard(cardId)?.type == CardType.Trap);
        string setLog = "";
        if (trapIndex >= 0 && traps.Count < 3)
        {
            server.RequestSetTrap(instances[trapIndex]);
            setLog = " · 함정 설치";
        }

        int index = hand.FindIndex(cardId => IsActionWithEffect(deck, cardId));

        if (index < 0)
        {
            server.RequestDraw();
            return "OK P" + me + setLog + " · 뽑기";
        }

        // 방장은 자기에게 보낸 RPC 를 그 자리에서 실행해 손패 목록이 바로 줄어든다 — 카드 번호는 요청 전에 읽는다
        int target = NextActor(me);
        int cardId = hand[index];
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

    /// <summary>
    /// 함정 시간에, 다른 사람에게 맞았고 설치한 함정이 있으면 발동한다 (04-card.md 6절 「피해 받은 후」). 아니면 SKIP.
    /// 덱의 테스트 함정(Card_T_005)이 「피해 받은 후 → 피해를 준 사람에게」 라 대상은 방장이 정한다.
    /// </summary>
    public static string TrapProbe()
    {
        GameDebugInput input = Object.FindFirstObjectByType<GameDebugInput>();
        PunGameServer server = Object.FindFirstObjectByType<PunGameServer>();
        if (input == null || server == null)
            return "SKIP 게임 화면 아님";

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        double opensAt = (double)typeof(GameDebugInput).GetField(TrapWindowOpensField, Private).GetValue(input);
        double deadline = (double)typeof(GameDebugInput).GetField(TrapWindowDeadlineField, Private).GetValue(input);
        FieldInfo hitByField = typeof(GameDebugInput).GetField(HitByField, Private);
        var traps = (List<int>)typeof(GameDebugInput).GetField(MyTrapsField, Private).GetValue(input);
        double now = PhotonNetwork.Time;
        if (now < opensAt || now >= deadline - 0.5 || (int)hitByField.GetValue(input) < 0 || traps.Count == 0)
            return "SKIP P" + me;

        hitByField.SetValue(input, -1);
        server.RequestActivateTrap(traps[0], new int[0]);
        return "OK P" + me + " 함정 시간에 함정 발동";
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

    public static string AddBot() => NetworkManager.Instance.AddBot() ? "OK 봇 추가 요청" : "FAIL 봇 추가 안 됨";

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

    /// <summary>규칙 계층의 함정 설치 · 발동 · 함정 시간 (04-card.md 6절, 기능 6). ChainTable 을 같이 쓴다.</summary>
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

        // 2. 피해 받은 후 — 조건 맞는 함정이 있으면 후처리 뒤 5초 함정 시간, 발동 → 피해를 준 사람에게
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.GiveTrap(t.O2, ChainTable.TrapHit);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + 5);
            TrapWindowStarted window = t.Tick().OfType<TrapWindowStarted>().FirstOrDefault();
            double opensAt = ChainTable.Reveal + 5 + ChainTable.Resolve;
            Check(window != null && Math.Abs(window.OpensAt - opensAt) < 1e-6 && Math.Abs(window.Deadline - (opensAt + 5)) < 1e-6, "피해 → 함정 시간 (후처리 뒤 5초)");
            t.At(opensAt - 0.5);
            Check(t.Code(t.Activate(t.O1, ChainTable.TrapHit)) == RejectCode.TrapConditionNotMet, "열리기 전 → 17");
            t.At(opensAt + 1);
            Check(!t.Tick().OfType<TurnStarted>().Any(), "함정 시간 중엔 다음 턴 없음");
            Check(t.Code(t.Activate(t.O2, ChainTable.TrapHit)) == RejectCode.TrapConditionNotMet, "피해 안 받은 사람 → 17");
            TrapActivated trap = t.Activate(t.O1, ChainTable.TrapHit).OfType<TrapActivated>().FirstOrDefault();
            Check(trap != null && trap.Targets.SequenceEqual(new[] { t.Turn }) && trap.TrapCount == 0, "발동 → 공개 · 대상 = 피해를 준 사람");
            t.At(opensAt + 1 + ChainTable.Reveal + 5);
            t.Tick();
            Check(t.Hp(t.Turn) == 90, "반격 피해 (♥90)");
            t.At(100);
            t.Tick();
            Check(t.Server.CurrentTurnActor == t.O1, "끝나고 다음 턴");
        }

        // 3. 함정 시간에 아무도 안 냄 → 닫히고 기회 사라짐 / 조건 맞는 함정이 없으면 열지 않음
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + 5);
            t.Tick();
            t.At(100);
            Check(t.Tick().OfType<TurnStarted>().Any(), "함정 시간 끝 → 다음 턴");
            Check(t.Code(t.Activate(t.O1, ChainTable.TrapHit)) == RejectCode.TrapConditionNotMet, "지난 피해로는 발동 못 함 → 17");
        }
        {
            var t = new ChainTable();
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + 5);
            Check(!t.Tick().OfType<TrapWindowStarted>().Any(), "함정이 없으면 함정 시간 없음");
        }

        // 4. 카드 반응 함정 — 반응 시간에, 체인 맨 위에서 먼저 처리
        {
            var t = new ChainTable();
            t.GiveTrap(t.O1, ChainTable.TrapReact);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(0.5);
            Check(t.Code(t.Activate(t.O1, ChainTable.TrapReact)) == RejectCode.NotReactionTime, "연출 중 → 6");
            t.At(2);
            Check(t.Activate(t.O1, ChainTable.TrapReact).OfType<TrapActivated>().Any(), "반응 시간에 발동");
            t.At(100);
            List<GameEvent> events = t.Tick().ToList();
            Check(t.Hp(t.Turn) == 90 && t.Hp(t.O1) == 80 && events.OfType<CardResolved>().Count() == 2, "함정 → 행동 카드 순으로 처리");
        }

        // 5. HP 이하 함정 — 남의 메인 행동 전에 발동하면 그 턴이 멈췄다가 남은 시간으로 이어진다
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 15);
            t.GiveTrap(t.O1, ChainTable.TrapLowHp);
            t.At(5);
            Check(t.Activate(t.O1, ChainTable.TrapLowHp).OfType<TrapActivated>().Any(), "HP 15 ≤ 20 → 언제든 발동 (새 체인)");
            Check(t.Code(t.Play(t.Turn, ChainTable.Hit, t.O2)) == RejectCode.Resolving, "끼어든 체인 중 메인 행동 → 18");
            t.At(5 + ChainTable.Reveal + 5);
            t.Tick();
            double resumeAt = 5 + ChainTable.Reveal + 5 + ChainTable.Resolve;
            t.At(resumeAt + 0.01);
            TurnResumed resumed = t.Tick().OfType<TurnResumed>().FirstOrDefault();
            Check(resumed != null && resumed.Actor == t.Turn && Math.Abs(resumed.Deadline - (20 + (resumeAt - 5))) < 0.05, "턴 이어짐 — 멈춘 만큼 마감이 밀림 (" + (resumed?.Deadline.ToString("0.00") ?? "-") + ")");
            Check(!(t.Play(t.Turn, ChainTable.Hit, t.O2)[0] is RequestRejected), "이어진 턴에 메인 행동 가능");
            t.SetHp(t.O2, 50);
            Check(t.Code(t.Activate(t.O2, ChainTable.TrapLowHp)) != 0, "HP 50 → 발동 못 함");
        }

        // 6. 사망 대기 — 사망 관련 함정만, 체인이 끝날 때까지 최종 사망을 미룬다
        {
            var t = new ChainTable();
            t.SetHp(t.O1, 20);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            t.GiveTrap(t.O1, ChainTable.TrapDying);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            t.At(ChainTable.Reveal + 5);
            DeathPending pending = t.Tick().OfType<DeathPending>().FirstOrDefault();
            t.At(pending.StartsAt + 1);
            Check(t.Code(t.Activate(t.O1, ChainTable.TrapHit)) == RejectCode.TrapConditionNotMet, "사망 대기 중 다른 함정 → 17");
            Check(t.Activate(t.O1, ChainTable.TrapDying).OfType<TrapActivated>().Any(), "본인 사망 대기 함정 발동");
            t.At(pending.Deadline + 0.1);
            Check(!t.Tick().OfType<PlayerDied>().Any(), "체인 중엔 최종 사망 보류");
            t.At(pending.StartsAt + 1 + ChainTable.Reveal + 5);
            Check(t.Tick().OfType<PlayerDied>().Any(), "체인 처리 뒤 최종 사망");
        }

        // 7. 나감 → 설치한 함정도 버림 더미로
        {
            var t = new ChainTable();
            t.GiveTrap(t.O2, ChainTable.TrapHit);
            t.GiveTrap(t.O2, ChainTable.TrapReact);
            int before = t.DiscardCount;
            PlayerLeft left = t.Server.RemovePlayer(t.O2).OfType<PlayerLeft>().FirstOrDefault();
            Check(left != null && left.DiscardCount == before + 6 + 2, "나감 → 손패 6 + 함정 2 버림 더미로");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 규칙 계층의 봇 (18-bot.md 7절). ① 봇끼리 기본 덱으로 여러 판을 끝까지 ② 찹츄 즉시 선언 ③ 나를 노린 카드에 카운터 ④ 맞은 뒤 함정. (거절 3회 상한은 봇이 미리 걸러 거절을 일부러 만들 수 없어 시험하지 않는다)
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
            server.StartGame(actors, deck.CreateCards());
            var states = (System.Collections.IDictionary)typeof(GameServer).GetField(ServerPlayersField, Private).GetValue(server);
            foreach (PlayerState state in states.Values)
                state.Hp = 40;

            var driver = new BotDriver(server, actors, new System.Random(game + 100), () => now);
            int turns = 0;
            while (!server.IsGameOver && now < 3000)
            {
                now += 0.1;
                turns += server.Tick().OfType<TurnStarted>().Count();
                turns += driver.Tick().OfType<TurnStarted>().Count();
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

        // ② 찹츄 — 손패가 10장이 되면 남의 턴에도 즉시 선언
        {
            var t = new ChainTable();
            var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => 0);
            t.SetHandSize(t.O1, 10);
            Check(driver.Tick().OfType<ChapchuChanged>().Any(e => e.Actor == t.O1 && e.IsChapchu), "손패 10장 → 즉시 찹츄 선언 (남의 턴)");
        }

        // ③ 카운터 — 나를 노린 카드에 1 ~ 3초 뒤. 남끼리의 카드에는 내지 않는다
        {
            double now = 0;
            var t = new ChainTable(() => now);
            var driver = new BotDriver(t.Server, new[] { t.O1, t.O2 }, new System.Random(1), () => now);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            var events = new List<GameEvent>();
            for (now = 0; now < ChainTable.Reveal + 4.9; now += 0.1)
                events.AddRange(driver.Tick());
            CardPlayed counter = events.OfType<CardPlayed>().FirstOrDefault();
            Check(counter != null && counter.Actor == t.O1, "나(O1)를 노린 카드 → O1 이 카운터");
            Check(counter != null && events.OfType<CardPlayed>().Count() == 1, "남(O2)은 끼지 않는다 · 한 창에 한 번");
        }

        // ④ 함정 — 맞은 뒤 함정 시간에 발동
        {
            double now = 0;
            var t = new ChainTable(() => now);
            t.GiveTrap(t.O1, ChainTable.TrapHit);
            ((PlayerState)t.States[t.O1]).Hand.RemoveAll(c => c.Type == CardType.Counter); // 카운터 없이 맞게
            var driver = new BotDriver(t.Server, new[] { t.O1 }, new System.Random(1), () => now);
            t.Play(t.Turn, ChainTable.Hit, t.O1);
            var events = new List<GameEvent>();
            for (now = 0; now < 30; now += 0.1)
            {
                events.AddRange(t.Tick());
                events.AddRange(driver.Tick());
            }
            TrapActivated trap = events.OfType<TrapActivated>().FirstOrDefault();
            Check(trap != null && trap.Actor == t.O1 && trap.Targets.SequenceEqual(new[] { t.Turn }) && t.Hp(t.Turn) == 90, "맞은 뒤 함정 시간에 반격 (♥90)");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
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
            t.Server.DeclareChapchu(t.O1);
            t.SetHandSize(t.O2, 10);
            t.PlayAndResolve(t.Turn, t.Give(t.Turn, Act(TargetType.TwoOthers, new MoveChapChuEffect())), t.O1, t.O2);
            Check(!t.State(t.O1).IsChapchu && t.State(t.O2).IsChapchu, "MoveChapChu 대상 1 → 대상 2");
        }
        {
            var t = new ChainTable();
            t.SetHandSize(t.O1, 10);
            t.Server.DeclareChapchu(t.O1);
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
            t.Run(20); // 처리 8.5 · 다음 턴 9.7 — O1 턴 20초 안
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
            t.Server.DeclareChapchu(t.O2);
            t.SetHandSize(t.O1, 10);
            Card c15 = t.Give(t.O1, Ctr(TargetType.None, actionTrap, ReactCondition.GivesMeChapchu, new NegateEffect { selfOnly = true }));
            t.SetHandSize(t.O1, 11); // 카운터를 내면 10장
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.TwoOthers, new MoveChapChuEffect())), t.O2, t.O1);
            t.At(2);
            Check(!(t.Counter(t.O1, c15)[0] is RequestRejected), "C15 조건 「나에게 찹츄 부여」 맞음");
            t.Run(20);
            Check(!t.State(t.O1).IsChapchu, "C15 나에게 오는 찹츄 무효");
        }
        {
            var t = new ChainTable();
            Func<int, Card> t05 = id => new Card(id, 905, CardType.Trap, TargetType.TriggerSource, new CardEffect[] { new NegateEffect(), new SkipTurnEffect { subject = EffectSubject.Target } }, action, ChainTable.Reveal, ChainTable.Resolve, TrapTrigger.ReactToCard, 0, ReactCondition.TargetsMe);
            Card mine = t.SetTrapCard(t.O1, t05), other = t.SetTrapCard(t.O2, t05);
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            t.At(2);
            Check(t.Code(t.Fire(t.O2, other)) == RejectCode.CannotReact, "T05 조건 「나를 대상으로」 — 대상 아닌 사람 → 9");
            t.Fire(t.O1, mine);
            t.Run(10);
            Check(t.Hp(t.O1) == 100 && t.State(t.Turn).SkipNextTurn, "T05 행동 카드 무효 + 시전자 턴 스킵");
        }

        // ── 「~한 후」 함정 ──
        void After(string name, Action<ChainTable> setup, Func<ChainTable, Card> trap, Action<ChainTable> act, Func<ChainTable, bool> result)
        {
            var t = new ChainTable();
            setup?.Invoke(t);
            Card card = trap(t);
            act(t);
            TrapWindowStarted window = t.RunUntil<TrapWindowStarted>(40);
            if (window == null)
            {
                Check(false, name + " — 함정 시간이 열리지 않았다");
                return;
            }

            t.At(window.OpensAt + 0.5);
            bool fired = t.Fire(t.O1, card).OfType<TrapActivated>().Any();
            t.Run(window.OpensAt + 20);
            Check(fired && result(t), name);
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
            t => t.Hp(t.Turn) == 90 || t.TrapCount(t.O1) == 0); // 무작위로 반격 함정 자신이 파괴되면 열리지 않는다 — 열렸다면 반격
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
            // T12 — 내 함정이 카운터로 무효된 뒤
            var t = new ChainTable();
            Card react = t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.ReactToCard, 0, Dmg(10)));
            Card t12 = t.SetTrapCard(t.O1, Trp(TargetType.TriggerSource, TrapTrigger.AfterMyTrapNegated, 0, new DiscardEffect { subject = EffectSubject.Target, count = 1 }));
            Card negate = t.Give(t.Turn, Ctr(TargetType.None, trapOnly, ReactCondition.Any, new NegateEffect()));
            t.PlayCard(t.Turn, t.Give(t.Turn, hit), t.O1);
            t.At(2);
            t.Fire(t.O1, react);
            t.At(4);
            t.Counter(t.Turn, negate);
            TrapWindowStarted window = t.RunUntil<TrapWindowStarted>(40);
            int hand = t.HandCount(t.Turn);
            bool fired = window != null && t.FireAt(window.OpensAt + 0.5, t.O1, t12);
            t.Run(80);
            Check(fired && t.HandCount(t.Turn) == hand - 1, "T12 내 함정 무효된 후 → 무효시킨 사람 손패 버림");
        }
        {
            // T15 — 턴 스킵된 뒤 → 다음 실제 턴에 추가 드로우
            var t = new ChainTable();
            Card t15 = t.SetTrapCard(t.O1, Trp(TargetType.Self, TrapTrigger.AfterTurnSkipped, 0, new DrawAtTurnStartEffect { subject = EffectSubject.Self, count = 2 }));
            t.PlayCard(t.Turn, t.Give(t.Turn, Act(TargetType.OneOther, new SkipTurnEffect { subject = EffectSubject.Target })), t.O1);
            TrapWindowStarted window = t.RunUntil<TrapWindowStarted>(40);
            bool fired = window != null && t.FireAt(window.OpensAt + 0.5, t.O1, t15);
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
            DeathPending pending = t.RunUntil<DeathPending>(40);
            bool fired = pending != null && t.FireAt(pending.StartsAt + 0.5, t.O1, t17);
            var events = t.Run(80);
            Check(fired && t.Hp(t.O1) == 10 && t.HandCount(t.O1) == 0 && t.TrapCount(t.O1) == 0 && events.OfType<DeathAvoided>().Any() && !events.OfType<PlayerDied>().Any(),
                "T17 사망 대기 → HP 10 생존 · 손패 · 함정 버림 · 사망 없음");
        }

        return (failed ? "FAIL\n" : "OK\n") + sb.ToString().TrimEnd();
    }

    // Chain 시나리오의 판 하나 — 3인, 시계를 직접 움직인다. 손패는 사람마다 모든 시험 카드 1장씩.
    private sealed class ChainTable
    {
        public const double Reveal = 1.46, Resolve = 1.28;
        public const int Hit = 101, Wave = 102, Storm = 103, Negate = 201, SelfOnly = 202, TrapOnly = 203;
        public const int TrapHit = 301, TrapReact = 302, TrapLowHp = 303, TrapDying = 304; // 손패에는 없다 — GiveCard · GiveTrap 으로

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
        public ChainTable(Func<double> clock)
        {
            Server = new GameServer(new System.Random(1), clock ?? (() => _now));
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

        public void GiveCard(int actor, int kind) => ((PlayerState)_players[actor]).Hand.Add(MakeCard(_nextInstance++, kind));

        // 시험용 카드 (make = 인스턴스 ID → 카드)
        public Card Give(int actor, Func<int, Card> make) { Card c = make(_nextInstance++); ((PlayerState)_players[actor]).Hand.Add(c); return c; }
        public Card SetTrapCard(int actor, Func<int, Card> make) { Card c = make(_nextInstance++); ((PlayerState)_players[actor]).Traps.Add(c); return c; }
        public PlayerState State(int actor) => (PlayerState)_players[actor];
        public List<Card> Hand(int actor) => State(actor).Hand;
        public int TrapCount(int actor) => State(actor).Traps.Count;

        public IReadOnlyList<GameEvent> PlayCard(int actor, Card card, params int[] targets) => Server.PlayCard(actor, card.InstanceId, targets);
        public IReadOnlyList<GameEvent> Counter(int actor, Card card, params int[] targets) => Server.PlayCounter(actor, card.InstanceId, targets);
        public IReadOnlyList<GameEvent> Fire(int actor, Card trap, params int[] targets) => Server.ActivateTrap(actor, trap.InstanceId, targets);
        public double Now => _now;
        public bool FireAt(double at, int actor, Card trap) { _now = at; return Fire(actor, trap).OfType<TrapActivated>().Any(); }

        // 내고 반응 없이 끝까지 (체인 처리까지)
        public List<GameEvent> PlayAndResolve(int actor, Card card, params int[] targets)
        {
            var events = PlayCard(actor, card, targets).ToList();
            events.AddRange(Run(_now + Reveal + 5.1));
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
        public void GiveTrap(int actor, int kind) => ((PlayerState)_players[actor]).Traps.Add(MakeCard(_nextInstance++, kind));

        public IReadOnlyList<GameEvent> SetTrap(int actor, int kind) => Server.SetTrap(actor, Find(actor, kind));

        public IReadOnlyList<GameEvent> Activate(int actor, int kind)
        {
            Card trap = ((PlayerState)_players[actor]).Traps.Find(c => c.CardId == kind);
            return Server.ActivateTrap(actor, trap != null ? trap.InstanceId : -1, new int[0]);
        }

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
}
