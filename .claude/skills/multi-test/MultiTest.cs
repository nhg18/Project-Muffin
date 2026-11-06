using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Chapchu.Cards;
using Chapchu.DebugTools;
using Chapchu.Game;
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
    private const string ServerDeckField = "deck";                         // PunGameServer — 게임에 쓰는 덱
    private const string ServerPlayersField = "_players";                  // GameServer — 플레이어 원본 상태
    private const string RulesDeckPath = "Assets/Cards/Decks/Deck_Default.asset";

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

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
        if (turnActor != me)
            return "SKIP P" + me + " 남의 턴(P" + turnActor + ")";

        var hand = (List<int>)typeof(GameDebugInput).GetField(MyHandField, Private).GetValue(input);
        var instances = (List<int>)typeof(GameDebugInput).GetField(MyHandInstancesField, Private).GetValue(input);
        var deck = (DeckData)typeof(PunGameServer).GetField(ServerDeckField, Private).GetValue(server);
        int index = hand.FindIndex(cardId => IsActionWithEffect(deck, cardId));

        if (index < 0)
        {
            server.RequestDraw();
            return "OK P" + me + " 뽑기";
        }

        int target = NextActor(me);
        server.RequestPlayCard(instances[index], new[] { target });
        return "OK P" + me + " 카드 " + hand[index] + " → P" + target;
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
        var server = new GameServer(new System.Random(1), () => 0.0);
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
            int after = ((PlayerState)states[target]).Hp;
            int expected = ExpectedDamage(deck, card.CardId);

            bool ok = events.Any(e => e is CardPlayed) && before - after == Math.Min(expected, before);
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
