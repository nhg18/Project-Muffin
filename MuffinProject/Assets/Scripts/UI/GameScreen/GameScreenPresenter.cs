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
    /// 체인: 카드가 나오면(OnCardPlayed) ChainCardsPresenter 가 크게 보여 주고, 반응 시간(OnReactionStarted)이 오면 그 아래에 반응 타이머. 처리 결과가 오면 카드를 치우고 체인이 비면 타이머를 숨긴다 (10-ui.md 5절).
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
        [Tooltip("체인에 올라간 카드 — 낸 카드를 모든 화면에 크게 보여 주고 처리되면 버림 더미로.")]
        [SerializeField] private ChainCardsPresenter chainCards;
        [SerializeField] private ReactionTimerPresenter reactionTimer;
        [SerializeField] private MyTrapsPresenter myTraps;

        // 단계 하나 = 영역에 값을 넣고, 기다릴 연출이 있으면 그 코루틴을 돌려준다 (없으면 null).
        private readonly Queue<Func<IEnumerator>> _steps = new Queue<Func<IEnumerator>>();
        private Coroutine _playing;

        // 내가 뽑으면 카드 내용(OnMyCardDrawn, 나에게만)이 장수(OnCardDrawn, 전원)보다 먼저 온다 — 둘을 한 단계로 묶는다.
        private bool _hasPendingMyCard;
        private int _pendingCardInstanceId;
        private int _pendingCardId;

        // 화면에 재생한 기준으로 체인에 남은 카드 수 — 0 이 되면 반응 타이머를 숨긴다. 연출 큐 단계 안에서만 바꾼다.
        private int _chainCount;

        private void Awake()
        {
            if (deck == null || discardPile == null || hand == null || myPanel == null || seats == null || turnStrip == null || turnTimer == null || history == null || chainCards == null || reactionTimer == null || myTraps == null)
                Debug.LogError($"[{nameof(GameScreenPresenter)}] 영역 프레젠터 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            hand.OnRaisedChanged += HandleHandRaised;
            chainCards.OnBottomChanged += reactionTimer.SetCardsBottom;
            GameEvents.OnHpChanged += HandleHpChanged;
            GameEvents.OnCardDrawn += HandleCardDrawn;
            GameEvents.OnMyCardDrawn += HandleMyCardDrawn;
            GameEvents.OnDeckRefilled += HandleDeckRefilled;
            GameEvents.OnTurnStarted += HandleTurnStarted;
            GameEvents.OnCardPlayed += HandleCardPlayed;
            GameEvents.OnTrapActivated += HandleTrapActivated;
            GameEvents.OnReactionStarted += HandleReactionStarted;
            GameEvents.OnMyCountersUsable += HandleMyCountersUsable;
            GameEvents.OnTrapSet += HandleTrapSet;
            GameEvents.OnMyTrapSet += HandleMyTrapSet;
            GameEvents.OnMyTrapRemoved += HandleMyTrapRemoved;
            GameEvents.OnTrapReturned += HandleTrapReturned;
            GameEvents.OnTrapMoved += HandleTrapMoved;
            GameEvents.OnHandCardsMoved += HandleHandCardsMoved;
            GameEvents.OnMyCardsGained += HandleMyCardsGained;
            GameEvents.OnMyCardsLost += HandleMyCardsLost;
            GameEvents.OnCardResolved += HandleCardResolved;
            GameEvents.OnCardNegated += HandleCardNegated;
            GameEvents.OnCardReturnedToHand += HandleCardReturnedToHand;
            GameEvents.OnCardsDiscarded += HandleCardsDiscarded;
            GameEvents.OnTrapDestroyed += HandleTrapDestroyed;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnPlayerLeftGame += HandlePlayerLeftGame;
            GameEvents.OnChapchuChanged += HandleChapchuChanged;
        }

        private void OnDisable()
        {
            hand.OnRaisedChanged -= HandleHandRaised;
            chainCards.OnBottomChanged -= reactionTimer.SetCardsBottom;
            GameEvents.OnHpChanged -= HandleHpChanged;
            GameEvents.OnCardDrawn -= HandleCardDrawn;
            GameEvents.OnMyCardDrawn -= HandleMyCardDrawn;
            GameEvents.OnDeckRefilled -= HandleDeckRefilled;
            GameEvents.OnTurnStarted -= HandleTurnStarted;
            GameEvents.OnCardPlayed -= HandleCardPlayed;
            GameEvents.OnTrapActivated -= HandleTrapActivated;
            GameEvents.OnReactionStarted -= HandleReactionStarted;
            GameEvents.OnMyCountersUsable -= HandleMyCountersUsable;
            GameEvents.OnTrapSet -= HandleTrapSet;
            GameEvents.OnMyTrapSet -= HandleMyTrapSet;
            GameEvents.OnMyTrapRemoved -= HandleMyTrapRemoved;
            GameEvents.OnTrapReturned -= HandleTrapReturned;
            GameEvents.OnTrapMoved -= HandleTrapMoved;
            GameEvents.OnHandCardsMoved -= HandleHandCardsMoved;
            GameEvents.OnMyCardsGained -= HandleMyCardsGained;
            GameEvents.OnMyCardsLost -= HandleMyCardsLost;
            GameEvents.OnCardResolved -= HandleCardResolved;
            GameEvents.OnCardNegated -= HandleCardNegated;
            GameEvents.OnCardReturnedToHand -= HandleCardReturnedToHand;
            GameEvents.OnCardsDiscarded -= HandleCardsDiscarded;
            GameEvents.OnTrapDestroyed -= HandleTrapDestroyed;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnPlayerLeftGame -= HandlePlayerLeftGame;
            GameEvents.OnChapchuChanged -= HandleChapchuChanged;

            if (_playing != null)
            {
                StopCoroutine(_playing);
                _playing = null;
            }

            _steps.Clear();
            _hasPendingMyCard = false;
            _chainCount = 0;
        }

        // 내 손패 올림 · 내림은 내 포인터의 일이라 연출 큐를 거치지 않고 바로 — 가운데 카드 · 반응 타이머가 비킨다.
        private void HandleHandRaised(bool raised)
        {
            chainCards.SetHandRaised(raised);
            reactionTimer.SetHandRaised(raised);
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

        // 누가 낸 카드든 1 단계(테이블 위로) + 3 단계(가운데에서 크게)가 끝날 때까지 큐가 기다린다 — 방장의 반응 시간도 그 뒤에 열린다 (04-card.md 8절).
        // 카드는 처리 결과가 올 때까지 크게 둔 자리에 남는다. 카운터도 같은 이벤트로 온다.
        private void HandleCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                _chainCount++;
                reactionTimer.Hide(); // 3 단계 동안은 숨는다 (시안 .g3big) — 새 반응 시간이 바로 뒤따라온다
                hand.EndReaction();
                history.ShowCardPlayed(actorNumber, cardId, targets);
                if (isMe)
                    myPanel.ShowHandCount(handCount);
                else
                    seats.ShowHandCount(actorNumber, handCount);

                return chainCards.PlayRoutine(actorNumber, cardInstanceId, cardId, targets, isMe ? hand.TakeCard(cardInstanceId) : null);
            });
        }

        // 함정 칸에서 발동한 함정도 체인에 올라간다 — 공개되며 그 칸에서 나와 낸 카드와 같은 1 · 3 단계를 탄다 (10-ui.md 5-1 「카운터 · 함정이 끼어든 경우」).
        private void HandleTrapActivated(int actorNumber, int cardInstanceId, int cardId, int[] targets, int trapCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                _chainCount++;
                reactionTimer.Hide();
                hand.EndReaction();

                Transform slot = isMe ? myTraps.SlotOf(cardInstanceId) : seats.GetTrapSlot(actorNumber);
                if (isMe)
                {
                    myTraps.Remove(cardInstanceId);
                    myTraps.ShowCount(trapCount);
                }
                else
                {
                    seats.ShowTrapCount(actorNumber, trapCount);
                }

                return chainCards.PlayRoutine(actorNumber, cardInstanceId, cardId, targets, null, slot);
            });
        }

        // 함정 설치 — 장수는 전원에게, 어떤 함정인지는 나에게만 (OnMyTrapSet 이 먼저 온다).
        private void HandleTrapSet(int actorNumber, int handCount, int trapCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                if (isMe)
                {
                    myPanel.ShowHandCount(handCount);
                    myTraps.ShowCount(trapCount);
                }
                else
                {
                    seats.ShowHandCount(actorNumber, handCount);
                    seats.ShowTrapCount(actorNumber, trapCount);
                }

                return null;
            });
        }

        // 내 함정 칸에 함정이 들어왔다 (설치 · 빼앗아 옴) — 설치면 손패의 그 카드가 칸으로 간다. 칸 수는 뒤따르는 OnTrapSet · OnTrapMoved 가 바꾼다.
        private void HandleMyTrapSet(int cardInstanceId, int cardId)
        {
            Enqueue(() => MyTrapSetRoutine(cardInstanceId, cardId));
        }

        private IEnumerator MyTrapSetRoutine(int cardInstanceId, int cardId)
        {
            yield return hand.SetTrapRoutine(cardInstanceId, myTraps.NextSlot);
            myTraps.Add(cardInstanceId, cardId);
        }

        private void HandleMyTrapRemoved(int cardInstanceId)
        {
            Enqueue(() =>
            {
                myTraps.Remove(cardInstanceId);
                return null;
            });
        }

        // 함정이 주인 손패로 돌아갔다 — 내 것이면 카드 내용은 OnMyTrapRemoved · OnMyCardsGained 로 따로 온다.
        private void HandleTrapReturned(int owner, int trapCount, int handCount)
        {
            bool isMe = IsMe(owner);

            Enqueue(() =>
            {
                if (isMe)
                {
                    myTraps.ShowCount(trapCount);
                    myPanel.ShowHandCount(handCount);
                }
                else
                {
                    seats.ShowTrapCount(owner, trapCount);
                    seats.ShowHandCount(owner, handCount);
                }

                return null;
            });
        }

        // 함정 빼앗기 — 두 사람의 칸 수.
        private void HandleTrapMoved(int from, int to, int fromTrapCount, int toTrapCount)
        {
            Enqueue(() =>
            {
                ShowTrapCount(from, fromTrapCount);
                ShowTrapCount(to, toTrapCount);
                return null;
            });
        }

        // 손패 이동 (훔치기 · 교환) — 두 사람의 장수. 내 카드 내용은 OnMyCardsGained · OnMyCardsLost 로 따로 온다.
        private void HandleHandCardsMoved(int from, int to, int count, int fromHandCount, int toHandCount)
        {
            Enqueue(() =>
            {
                ShowHandCount(from, fromHandCount);
                ShowHandCount(to, toHandCount);
                return null;
            });
        }

        private void HandleMyCardsGained(int[] cardInstanceIds, int[] cardIds)
        {
            Enqueue(() =>
            {
                hand.GainCards(cardInstanceIds, cardIds);
                return null;
            });
        }

        private void HandleMyCardsLost(int[] cardInstanceIds)
        {
            Enqueue(() =>
            {
                hand.LoseCards(cardInstanceIds);
                return null;
            });
        }

        private void ShowTrapCount(int actorNumber, int trapCount)
        {
            if (IsMe(actorNumber))
                myTraps.ShowCount(trapCount);
            else
                seats.ShowTrapCount(actorNumber, trapCount);
        }

        private void ShowHandCount(int actorNumber, int handCount)
        {
            if (IsMe(actorNumber))
                myPanel.ShowHandCount(handCount);
            else
                seats.ShowHandCount(actorNumber, handCount);
        }

        // 카드가 체인에 오를 때마다 반응 시간이 새로 정해진다 — 카드 연출 단계 뒤에 재생되므로 크게 둔 카드 아래에 뜬다.
        private void HandleReactionStarted(double opensAt, double deadline)
        {
            Enqueue(() =>
            {
                reactionTimer.SetCardName(chainCards.TopCardName);
                reactionTimer.Show(opensAt, deadline);
                hand.ShowReaction(opensAt, deadline);
                return null;
            });
        }

        // 이번 반응 시간에 낼 수 있는 내 카운터 — 방장이 판정해 나에게만 보낸다 (OnReactionStarted 바로 뒤). 손패의 그 카드에 초록 빛.
        private void HandleMyCountersUsable(int[] cardInstanceIds)
        {
            Enqueue(() =>
            {
                hand.ShowUsableCounters(cardInstanceIds);
                return null;
            });
        }

        // 효과 적용 · 무효화 — 어느 쪽이든 카드는 버림 더미로 간다 (04-card.md 4절). 4 · 5 단계 · 도장 연출이 생기면 버림 더미로 보내기 전에 넣는다 (10-ui.md 5-1).
        private void HandleCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount)
            => Enqueue(() => ChainCardToDiscardRoutine(cardInstanceId, cardId, discardCount));

        private void HandleCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount)
            => Enqueue(() => ChainCardToDiscardRoutine(cardInstanceId, cardId, discardCount));

        // 체인의 카드를 버림 더미로 보낸 뒤, 그 카드를 더미의 맨 위로. 함정 칸에서 나온 카드는 아직 그리지 않아 더미만 바뀐다.
        private IEnumerator ChainCardToDiscardRoutine(int cardInstanceId, int cardId, int discardCount)
        {
            LeaveChain();
            yield return chainCards.DiscardRoutine(cardInstanceId);
            discardPile.Show(cardId, discardCount);
        }

        // 체인의 카드가 버림 더미 대신 시전자 손패로 (04-card.md 7절 「손패로 반환」) — 내 것은 손패에 다시 붙고, 남의 것은 그 좌석으로 돌아간다.
        private void HandleCardReturnedToHand(int actorNumber, int cardInstanceId, int cardId, int handCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                LeaveChain();
                if (!isMe)
                {
                    seats.ShowHandCount(actorNumber, handCount);
                    return chainCards.ReturnToSeatRoutine(actorNumber, cardInstanceId);
                }

                myPanel.ShowHandCount(handCount);
                CardView card = chainCards.Take(cardInstanceId);
                if (card != null)
                    hand.PutBack(card);
                else
                    Debug.LogWarning($"[{nameof(GameScreenPresenter)}] 손패로 돌아온 카드가 체인에 없다. instanceId={cardInstanceId}", this);
                return null;
            });
        }

        // 체인의 카드 한 장이 처리됐다 — 처리 중에는 타이머가 「처리하는 중」 이고, 마지막 카드까지 끝나면 숨긴다.
        private void LeaveChain()
        {
            hand.EndReaction(); // 처리가 시작됐다 — 더는 카운터를 낼 수 없다
            _chainCount = Mathf.Max(0, _chainCount - 1);
            if (_chainCount == 0)
                reactionTimer.Hide();
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
            bool isMe = IsMe(owner);

            Enqueue(() =>
            {
                if (isMe)
                    myTraps.Remove(cardInstanceId);
                ShowTrapCount(owner, trapCount);
                discardPile.Show(cardId, discardCount);
                return null;
            });
        }

        // 최종 사망 · 나감 — 손패(와 함정) 전부가 버림 더미로 (05-deck.md 6 · 7절). 죽은 사람의 좌석 표시는 여기서 다루지 않는다 (기능 7).
        private void HandlePlayerDied(int actorNumber, int[] cardIds, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                // 함정도 전부 버림 더미로 (05-deck.md 6절)
                if (isMe)
                    myTraps.Clear();
                else
                    seats.ShowTrapCount(actorNumber, 0);

                return CardsToDiscardRoutine(actorNumber, isMe, 0, cardIds, discardCount, isMe ? hand.DiscardHandRoutine() : null);
            });
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

        // 찹츄 선언 · 해제 — 내 패널(버튼 「찹츄 완료」) 또는 그 사람 좌석, 턴 띠 칩, 기록. 선언 연출(전원 화면 1.5초, 10-ui.md 7절)은 아직 없다.
        private void HandleChapchuChanged(int actorNumber, bool isChapchu)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                if (isMe)
                    myPanel.ShowChapchu(isChapchu);
                else
                    seats.ShowChapchu(actorNumber, isChapchu);
                turnStrip.ShowChapchu(actorNumber, isChapchu);
                if (isChapchu)
                    history.ShowChapchuDeclared(actorNumber);
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
