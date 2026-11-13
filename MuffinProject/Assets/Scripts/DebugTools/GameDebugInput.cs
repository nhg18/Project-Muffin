using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 멀티 테스트용 키 입력과 결과 로그. UI 없이 키로 요청을 보내고, 이 클라이언트가 받은 결과를 콘솔에 찍는다. 판정은 하지 않는다.
    /// 턴이 시작될 때마다 "내 손패 · 모두의 손패 장수 · 체력" 한 줄을 찍는다 — 클라이언트마다 비교해 기능 1 · 2 를 확인한다.
    /// 키: D = 뽑기, P = 손패 첫 카드를 다른 사람 1명에게, C = 손패 첫 카운터를 체인 맨 위에, K = 찹츄 선언,
    /// T = 손패 첫 함정 설치, Y = 설치한 첫 함정 발동.
    /// </summary>
    public class GameDebugInput : MonoBehaviour
    {
        [SerializeField] private PunGameServer server;

        private readonly List<int> _myHand = new List<int>();                       // 내 손패 카드 ID (뽑은 순서) — P 키는 인스턴스 ID 를 쓴다
        private readonly List<int> _myHandInstances = new List<int>();
        private readonly Dictionary<int, int> _handCounts = new Dictionary<int, int>(); // actorNumber → 손패 장수
        private readonly Dictionary<int, int> _hps = new Dictionary<int, int>();        // actorNumber → 체력
        private bool _gameEnded;                                                        // 게임 종료를 받았다 — multi-test 가 읽는다
        private readonly List<int> _myTraps = new List<int>();                          // 내가 설치한 함정 인스턴스 ID
        private double _trapWindowOpensAt = -1;                                         // 함정 시간 (서버 시각) — multi-test 가 읽는다
        private double _trapWindowDeadline = -1;
        private int _hitBy = -1;                                                        // 마지막으로 나를 대상으로 처리된 카드를 낸 사람 — multi-test 가 읽는다

        private static int Me => PhotonNetwork.LocalPlayer.ActorNumber;

        private void Awake()
        {
            if (server == null)
                Debug.LogError($"[{nameof(GameDebugInput)}] {nameof(server)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            GameEvents.OnHpChanged += HandleHpChanged;
            GameEvents.OnCardDrawn += HandleCardDrawn;
            GameEvents.OnMyCardDrawn += HandleMyCardDrawn;
            GameEvents.OnTurnStarted += HandleTurnStarted;
            GameEvents.OnCardPlayed += HandleCardPlayed;
            GameEvents.OnReactionStarted += HandleReactionStarted;
            GameEvents.OnCardResolved += HandleCardResolved;
            GameEvents.OnCardNegated += HandleCardNegated;
            GameEvents.OnEmptyDeckDamaged += HandleEmptyDeckDamaged;
            GameEvents.OnDeathPending += HandleDeathPending;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnPlayerLeftGame += HandlePlayerLeftGame;
            GameEvents.OnTrapReturned += HandleTrapReturned;
            GameEvents.OnChapchuChanged += HandleChapchuChanged;
            GameEvents.OnMyTrapSet += HandleMyTrapSet;
            GameEvents.OnTrapSet += HandleTrapSet;
            GameEvents.OnTrapActivated += HandleTrapActivated;
            GameEvents.OnTrapWindowStarted += HandleTrapWindowStarted;
            GameEvents.OnTurnResumed += HandleTurnResumed;
            GameEvents.OnMyCardsGained += HandleMyCardsGained;
            GameEvents.OnMyCardsLost += HandleMyCardsLost;
            GameEvents.OnMyTrapRemoved += HandleMyTrapRemoved;
            GameEvents.OnHandCardsMoved += HandleHandCardsMoved;
            GameEvents.OnCardsDiscarded += HandleCardsDiscarded;
            GameEvents.OnCardReturnedToHand += HandleCardReturnedToHand;
            GameEvents.OnDiscardTaken += HandleDiscardTaken;
            GameEvents.OnTurnDirectionChanged += HandleTurnDirectionChanged;
            GameEvents.OnTrapDestroyed += HandleTrapDestroyed;
            GameEvents.OnTurnSkipped += HandleTurnSkipped;
            GameEvents.OnActionBlockChanged += HandleActionBlockChanged;
            GameEvents.OnWaitChanged += HandleWaitChanged;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnFalloutChanged += HandleFalloutChanged;
            GameEvents.OnChapchuWinHeld += HandleChapchuWinHeld;
            GameEvents.OnDeathAvoided += HandleDeathAvoided;
            GameEvents.OnMySync += HandleMySync;
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnMyRequestRejected += HandleMyRequestRejected;
        }

        private void OnDisable()
        {
            GameEvents.OnHpChanged -= HandleHpChanged;
            GameEvents.OnCardDrawn -= HandleCardDrawn;
            GameEvents.OnMyCardDrawn -= HandleMyCardDrawn;
            GameEvents.OnTurnStarted -= HandleTurnStarted;
            GameEvents.OnCardPlayed -= HandleCardPlayed;
            GameEvents.OnReactionStarted -= HandleReactionStarted;
            GameEvents.OnCardResolved -= HandleCardResolved;
            GameEvents.OnCardNegated -= HandleCardNegated;
            GameEvents.OnEmptyDeckDamaged -= HandleEmptyDeckDamaged;
            GameEvents.OnDeathPending -= HandleDeathPending;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnPlayerLeftGame -= HandlePlayerLeftGame;
            GameEvents.OnTrapReturned -= HandleTrapReturned;
            GameEvents.OnChapchuChanged -= HandleChapchuChanged;
            GameEvents.OnMyTrapSet -= HandleMyTrapSet;
            GameEvents.OnTrapSet -= HandleTrapSet;
            GameEvents.OnTrapActivated -= HandleTrapActivated;
            GameEvents.OnTrapWindowStarted -= HandleTrapWindowStarted;
            GameEvents.OnTurnResumed -= HandleTurnResumed;
            GameEvents.OnMyCardsGained -= HandleMyCardsGained;
            GameEvents.OnMyCardsLost -= HandleMyCardsLost;
            GameEvents.OnMyTrapRemoved -= HandleMyTrapRemoved;
            GameEvents.OnHandCardsMoved -= HandleHandCardsMoved;
            GameEvents.OnCardsDiscarded -= HandleCardsDiscarded;
            GameEvents.OnCardReturnedToHand -= HandleCardReturnedToHand;
            GameEvents.OnDiscardTaken -= HandleDiscardTaken;
            GameEvents.OnTurnDirectionChanged -= HandleTurnDirectionChanged;
            GameEvents.OnTrapDestroyed -= HandleTrapDestroyed;
            GameEvents.OnTurnSkipped -= HandleTurnSkipped;
            GameEvents.OnActionBlockChanged -= HandleActionBlockChanged;
            GameEvents.OnWaitChanged -= HandleWaitChanged;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnFalloutChanged -= HandleFalloutChanged;
            GameEvents.OnChapchuWinHeld -= HandleChapchuWinHeld;
            GameEvents.OnDeathAvoided -= HandleDeathAvoided;
            GameEvents.OnMySync -= HandleMySync;
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnMyRequestRejected -= HandleMyRequestRejected;
        }

        private void Update()
        {
            if (server == null)
                return;

            if (Input.GetKeyDown(KeyCode.D))
                server.RequestDraw();

            if (Input.GetKeyDown(KeyCode.P))
                PlayFirstCard();

            if (Input.GetKeyDown(KeyCode.C))
                PlayFirstCounter();

            if (Input.GetKeyDown(KeyCode.K))
                server.RequestDeclareChapchu();

            if (Input.GetKeyDown(KeyCode.T))
                SetFirstTrap();

            if (Input.GetKeyDown(KeyCode.Y))
                ActivateFirstTrap();
        }

        private void SetFirstTrap()
        {
            DeckData deck = server.Deck;
            int index = _myHand.FindIndex(cardId => deck != null && deck.GetCard(cardId)?.type == CardType.Trap);
            if (index < 0)
            {
                Log("설치할 함정이 없다");
                return;
            }

            server.RequestSetTrap(_myHandInstances[index]);
        }

        private void ActivateFirstTrap()
        {
            if (_myTraps.Count == 0)
            {
                Log("설치한 함정이 없다");
                return;
            }

            int target = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0].ActorNumber : Me;
            server.RequestActivateTrap(_myTraps[0], new[] { target });
        }

        private void PlayFirstCounter()
        {
            DeckData deck = server.Deck;
            int index = _myHand.FindIndex(cardId => deck != null && deck.GetCard(cardId)?.type == CardType.Counter);
            if (index < 0)
            {
                Log("낼 카운터가 없다");
                return;
            }

            int target = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0].ActorNumber : Me;
            server.RequestPlayCounter(_myHandInstances[index], new[] { target });
        }

        private void PlayFirstCard()
        {
            if (_myHandInstances.Count == 0)
            {
                Log("낼 카드가 없다");
                return;
            }

            // 대상이 자동인 카드면 방장이 대상을 무시한다
            int target = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0].ActorNumber : Me;
            server.RequestPlayCard(_myHandInstances[0], new[] { target });
        }

        private void HandleHpChanged(int actorNumber, int hp) => _hps[actorNumber] = hp;

        private void HandleCardDrawn(int actorNumber, int handCount, int deckCount) => _handCounts[actorNumber] = handCount;

        private void HandleMyCardDrawn(int cardInstanceId, int cardId)
        {
            _myHandInstances.Add(cardInstanceId);
            _myHand.Add(cardId);
        }

        private void HandleTurnStarted(int actorNumber, double deadline)
        {
            string players = string.Join(" │ ", _hps.Keys.OrderBy(a => a).Select(a =>
                $"P{a} 손{(_handCounts.TryGetValue(a, out int count) ? count : 0)} ♥{_hps[a]}"));

            Log($"턴 P{actorNumber} (남은 {deadline - PhotonNetwork.Time:0.0}초) · 내 손패 [{string.Join(" ", _myHand)}] · {players}");
        }

        private void HandleCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
        {
            _handCounts[actorNumber] = handCount;

            if (actorNumber == Me)
            {
                int index = _myHandInstances.IndexOf(cardInstanceId);
                if (index >= 0)
                {
                    _myHandInstances.RemoveAt(index);
                    _myHand.RemoveAt(index);
                }
            }

            Log($"P{actorNumber} 카드 사용 — 카드 {cardId} (#{cardInstanceId}) → {string.Join(", ", targets.Select(t => $"P{t}"))}");
        }

        private void HandleReactionStarted(double opensAt, double deadline)
            => Log($"반응 시간 — {opensAt - PhotonNetwork.Time:0.0}초 뒤 열림 · {deadline - PhotonNetwork.Time:0.0}초 뒤 마감");

        private void HandleCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount)
        {
            if (actorNumber != Me && targets.Contains(Me))
                _hitBy = actorNumber;

            Log($"P{actorNumber} 카드 처리 — 카드 {cardId} (#{cardInstanceId}) → {string.Join(", ", targets.Select(t => $"P{t}"))} · 버림 {discardCount}");
        }

        private void HandleCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount)
            => Log($"P{actorNumber} 카드 무효 — 카드 {cardId} (#{cardInstanceId}) · 버림 {discardCount}");

        private void HandleEmptyDeckDamaged(int actorNumber, int amount) => Log($"P{actorNumber} 빈 덱 피해 {amount}");

        private void HandleDeathPending(int actorNumber, double startsAt, double deadline)
            => Log($"P{actorNumber} 사망 대기 — {startsAt - PhotonNetwork.Time:0.0}초 뒤부터 {deadline - PhotonNetwork.Time:0.0}초 뒤까지");

        private void HandlePlayerDied(int actorNumber, int[] cardIds, int discardCount)
        {
            _handCounts[actorNumber] = 0;
            if (actorNumber == Me)
                ClearMyCards();
            Log($"P{actorNumber} 사망 · 버림 {discardCount} [{string.Join(" ", cardIds)}]");
        }

        private void HandlePlayerLeftGame(int actorNumber, int[] cardIds, int discardCount)
        {
            _handCounts[actorNumber] = 0;
            if (actorNumber == Me)
                ClearMyCards();
            Log($"P{actorNumber} 게임 중 나감 · 버림 {discardCount} [{string.Join(" ", cardIds)}]");
        }

        // 방장이 내 손패 · 함정을 0 으로 만들었다
        private void ClearMyCards()
        {
            _myHandInstances.Clear();
            _myHand.Clear();
            _myTraps.Clear();
            _hitBy = -1;
        }

        private void HandleTrapReturned(int owner, int trapCount, int handCount)
        {
            _handCounts[owner] = handCount;
            Log($"P{owner} 함정 손패로 반환 — 함정 {trapCount}장");
        }

        private void HandleMyTrapSet(int cardInstanceId, int cardId)
        {
            int index = _myHandInstances.IndexOf(cardInstanceId);
            if (index >= 0)
            {
                _myHandInstances.RemoveAt(index);
                _myHand.RemoveAt(index);
            }

            _myTraps.Add(cardInstanceId);
        }

        private void HandleTrapSet(int actorNumber, int handCount, int trapCount)
        {
            _handCounts[actorNumber] = handCount;
            Log($"P{actorNumber} 함정 설치 — 함정 {trapCount}장");
        }

        private void HandleTrapActivated(int actorNumber, int cardInstanceId, int cardId, int[] targets, int trapCount)
        {
            if (actorNumber == Me)
                _myTraps.Remove(cardInstanceId);

            Log($"P{actorNumber} 함정 발동 — 카드 {cardId} (#{cardInstanceId}) → {string.Join(", ", targets.Select(t => $"P{t}"))} · 함정 {trapCount}장");
        }

        private void HandleTrapWindowStarted(double opensAt, double deadline)
        {
            _trapWindowOpensAt = opensAt;
            _trapWindowDeadline = deadline;
            Log($"함정 시간 — {opensAt - PhotonNetwork.Time:0.0}초 뒤 열림 · {deadline - PhotonNetwork.Time:0.0}초 뒤 마감");
        }

        private void HandleMyCardsGained(int[] cardInstanceIds, int[] cardIds)
        {
            _myHandInstances.AddRange(cardInstanceIds);
            _myHand.AddRange(cardIds);
        }

        private void HandleMyCardsLost(int[] cardInstanceIds) => RemoveFromMyHand(cardInstanceIds);

        private void HandleMyTrapRemoved(int cardInstanceId) => _myTraps.Remove(cardInstanceId);

        private void HandleHandCardsMoved(int from, int to, int count, int fromHandCount, int toHandCount)
        {
            _handCounts[from] = fromHandCount;
            _handCounts[to] = toHandCount;
            Log($"P{from} → P{to} 손패 {count}장 이동");
        }

        private void HandleCardsDiscarded(int actorNumber, int[] cardInstanceIds, int[] cardIds, int handCount, int discardCount)
        {
            _handCounts[actorNumber] = handCount;
            if (actorNumber == Me)
                RemoveFromMyHand(cardInstanceIds);
            Log($"P{actorNumber} 손패 버림 [{string.Join(" ", cardIds)}] · 버림 {discardCount}");
        }

        private void HandleCardReturnedToHand(int actorNumber, int cardInstanceId, int cardId, int handCount)
        {
            _handCounts[actorNumber] = handCount;
            if (actorNumber == Me)
            {
                _myHandInstances.Add(cardInstanceId);
                _myHand.Add(cardId);
            }
            Log($"P{actorNumber} 카드 {cardId} 손패로 반환");
        }

        private void HandleDiscardTaken(int actorNumber, int cardInstanceId, int cardId, int handCount, int discardCount, int topCardId)
        {
            _handCounts[actorNumber] = handCount;
            if (actorNumber == Me)
            {
                _myHandInstances.Add(cardInstanceId);
                _myHand.Add(cardId);
            }
            Log($"P{actorNumber} 버림 더미에서 카드 {cardId} 가져옴 — 버림 {discardCount}장");
        }

        private void HandleTurnDirectionChanged(bool reversed, int extraTurn)
            => Log($"턴 방향 {(reversed ? "역방향" : "정방향")}{(extraTurn >= 0 ? $" — P{extraTurn} 다시 턴" : "")}");

        private void HandleTrapDestroyed(int owner, int cardInstanceId, int cardId, int trapCount, int discardCount)
        {
            if (owner == Me)
                _myTraps.Remove(cardInstanceId);
            Log($"P{owner} 함정 파괴 — 카드 {cardId} · 함정 {trapCount}장");
        }

        private void HandleTurnSkipped(int actorNumber) => Log($"P{actorNumber} 턴 스킵");

        private void HandleActionBlockChanged(int actorNumber, Chapchu.Game.ActionBlock state) => Log($"P{actorNumber} 행동 카드 금지 — {state}");

        private void HandleGameRestarted(int user, int count, int[] order)
        {
            ClearMyCards(); // 새 판 — 손패 · 함정은 처음부터 다시 나눈다 (11-card-list 112)
            Log($"대소동 — P{user} · 새 판 {count}번째 · 순서 {string.Join(" → ", System.Linq.Enumerable.Select(order, a => $"P{a}"))}");
        }

        private void HandleFalloutChanged(int n, int turns) => Log($"피폭 — n {n} · k {turns}{(turns > 0 ? $" · 모두 −{Chapchu.Game.GameServer.FalloutDamage(turns, n)}" : "")}");

        private void HandleWaitChanged(int[] casters) => Log($"기다려 — {(casters.Length > 0 ? string.Join(" ", System.Linq.Enumerable.Select(casters, a => $"P{a}")) : "끝")}");

        private void HandleChapchuWinHeld(int actorNumber) => Log($"P{actorNumber} 찹츄 판정 미룸 (기다려)");

        private void HandleDeathAvoided(int actorNumber, int hp) => Log($"P{actorNumber} 생존 — ♥{hp}");

        // 방장이 보낸 지금 내 카드와, 알림으로 따라온 내 기록을 비교한 뒤 방장 값으로 맞춘다
        private void HandleMySync(int[] handIds, int[] handCardIds, int[] trapIds, int[] trapCardIds)
        {
            bool same = handIds.OrderBy(i => i).SequenceEqual(_myHandInstances.OrderBy(i => i)) && trapIds.OrderBy(i => i).SequenceEqual(_myTraps.OrderBy(i => i));
            Log($"내 카드 동기화 — {(same ? "일치" : "불일치")} · 손패 {handIds.Length} · 함정 {trapIds.Length}");

            _myHandInstances.Clear();
            _myHandInstances.AddRange(handIds);
            _myHand.Clear();
            _myHand.AddRange(handCardIds);
            _myTraps.Clear();
            _myTraps.AddRange(trapIds);
        }

        private void RemoveFromMyHand(int[] cardInstanceIds)
        {
            foreach (int id in cardInstanceIds)
            {
                int index = _myHandInstances.IndexOf(id);
                if (index < 0)
                    continue;
                _myHandInstances.RemoveAt(index);
                _myHand.RemoveAt(index);
            }
        }

        private void HandleTurnResumed(int actorNumber, double deadline) => Log($"턴 이어짐 P{actorNumber} (남은 {deadline - PhotonNetwork.Time:0.0}초)");

        private void HandleChapchuChanged(int actorNumber, bool isChapchu) => Log($"P{actorNumber} 찹츄 {(isChapchu ? "선언" : "해제")}");

        private void HandleGameEnded(int winner, GameEndReason reason, double returnAt)
        {
            _gameEnded = true;
            Log($"게임 종료 — {reason} · 승자 P{winner} · {returnAt - PhotonNetwork.Time:0.0}초 뒤 방으로");
        }

        private void HandleMyRequestRejected(int code) => Log($"내 요청 거절 — 코드 {code}");

        private static void Log(string message) => Debug.Log($"[{nameof(GameDebugInput)}] {PhotonNetwork.NickName} (P{Me}) {message}");
    }
}
