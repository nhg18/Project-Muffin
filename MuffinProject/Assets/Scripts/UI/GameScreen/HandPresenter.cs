using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Network;
using DG.Tweening;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 뷰에 카드를 넣고 빼고, 드롭한 카드의 사용을 요청한다 (10-ui.md 4 · 6절). 손패 원본은 방장(GameServer)에 있고 여기는 화면 표시용 사본이다.
    /// 넣고 빼기는 GameScreenPresenter 가 연출 큐 순서로 부른다 — 내가 뽑은 카드를 덱 자리에서 꺼내 붙이고, 방장이 승인한 내 카드 사용만 뺀다.
    /// 카드 종류로 요청이 갈린다 (04-card.md 5 ~ 7절): 행동 = 카드 사용, 카운터 = 카운터 사용(턴과 관계없이 반응 시간에), 함정 = 설치(대상 없음).
    /// 반응 시간 동안은 방장이 알려 준 「낼 수 있는 카운터」 에 초록 빛을 켠다 (10-ui.md 5절) — 무엇을 낼 수 있는지는 방장이 판정하고 여기는 켜기만 한다.
    /// 드롭: 드롭 영역 안이면 대상 타입에 따라 바로 요청하거나(자동 대상 — 방장이 정한다) 상대 좌석에서 대상을 고르게 한 뒤 요청한다.
    /// 요청 뒤에도 카드는 손패에 남는다 — 승인(OnCardPlayed)이 오면 빠지고, 거절이면 그대로다 (04-card.md 10절). 거절 문구는 RejectToastPresenter.
    /// 승인된 카드는 손패에서 떼어 체인 카드 연출(ChainCardsPresenter)에 넘긴다 — 테이블에 내고 크게 보여 주고 버리는 것은 그쪽. 「손패로 반환」이면 돌려받아 다시 붙인다.
    /// 강제 버림 · 최종 사망으로 버려지는 손패는 여기서 버림 더미로 보낸다.
    /// 카드 종류 ID → CardData 는 덱 에셋으로 찾는다 — 게임에 나오는 카드는 전부 덱에서 나오기 때문.
    /// </summary>
    [RequireComponent(typeof(HandView))]
    public class HandPresenter : MonoBehaviour
    {
        [Tooltip("PunGameServer 와 같은 덱 에셋 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;
        [Tooltip("뽑은 카드가 날아오기 시작하는 자리 — 덱 더미 (3D 층).")]
        [SerializeField] private Transform drawOrigin;
        [Tooltip("카드를 꺼낸 뒤 다음 일로 넘어가기까지 초. 날아오는 연출(HandView)이 끝나기를 다 기다리면 첫 5장에 5초가 걸려, 겹쳐 날아오게 둔다.")]
        [SerializeField] private float dealInterval = 0.15f;

        [Header("카드 사용")]
        [SerializeField] private PunGameServer server;
        [Tooltip("이 Rect 안에 카드를 놓으면 사용 요청 (10-ui.md 2-1 「드롭 영역」). 씬에서 위치 · 크기를 바꾼다.")]
        [SerializeField] private RectTransform dropZone;
        [Tooltip("대상을 고르는 카드(OneOther · TwoOthers)일 때 좌석 탭을 받는 곳.")]
        [SerializeField] private OpponentSeatsPresenter seats;

        [Header("버리기 연출")]
        [Tooltip("버려지는 카드가 날아가 닿는 자리 — 버림 더미의 맨 위 카드 자리 (DiscardPileView.TopAnchor). 위치 · 회전 · 배율 그대로 닿는다.")]
        [SerializeField] private Transform discardSpot;
        [Tooltip("버림 더미로 가는 초.")]
        [SerializeField] private float discardDuration = 0.38f;
        [Tooltip("설치한 함정이 손패에서 함정 칸으로 가는 초.")]
        [SerializeField] private float trapSetDuration = 0.3f;

        // 반응 시간 — 방장이 보낸 시각(서버 시각)과 낼 수 있는 내 카운터. 시각 안에서만 빛을 켠다.
        private readonly HashSet<int> _usableCounters = new HashSet<int>();
        private double _reactionOpensAt;
        private double _reactionDeadline;
        private bool _glowing;

        private HandView _view;

        private void Awake()
        {
            _view = GetComponent<HandView>();

            if (deck == null || drawOrigin == null || server == null || dropZone == null || seats == null || discardSpot == null)
                Debug.LogError($"[{nameof(HandPresenter)}] {nameof(deck)} · {nameof(drawOrigin)} · {nameof(server)} · {nameof(dropZone)} · {nameof(seats)} · {nameof(discardSpot)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.OnCardDropped += HandleCardDropped;
        }

        private void OnDisable()
        {
            _view.OnCardDropped -= HandleCardDropped;
        }

        private void Update()
        {
            double now = PhotonNetwork.Time;
            bool glow = _usableCounters.Count > 0 && now >= _reactionOpensAt && now < _reactionDeadline;
            if (glow == _glowing) return;

            _glowing = glow;
            _view.SetGlow(glow ? _usableCounters : null);
        }

        /// <summary>반응 시간이 정해졌다 (OnReactionStarted) — 앞 반응 시간의 「낼 수 있는 카운터」 는 버린다. 새 목록은 ShowUsableCounters 로 따로 온다.</summary>
        public void ShowReaction(double opensAt, double deadline)
        {
            EndReaction();
            _reactionOpensAt = opensAt;
            _reactionDeadline = deadline;
        }

        /// <summary>이번 반응 시간에 낼 수 있는 내 카운터 (OnMyCountersUsable) — 반응 시간이 열려 있는 동안 초록 빛.</summary>
        public void ShowUsableCounters(int[] cardInstanceIds)
        {
            _usableCounters.Clear();
            _usableCounters.UnionWith(cardInstanceIds);
            _glowing = false; // 다음 Update 에서 새 목록으로 다시 켠다
            _view.SetGlow(null);
        }

        /// <summary>반응 시간이 끝났다 (새 카드가 체인에 오름 · 체인 처리 시작) — 빛을 끈다.</summary>
        public void EndReaction()
        {
            _usableCounters.Clear();
            _glowing = false;
            _view.SetGlow(null);
        }

        /// <summary>내가 설치한 함정 — 손패에서 떼어 함정 칸(slot 의 위치 · 회전 · 배율)으로 보내 지운다. 연출 큐가 기다릴 코루틴. 손패에 없으면(빼앗아 온 함정) 바로 끝난다.</summary>
        public IEnumerator SetTrapRoutine(int cardInstanceId, Transform slot)
        {
            CardView card = _view.TakeCard(cardInstanceId);
            if (card == null)
                yield break;

            Transform t = card.transform;
            t.SetParent(transform.parent, true);
            card.SetRaycastBlocking(false);
            t.DOKill();
            t.DOMove(slot.position, trapSetDuration).SetEase(Ease.InQuad).SetLink(card.gameObject);
            t.DORotateQuaternion(slot.rotation * Quaternion.Euler(0f, 180f, 0f), trapSetDuration).SetEase(Ease.InQuad).SetLink(card.gameObject); // 뒷면으로 눕는다
            t.DOScale(slot.lossyScale, trapSetDuration).SetEase(Ease.InQuad).SetLink(card.gameObject);
            yield return new WaitForSeconds(trapSetDuration);
            Destroy(card.gameObject);
        }

        /// <summary>내 손패로 카드가 들어왔다 (훔침 · 교환 · 함정 반환, OnMyCardsGained) — 손패 오른쪽 끝에 붙인다. 날아오는 연출은 아직 없다.</summary>
        public void GainCards(int[] cardInstanceIds, int[] cardIds)
        {
            for (int i = 0; i < cardInstanceIds.Length; i++)
            {
                CardData data = deck.GetCard(cardIds[i]);
                if (data == null)
                    Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardIds[i]}", this);
                else
                    _view.AddCard(cardInstanceIds[i], data, transform.position);
            }
        }

        /// <summary>내 손패에서 카드가 나갔다 (훔쳐짐 · 교환, OnMyCardsLost) — 지운다. 날아가는 연출은 아직 없다.</summary>
        public void LoseCards(int[] cardInstanceIds)
        {
            foreach (int instanceId in cardInstanceIds)
            {
                CardView card = _view.TakeCard(instanceId);
                if (card != null)
                    Destroy(card.gameObject);
            }
        }

        /// <summary>내가 뽑은 카드를 덱 자리에서 꺼내 손패 끝에 붙인다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator DrawRoutine(int cardInstanceId, int cardId)
        {
            CardData data = deck.GetCard(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardId}", this);
                yield break;
            }

            _view.AddCard(cardInstanceId, data, drawOrigin.position);
            yield return new WaitForSeconds(dealInterval);
        }

        /// <summary>방장이 승인한 내 카드 사용 — 손패에서 떼어 넘긴다. 체인 카드 연출(ChainCardsPresenter)이 이어받는다. 손패에 없으면 경고하고 null.</summary>
        public CardView TakeCard(int cardInstanceId)
        {
            CardView card = _view.TakeCard(cardInstanceId);
            if (card == null)
                Debug.LogWarning($"[{nameof(HandPresenter)}] 승인된 카드가 손패에 없다. instanceId={cardInstanceId}", this);
            return card;
        }

        /// <summary>체인에 있던 내 카드가 손패로 돌아왔다 (04-card.md 7절 「손패로 반환」) — 손패 오른쪽 끝에 다시 붙인다.</summary>
        public void PutBack(CardView card) => _view.PutBack(card);

        /// <summary>내 손패의 카드가 버려졌다 (강제 버림, 05-deck.md 7절) — 손패에서 떼어 버림 더미로 보내 지운다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator DiscardFromHandRoutine(int[] cardInstanceIds)
        {
            var cards = new List<CardView>();
            foreach (int instanceId in cardInstanceIds)
            {
                CardView card = _view.TakeCard(instanceId);
                if (card == null)
                    Debug.LogWarning($"[{nameof(HandPresenter)}] 버려진 카드가 손패에 없다. instanceId={instanceId}", this);
                else
                    cards.Add(card);
            }

            yield return FlyToDiscardRoutine(cards);
        }

        /// <summary>내 손패 전부가 버려졌다 (최종 사망, 05-deck.md 6절) — 버림 더미로 보내 지운다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator DiscardHandRoutine() => FlyToDiscardRoutine(_view.TakeAll());

        // 카드들을 버림 더미의 맨 위 카드 자리로 보내고 지운다 — 닿는 자세가 더미의 맨 위 카드와 같아, 지운 뒤 더미가 그 카드를 맨 위로 보여 준다 (DiscardPilePresenter).
        private IEnumerator FlyToDiscardRoutine(List<CardView> cards)
        {
            if (cards.Count == 0)
                yield break;

            foreach (CardView card in cards)
            {
                Transform t = card.transform;
                t.SetParent(transform.parent, true); // 손패 올리기 · 내리기에 끌려가지 않게
                card.SetRaycastBlocking(false);

                t.DOKill();
                t.DOMove(discardSpot.position, discardDuration).SetEase(Ease.InQuad).SetLink(card.gameObject);
                t.DORotateQuaternion(discardSpot.rotation, discardDuration).SetEase(Ease.InQuad).SetLink(card.gameObject);
                t.DOScale(discardSpot.lossyScale, discardDuration).SetEase(Ease.InQuad).SetLink(card.gameObject);
            }

            yield return new WaitForSeconds(discardDuration);

            foreach (CardView card in cards)
                Destroy(card.gameObject);
        }

        // 드롭 영역 밖이면 손패로. 안이면 대상 타입으로 나눈다 — 자동 타입은 빈 대상으로 바로 요청(방장이 정한다, 04-card.md 11절), 고르는 타입은 좌석 탭.
        private void HandleCardDropped(CardView card, Vector2 screenPosition)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(dropZone, screenPosition, null))
            {
                _view.ReturnCard(card);
                return;
            }

            CardData data = deck.GetCard(card.CardId);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={card.CardId}", this);
                _view.ReturnCard(card);
                return;
            }

            // 함정은 설치 — 대상이 없다 (04-card.md 6절). 발동 대상은 발동할 때 방장이 정한다.
            if (data.type == CardType.Trap)
            {
                Debug.Log($"[{nameof(HandPresenter)}] 함정 설치 요청 instanceId={card.InstanceId} cardId={card.CardId}", this);
                server.RequestSetTrap(card.InstanceId);
                _view.ReturnCard(card);
                return;
            }

            switch (data.targetType)
            {
                case TargetType.OneOther:
                    StartCoroutine(PlayWithTargetsRoutine(card, data.cardName, 1));
                    break;
                case TargetType.TwoOthers:
                    StartCoroutine(PlayWithTargetsRoutine(card, data.cardName, 2));
                    break;
                default:
                    RequestPlay(card, Array.Empty<int>());
                    _view.ReturnCard(card);
                    break;
            }
        }

        // 대상 선택 동안 손패를 잠그고 카드는 놓은 자리에 둔다. 안내 바에 카드 이름을 보여 준다. 고르면 요청, 취소 · 시간 초과면 요청하지 않는다. 끝나면 풀고 카드를 돌린다 (10-ui.md 6절).
        private IEnumerator PlayWithTargetsRoutine(CardView card, string cardName, int targetCount)
        {
            _view.SetLocked(true);

            int[] targets = null;
            yield return seats.SelectTargetsRoutine(cardName, targetCount, picked => targets = picked);

            if (targets != null)
                RequestPlay(card, targets);
            else
                Debug.Log($"[{nameof(HandPresenter)}] 대상 선택 취소 — 카드 {card.InstanceId} 손패로", this);

            _view.SetLocked(false);
            _view.ReturnCard(card);
        }

        private void RequestPlay(CardView card, int[] targets)
        {
            bool isCounter = deck.GetCard(card.CardId)?.type == CardType.Counter;
            Debug.Log($"[{nameof(HandPresenter)}] {(isCounter ? "카운터" : "카드")} 사용 요청 instanceId={card.InstanceId} cardId={card.CardId} targets=[{string.Join(",", targets)}]", this);
            if (isCounter)
                server.RequestPlayCounter(card.InstanceId, targets);
            else
                server.RequestPlayCard(card.InstanceId, targets);
        }
    }
}
