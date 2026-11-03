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
    /// 큐를 타지 않는 것: 내 요청 거절(RejectToastPresenter 가 바로 띄움) · 남은 초(MyPanelPresenter 가 매 프레임 계산) · 늦게 열렸을 때의 스냅샷 복구(각 프레젠터 Start).
    /// 버림 더미 · 히스토리는 아직 없어 그쪽 일은 흘려보낸다 — 생기면 여기에 단계를 더한다.
    /// </summary>
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private DeckPresenter deck;
        [SerializeField] private HandPresenter hand;
        [SerializeField] private MyPanelPresenter myPanel;
        [SerializeField] private OpponentSeatsPresenter seats;

        // 단계 하나 = 영역에 값을 넣고, 기다릴 연출이 있으면 그 코루틴을 돌려준다 (없으면 null).
        private readonly Queue<Func<IEnumerator>> _steps = new Queue<Func<IEnumerator>>();
        private Coroutine _playing;

        // 내가 뽑으면 카드 내용(OnMyCardDrawn, 나에게만)이 장수(OnCardDrawn, 전원)보다 먼저 온다 — 둘을 한 단계로 묶는다.
        private bool _hasPendingMyCard;
        private int _pendingCardInstanceId;
        private int _pendingCardId;

        private void Awake()
        {
            if (deck == null || hand == null || myPanel == null || seats == null)
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
        }

        private void OnDisable()
        {
            GameEvents.OnHpChanged -= HandleHpChanged;
            GameEvents.OnCardDrawn -= HandleCardDrawn;
            GameEvents.OnMyCardDrawn -= HandleMyCardDrawn;
            GameEvents.OnDeckRefilled -= HandleDeckRefilled;
            GameEvents.OnTurnStarted -= HandleTurnStarted;
            GameEvents.OnCardPlayed -= HandleCardPlayed;

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
                if (!isMe)
                {
                    seats.ShowHandCount(actorNumber, handCount);
                    return null;
                }

                myPanel.ShowHandCount(handCount);
                return hasCard ? hand.DrawRoutine(cardInstanceId, cardId) : null;
            });
        }

        private void HandleDeckRefilled(int deckCount)
        {
            Enqueue(() =>
            {
                deck.ShowDeckCount(deckCount);
                return null;
            });
        }

        private void HandleTurnStarted(int actorNumber, double deadline)
        {
            bool isMyTurn = IsMe(actorNumber);

            Enqueue(() =>
            {
                myPanel.ShowTurn(isMyTurn, deadline);
                seats.ShowTurn(actorNumber);
                return null;
            });
        }

        // 버림 더미 · 히스토리 항목은 이후 작업.
        private void HandleCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                if (!isMe)
                {
                    seats.ShowHandCount(actorNumber, handCount);
                    return null;
                }

                hand.ShowCardPlayed(cardInstanceId);
                myPanel.ShowHandCount(handCount);
                return null;
            });
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
