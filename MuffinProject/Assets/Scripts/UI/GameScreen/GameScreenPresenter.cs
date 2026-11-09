using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 인게임 화면의 연출 큐 (CODE_CONVENTION 9절 · 09-network.md 6.1). 방장이 보낸 일어난 일(GameEvents)을 받은 순서대로 줄 세우고,
    /// 하나씩 영역 프레젠터에 넘겨 연출이 끝난 뒤 다음을 꺼낸다. 화면 값은 재생하는 순간 이벤트에 담긴 값으로 바뀐다. 판정은 하지 않는다.
    /// 받는 순서 = 일어난 순서다 (GameServer 가 쌓은 순서대로 PunGameServer 가 보내고, 같은 보낸이의 RPC 는 순서가 지켜진다).
    /// 큐를 타지 않는 것: 내 요청 거절(RejectToastPresenter 가 바로 띄움) · 남은 초(TurnStripPresenter 가 매 프레임 계산) · 퇴장(RoomEvents) · 늦게 열렸을 때의 스냅샷 복구(각 프레젠터 Start).
    /// 버림 더미: 버림이 생기는 일(효과 적용 · 무효화 · 강제 버림 · 함정 파괴 · 사망 · 나감)마다 내 카드가 있으면 더미로 보낸 뒤 맨 위 카드 · 장수를 바꾼다 (10-ui.md 2-1 「버림 더미」).
    /// </summary>
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private DeckPresenter deck;
        [SerializeField] private DiscardPilePresenter discardPile;
        [SerializeField] private HandPresenter hand;
        [SerializeField] private MyPanelPresenter myPanel;
        [SerializeField] private OpponentSeatsPresenter seats;
        [SerializeField] private TurnStripPresenter turnStrip;
        [SerializeField] private TurnTimerPresenter turnTimer;
        [SerializeField] private HistoryPresenter history;

        // 단계 하나 = 영역에 값을 넣고, 기다릴 연출이 있으면 그 코루틴을 돌려준다 (없으면 null).
        private readonly Queue<Func<IEnumerator>> _steps = new Queue<Func<IEnumerator>>();
        private Coroutine _playing;

        // 내가 뽑으면 카드 내용(OnMyCardDrawn, 나에게만)이 장수(OnCardDrawn, 전원)보다 먼저 온다 — 둘을 한 단계로 묶는다.
        private bool _hasPendingMyCard;
        private int _pendingCardInstanceId;
        private int _pendingCardId;

        private void Awake()
        {
            if (deck == null || discardPile == null || hand == null || myPanel == null || seats == null || turnStrip == null || turnTimer == null || history == null)
                Debug.LogError($"[{nameof(GameScreenPresenter)}] 영역 프레젠터 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            GameEvents.OnHpChanged += HandleHpChanged;
            GameEvents.OnCardDrawn += HandleCardDrawn;
            GameEvents.OnMyCardDrawn += HandleMyCardDrawn;
            GameEvents.OnDeckRefilled += HandleDeckRefilled;
            GameEvents.OnTurnStarted += HandleTurnStarted;
            GameEvents.OnCardPlayed += HandleCardPlayed;
            GameEvents.OnCardResolved += HandleCardResolved;
            GameEvents.OnCardNegated += HandleCardNegated;
            GameEvents.OnCardReturnedToHand += HandleCardReturnedToHand;
            GameEvents.OnCardsDiscarded += HandleCardsDiscarded;
            GameEvents.OnTrapDestroyed += HandleTrapDestroyed;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnPlayerLeftGame += HandlePlayerLeftGame;
        }

        private void OnDisable()
        {
            GameEvents.OnHpChanged -= HandleHpChanged;
            GameEvents.OnCardDrawn -= HandleCardDrawn;
            GameEvents.OnMyCardDrawn -= HandleMyCardDrawn;
            GameEvents.OnDeckRefilled -= HandleDeckRefilled;
            GameEvents.OnTurnStarted -= HandleTurnStarted;
            GameEvents.OnCardPlayed -= HandleCardPlayed;
            GameEvents.OnCardResolved -= HandleCardResolved;
            GameEvents.OnCardNegated -= HandleCardNegated;
            GameEvents.OnCardReturnedToHand -= HandleCardReturnedToHand;
            GameEvents.OnCardsDiscarded -= HandleCardsDiscarded;
            GameEvents.OnTrapDestroyed -= HandleTrapDestroyed;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnPlayerLeftGame -= HandlePlayerLeftGame;

            if (_playing != null)
            {
                StopCoroutine(_playing);
                _playing = null;
            }

            _steps.Clear();
            _hasPendingMyCard = false;
        }

        private void HandleHpChanged(int actorNumber, int hp)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                if (isMe)
                    myPanel.ShowHp(hp);
                else
                    seats.ShowHp(actorNumber, hp);
                history.ShowHpChanged(actorNumber, hp);
                return null;
            });
        }

        private void HandleMyCardDrawn(int cardInstanceId, int cardId)
        {
            if (_hasPendingMyCard)
                Debug.LogWarning($"[{nameof(GameScreenPresenter)}] 카드 내용이 연달아 왔다 — 앞 카드(instanceId={_pendingCardInstanceId})의 장수 이벤트가 없었다.", this);

            _hasPendingMyCard = true;
            _pendingCardInstanceId = cardInstanceId;
            _pendingCardId = cardId;
        }

        private void HandleCardDrawn(int actorNumber, int handCount, int deckCount)
        {
            bool isMe = IsMe(actorNumber);
            bool hasCard = isMe && _hasPendingMyCard;
            int cardInstanceId = _pendingCardInstanceId;
            int cardId = _pendingCardId;

            if (isMe && !_hasPendingMyCard)
                Debug.LogWarning($"[{nameof(GameScreenPresenter)}] 내가 뽑았는데 카드 내용이 먼저 오지 않았다. handCount={handCount}", this);

            _hasPendingMyCard = false;

            Enqueue(() =>
            {
                deck.ShowDeckCount(deckCount);
                history.ShowCardDrawn(actorNumber);
                if (!isMe)
                    return seats.DrawRoutine(actorNumber, handCount);

                myPanel.ShowHandCount(handCount);
                return hasCard ? hand.DrawRoutine(cardInstanceId, cardId) : null;
            });
        }

        private void HandleDeckRefilled(int deckCount)
        {
            Enqueue(() =>
            {
                deck.ShowDeckCount(deckCount);
                discardPile.Clear(); // 버림 더미를 섞어 덱으로 (05-deck.md 5절)
                return null;
            });
        }

        private void HandleTurnStarted(int actorNumber, double deadline)
        {
            bool isMyTurn = IsMe(actorNumber);

            Enqueue(() =>
            {
                myPanel.ShowTurn(isMyTurn);
                seats.ShowTurn(actorNumber);
                turnStrip.ShowTurn(actorNumber);
                turnTimer.ShowTurn(deadline);
                history.ShowTurnStarted(actorNumber);
                return null;
            });
        }

        // 내 카드는 1단계 연출(테이블 위로)이 끝날 때까지 큐가 기다린다. 카드는 처리 결과가 올 때까지 내는 자리에 남는다.
        private void HandleCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                history.ShowCardPlayed(actorNumber, cardId, targets);
                if (!isMe)
                {
                    seats.ShowHandCount(actorNumber, handCount);
                    return null;
                }

                myPanel.ShowHandCount(handCount);
                return hand.PlayCardRoutine(cardInstanceId);
            });
        }

        // 효과 적용 · 무효화 — 어느 쪽이든 카드는 버림 더미로 간다 (04-card.md 4절). 4 · 5 단계 · 도장 연출이 생기면 버림 더미로 보내기 전에 넣는다 (10-ui.md 5-1).
        private void HandleCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount)
            => Enqueue(() => PlayedCardToDiscardRoutine(cardInstanceId, cardId, discardCount));

        private void HandleCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount)
            => Enqueue(() => PlayedCardToDiscardRoutine(cardInstanceId, cardId, discardCount));

        // 내는 자리에 내 카드가 있으면 버림 더미로 보낸 뒤, 그 카드를 더미의 맨 위로. 남의 카드는 아직 테이블에 그리지 않아 더미만 바뀐다.
        private IEnumerator PlayedCardToDiscardRoutine(int cardInstanceId, int cardId, int discardCount)
        {
            yield return hand.DiscardPlayedRoutine(cardInstanceId);
            discardPile.Show(cardId, discardCount);
        }

        // 체인의 카드가 버림 더미 대신 시전자 손패로 (04-card.md 7절 「손패로 반환」).
        private void HandleCardReturnedToHand(int actorNumber, int cardInstanceId, int cardId, int handCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                if (!isMe)
                {
                    seats.ShowHandCount(actorNumber, handCount);
                    return null;
                }

                myPanel.ShowHandCount(handCount);
                hand.ReturnPlayedCard(cardInstanceId);
                return null;
            });
        }

        // 강제 버림 — 버림 더미는 공개라 카드도 공개 (05-deck.md 7절). 내 카드는 손패에서 더미로 날아간다.
        private void HandleCardsDiscarded(int actorNumber, int[] cardInstanceIds, int[] cardIds, int handCount, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() => CardsToDiscardRoutine(actorNumber, isMe, handCount, cardIds, discardCount,
                isMe ? hand.DiscardFromHandRoutine(cardInstanceIds) : null));
        }

        private void HandleTrapDestroyed(int owner, int cardInstanceId, int cardId, int trapCount, int discardCount)
        {
            Enqueue(() =>
            {
                discardPile.Show(cardId, discardCount);
                return null;
            });
        }

        // 최종 사망 · 나감 — 손패(와 함정) 전부가 버림 더미로 (05-deck.md 6 · 7절). 죽은 사람의 좌석 표시는 여기서 다루지 않는다 (기능 7).
        private void HandlePlayerDied(int actorNumber, int[] cardIds, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() => CardsToDiscardRoutine(actorNumber, isMe, 0, cardIds, discardCount,
                isMe ? hand.DiscardHandRoutine() : null));
        }

        // 나간 사람의 좌석은 RoomEvents 가 지운다 — 여기서는 더미만.
        private void HandlePlayerLeftGame(int actorNumber, int[] cardIds, int discardCount)
        {
            Enqueue(() =>
            {
                discardPile.Show(cardIds, discardCount);
                return null;
            });
        }

        // 손패 장수를 바꾸고, 내 카드가 날아가는 연출(fly)이 있으면 기다린 뒤 버려진 마지막 카드를 더미의 맨 위로.
        private IEnumerator CardsToDiscardRoutine(int actorNumber, bool isMe, int handCount, int[] cardIds, int discardCount, IEnumerator fly)
        {
            if (isMe)
                myPanel.ShowHandCount(handCount);
            else
                seats.ShowHandCount(actorNumber, handCount);

            if (fly != null)
                yield return fly;
            discardPile.Show(cardIds, discardCount);
        }

        private void Enqueue(Func<IEnumerator> step)
        {
            _steps.Enqueue(step);

            if (_playing == null)
                _playing = StartCoroutine(PlayRoutine());
        }

        // 앞에서부터 하나씩. 연출이 있는 단계는 끝날 때까지 다음을 꺼내지 않는다.
        private IEnumerator PlayRoutine()
        {
            while (_steps.Count > 0)
            {
                IEnumerator step = _steps.Dequeue()();
                if (step != null)
                    yield return step;
            }

            _playing = null;
        }

        private static bool IsMe(int actorNumber) => PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
    }
}
