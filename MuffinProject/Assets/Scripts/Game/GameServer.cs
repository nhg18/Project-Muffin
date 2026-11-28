using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    /// <summary>
    /// 게임 규칙의 원본. 방장 기기에서만 돌아가는 순수 C# 이다 (CODE_CONVENTION 9절 · 09-network.md).
    /// 요청 메서드는 검증 → 적용 → 일어난 일 목록을 돌려준다. 보내는 일은 PunGameServer 가 한다.
    /// 기능별 규칙은 GameServer.기능.cs (partial) 에 있다. 예: GameServer.Turn.cs
    /// </summary>
    public partial class GameServer
    {
        // ── 기능 추가하는 법 (예: 함정 설치) ─────────────────────────────────────────
        //
        // 1. 파일을 하나 만든다: GameServer.Trap.cs — namespace Chapchu.Game { public partial class GameServer { ... } }
        //    한 클래스라 다른 파일의 필드 · 메서드를 그대로 쓴다. 규칙을 별도 클래스로 떼지 않는다 — 상태를 두루 봐야 한다.
        //
        // 2. 원본 상태 필드는 그 기능의 partial 파일에 둔다. 플레이어별 값은 PlayerState 에 필드로.
        //    새 필드는 GameServer.Snapshot.cs 의 Capture · Restore 에 같은 자리로 더한다.
        //
        // 3. 요청 메서드는 requester 를 첫 인자로 받고, 검증 → 적용 → return TakeEvents() 순서다.
        //        public IReadOnlyList<GameEvent> SetTrap(int requester, int cardInstanceId)
        //        {
        //            if (requester != CurrentTurnActor) return Reject(requester, RejectCode.NotYourTurn); // 상태를 바꾸기 전에
        //            ... 원본 변경 ...
        //            _events.Add(new TrapSet(requester, ...));   // 새 종류의 일이면 GameEvent.cs 에 클래스 추가
        //            return TakeEvents();
        //        }
        //
        // 4. PunGameServer 에 요청 RPC 와, 새 일을 보내는 case 를 잇는다.
        //
        // 하지 말 것: UnityEngine · Photon 참조(어셈블리가 막는다) / 클라가 보낸 actorNumber 를 요청자로 믿기.
        // ─────────────────────────────────────────────────────────────────────────────

        // 확정 (06-health.md 2절).
        public const int MaxHp = 100;

        // ── 원본 상태 ──
        private readonly Random _random;
        private readonly Func<double> _clock; // 서버 시각(초). 턴 마감 계산에 쓴다 (09-network.md 8절)
        private readonly List<int> _turnOrder = new List<int>();
        private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>(); // actorNumber → 원본
        private readonly List<GameEvent> _events = new List<GameEvent>(); // 이번 요청에서 일어난 일. TakeEvents 가 비운다

        public int CurrentTurnActor { get; private set; } = -1;

        /// <summary>턴 순서 (정방향, 03-turn.md 6절) — 빠진 사람도 자리에 남는다. 화면에 알리는 값.</summary>
        public IReadOnlyList<int> TurnOrder => _turnOrder;

        public GameServer(Random random, Func<double> clock)
        {
            _random = random;
            _clock = clock;
        }

        /// <summary>
        /// 게임 시작 1회 (01-game-flow.md 3절 순서): 덱 셔플 → 체력 100 → 5장씩 → 턴 순서 무작위 · 첫 턴.
        /// deck 의 카드는 장마다 고유 InstanceId 를 가져야 한다 (09-network.md 7절).
        /// </summary>
        /// <param name="noTimeoutActors">시간 초과 자동 행동 · 강제 퇴장을 받지 않는 사람 — 봇 (03-turn.md 4절). 없으면 전원이 받는다.</param>
        /// <param name="openingSeconds">첫 턴이 멈춰 있는 초 — 화면의 게임 시작 연출 (<see cref="OpeningSeconds"/>). 시험은 0.</param>
        public IReadOnlyList<GameEvent> StartGame(IReadOnlyList<int> actors, IReadOnlyList<Card> deck, IEnumerable<int> noTimeoutActors = null, double openingSeconds = 0)
        {
            _noTimeoutActors.Clear();
            if (noTimeoutActors != null)
                _noTimeoutActors.UnionWith(noTimeoutActors);
            _players.Clear();
            _chain.Clear();
            _resolving.Clear();
            _chainOrder.Clear();
            _gameOver = false;
            _afterRecords.Clear();
            _claimedCards.Clear();
            _waitCasters.Clear();
            _allCards.Clear();
            _allCards.AddRange(deck);
            _restartCount = 0;
            _falloutTurns = 0;
            _restartPending = false;
            _heldNext = -1;
            _turnTaker = -1;
            _reversed = false; // 게임 시작은 정방향 (03-turn.md 6절 #4)
            _extraTurnActor = -1;
            TurnSerial = 0; // 턴 번호는 판마다 1부터 (07 11-3)
            ResetGaepan();
            ResetDomain();
            ClearChoice();
            _judgesElimination = actors.Count >= 2;
            foreach (int actor in actors)
                _players[actor] = new PlayerState();
            ResetStats(actors);

            InitDeck(deck);

            foreach (int actor in actors)
                SetHp(actor, MaxHp);

            DealInitialHands(actors);
            StartFirstTurn(openingSeconds);
            _events.Insert(0, new GameStarted(_turnOrder.ToArray(), openingSeconds)); // 시작 알림 묶음의 맨 앞 — 화면이 시작 연출을 먼저 줄 세운다
            return TakeEvents();
        }

        /// <summary>
        /// 첫 턴을 멈춰 두는 초 (01-game-flow.md 3절 #8 · 10-ui.md 5-2 「게임 시작 연출」 표) — 화면의 게임 시작 연출 + 시작 손패 배분이 끝날 때까지.
        /// 연출 3.1 + 0.48 × 인원 (섞기 → 번호 공개 → 「{이름}부터 시작!」 → 자리로 날아가기) + 배분 0.15 × 5장 × 인원 + 알림이 닿는 틈.
        /// 연출 값을 바꾸면 이 식도 같이 고친다 — GameStartIntroView 가 자기 시간표가 이보다 길면 경고한다.
        /// </summary>
        public static double OpeningSeconds(int players) => OpeningIntroSeconds(players) + OpeningDealSeconds * InitialHandCount * players + OpeningMargin;

        /// <summary>게임 시작 연출(섞기 ~ 자리 배치)의 길이 — 화면이 이 안에 마친다.</summary>
        public static double OpeningIntroSeconds(int players) => 3.1 + 0.48 * players;

        /// <summary>시작 손패 1장을 나눠 주는 간격 — HandPresenter · OpponentSeatsPresenter 의 dealInterval.</summary>
        public const double OpeningDealSeconds = 0.15;

        /// <summary>게임 시작 연출이 방장보다 늦게 시작하는 틈 — 화면 전환 덮개가 걷히는 시간(머묾 0.2 + 걷기 0.28, 15-screen 9-3) + 알림이 닿는 시간.</summary>
        public const double OpeningMargin = 0.8;

        /// <summary>actor 의 비공개 카드 — 손패 · 설치 함정 (09-network.md 10절 동기화). 그 사람에게만 보낸다.</summary>
        public void GetPrivateCards(int actor, out int[] handIds, out int[] handCardIds, out int[] trapIds, out int[] trapCardIds)
        {
            if (!_players.TryGetValue(actor, out PlayerState player))
            {
                handIds = handCardIds = trapIds = trapCardIds = Array.Empty<int>();
                return;
            }

            handIds = player.Hand.Select(c => c.InstanceId).ToArray();
            handCardIds = player.Hand.Select(c => c.CardId).ToArray();
            trapIds = player.Traps.Select(c => c.InstanceId).ToArray();
            trapCardIds = player.Traps.Select(c => c.CardId).ToArray();
        }

        /// <summary>
        /// 디버그 로그용 한 줄 요약 — 덱 · 버림, 사람마다 체력 · 손패[카드 ID]. 판정에 쓰지 않는다.
        /// 손패 카드 ID 는 비공개 정보다 — 출시 빌드 로그(Player.log)에는 남기지 않는다 (CODE_CONVENTION 7절). showHands = false 면 장수만.
        /// </summary>
        public string DebugState(bool showHands = true)
        {
            string players = string.Join(" │ ", _players.Select(p =>
                $"P{p.Key} ♥{p.Value.Hp} 손{p.Value.Hand.Count}{(showHands ? $"[{string.Join(" ", p.Value.Hand.Select(c => c.CardId))}]" : "")}{(p.Value.Traps.Count > 0 ? $" 함정{p.Value.Traps.Count}" : "")}{(p.Value.IsChapchu ? " 찹츄" : "")}{(p.Value.HasLeft ? " 나감" : p.Value.Life == LifeState.Dead ? " 사망" : p.Value.Life == LifeState.DeathPending ? " 사망대기" : "")}"));

            string chain = _chain.Count == 0 ? "" : $" · 체인 [{string.Join(" ← ", _chain.Select(e => $"P{e.User}:{e.Card.CardId}"))}]";
            if (IsResolvePaused)
                chain += $" · 이어 갈 처리 [{string.Join(" ", _resolving.Select(e => $"P{e.User}:{e.Card.CardId}#{e.NextEffect}"))}]";
            return $"덱 {_deck.Count} · 버림 {_discardPile.Count}{chain}{DebugStatus()} ║ {players}";
        }

        private IReadOnlyList<GameEvent> TakeEvents()
        {
            SettleChapchu(); // 요청 · Tick 하나가 끝날 때 남은 손패 변화를 마저 본다 (07 4.1)
            if (_events.Count == 0)
                return Array.Empty<GameEvent>(); // Tick 이 매 프레임 부르므로 빈 목록은 할당하지 않는다

            GameEvent[] taken = _events.ToArray();
            _events.Clear();
            if (taken.Any(e => !(e is RequestRejected)))
                Revision++; // 상태가 바뀌었다 — 예비 방장에게 다시 보낸다 (GameServer.Snapshot.cs)
            return taken;
        }

        // 거절은 상태를 바꾸지 않는다 — 검증 단계에서만 부른다.
        private IReadOnlyList<GameEvent> Reject(int requester, int code)
        {
            _events.Add(new RequestRejected(requester, code));
            return TakeEvents();
        }

        // 덱(GameServer.Deck.cs) · 턴 순서(GameServer.Turn.cs) 공용
        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
