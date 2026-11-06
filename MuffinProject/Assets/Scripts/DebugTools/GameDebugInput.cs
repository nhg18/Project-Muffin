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
    /// 키: D = 뽑기, P = 손패 첫 카드를 다른 사람 1명에게, C = 손패 첫 카운터를 체인 맨 위에.
    /// </summary>
    public class GameDebugInput : MonoBehaviour
    {
        [SerializeField] private PunGameServer server;

        private readonly List<int> _myHand = new List<int>();                       // 내 손패 카드 ID (뽑은 순서) — P 키는 인스턴스 ID 를 쓴다
        private readonly List<int> _myHandInstances = new List<int>();
        private readonly Dictionary<int, int> _handCounts = new Dictionary<int, int>(); // actorNumber → 손패 장수
        private readonly Dictionary<int, int> _hps = new Dictionary<int, int>();        // actorNumber → 체력

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
            => Log($"P{actorNumber} 카드 처리 — 카드 {cardId} (#{cardInstanceId}) → {string.Join(", ", targets.Select(t => $"P{t}"))} · 버림 {discardCount}");

        private void HandleCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount)
            => Log($"P{actorNumber} 카드 무효 — 카드 {cardId} (#{cardInstanceId}) · 버림 {discardCount}");

        private void HandleMyRequestRejected(int code) => Log($"내 요청 거절 — 코드 {code}");

        private static void Log(string message) => Debug.Log($"[{nameof(GameDebugInput)}] {PhotonNetwork.NickName} (P{Me}) {message}");
    }
}
