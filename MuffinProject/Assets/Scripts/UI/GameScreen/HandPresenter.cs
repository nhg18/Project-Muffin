using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Network;
using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 뷰에 카드를 넣고 빼고, 드롭한 카드의 사용을 요청한다 (10-ui.md 4 · 6절). 손패 원본은 방장(GameServer)에 있고 여기는 화면 표시용 사본이다.
    /// 넣고 빼기는 GameScreenPresenter 가 연출 큐 순서로 부른다 — 내가 뽑은 카드를 덱 자리에서 꺼내 붙이고, 방장이 승인한 내 카드 사용만 뺀다.
    /// 드롭: 드롭 영역 안이면 대상 타입에 따라 바로 요청하거나(자동 대상 — 방장이 정한다) 상대 좌석에서 대상을 고르게 한 뒤 요청한다.
    /// 요청 뒤에도 카드는 손패에 남는다 — 승인(OnCardPlayed)이 오면 빠지고, 거절이면 그대로다 (04-card.md 10절). 거절 문구는 RejectToastPresenter.
    /// 승인된 카드는 1단계 "테이블 위로"(10-ui.md 5-1)로 카드 내는 자리에 눕혀 두고, 처리 결과(효과 적용 · 무효화)가 오면 버림 더미로 보내 지운다 — 「손패로 반환」이면 손패로 되돌린다.
    /// 그 사이가 2 · 3 단계 · 반응 시간이고, 4 · 5 단계는 버림 더미로 가기 전에 들어온다 — 카드 이펙트 작업이 이어받는다. 강제 버림 · 최종 사망으로 버려지는 손패도 여기서 버림 더미로 보낸다.
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

        [Header("카드 내기 연출 (10-ui.md 5-1 · 2-1 「3D 층」)")]
        [Tooltip("1단계 — 승인된 카드가 눕혀져 놓이는 테이블 자리.")]
        [SerializeField] private Transform playSpot;
        [Tooltip("버려지는 카드가 날아가 닿는 자리 — 버림 더미의 맨 위 카드 자리 (DiscardPileView.TopAnchor). 위치 · 회전 · 배율 그대로 닿는다.")]
        [SerializeField] private Transform discardSpot;
        [Tooltip("1단계에서 카드가 눕는 각도(X 축) — 테이블 기울기 40°.")]
        [SerializeField] private float playTilt = 40f;
        [Tooltip("손패 → 테이블 자리 이동 초 (CardFxTestScene 1단계).")]
        [SerializeField] private float playDuration = 0.46f;
        [Tooltip("내 카드 여러 장이 체인에 함께 있을 때 뒤에 낸 카드를 카메라 쪽으로 띄우는 z — 같은 자리라 면이 겹치지 않게 (배치는 10-ui.md 11절 #14 미정).")]
        [SerializeField] private float tableStackZ = -0.05f;
        [Tooltip("버림 더미로 가는 초.")]
        [SerializeField] private float discardDuration = 0.38f;

        // 내는 자리에 놓여 처리 결과를 기다리는 내 카드 (인스턴스 ID → 카드).
        private readonly Dictionary<int, CardView> _onTable = new Dictionary<int, CardView>();
        private HandView _view;

        private void Awake()
        {
            _view = GetComponent<HandView>();

            if (deck == null || drawOrigin == null || server == null || dropZone == null || seats == null || playSpot == null || discardSpot == null)
                Debug.LogError($"[{nameof(HandPresenter)}] {nameof(deck)} · {nameof(drawOrigin)} · {nameof(server)} · {nameof(dropZone)} · {nameof(seats)} · {nameof(playSpot)} · {nameof(discardSpot)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.OnCardDropped += HandleCardDropped;
        }

        private void OnDisable()
        {
            _view.OnCardDropped -= HandleCardDropped;
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

        /// <summary>
        /// 방장이 승인한 내 카드 사용 — 손패에서 떼어 테이블 자리에 눕혀 놓는다(1단계). 처리 결과가 올 때까지 그 자리에 둔다. 연출 큐가 기다릴 코루틴.
        /// 손패에 없으면 경고만 하고 끝난다.
        /// </summary>
        public IEnumerator PlayCardRoutine(int cardInstanceId)
        {
            CardView card = _view.TakeCard(cardInstanceId);
            if (card == null)
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 승인된 카드가 손패에 없다. instanceId={cardInstanceId}", this);
                yield break;
            }

            Transform t = card.transform;
            t.SetParent(playSpot.parent, true); // 손패 올리기 · 내리기에 끌려가지 않게
            card.SetRaycastBlocking(false);

            Vector3 spot = playSpot.position + playSpot.forward * (tableStackZ * _onTable.Count);
            _onTable[cardInstanceId] = card;

            t.DOKill();
            t.DOMove(spot, playDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DORotateQuaternion(playSpot.rotation * Quaternion.Euler(playTilt, 0f, 0f), playDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DOScale(1f, playDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            yield return new WaitForSeconds(playDuration);
        }

        /// <summary>
        /// 내는 자리에 있던 내 카드가 처리됐다(효과 적용 · 무효화) — 버림 더미로 보내 지운다. 연출 큐가 기다릴 코루틴.
        /// 내는 자리에 없는 카드(남의 카드 · 함정 칸에서 나온 카드)면 바로 끝난다.
        /// </summary>
        public IEnumerator DiscardPlayedRoutine(int cardInstanceId)
        {
            if (!_onTable.TryGetValue(cardInstanceId, out CardView card))
                yield break;

            _onTable.Remove(cardInstanceId);
            yield return FlyToDiscardRoutine(new List<CardView> { card });
        }

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

        /// <summary>내는 자리에 있던 내 카드가 손패로 돌아왔다 (04-card.md 7절 「손패로 반환」) — 손패 오른쪽 끝에 다시 붙인다. 내는 자리에 없으면 경고만.</summary>
        public void ReturnPlayedCard(int cardInstanceId)
        {
            if (!_onTable.TryGetValue(cardInstanceId, out CardView card))
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 손패로 돌아온 카드가 내는 자리에 없다. instanceId={cardInstanceId}", this);
                return;
            }

            _onTable.Remove(cardInstanceId);
            _view.PutBack(card);
        }

        // 카드들을 버림 더미의 맨 위 카드 자리로 보내고 지운다 — 닿는 자세가 더미의 맨 위 카드와 같아, 지운 뒤 더미가 그 카드를 맨 위로 보여 준다 (DiscardPilePresenter).
        private IEnumerator FlyToDiscardRoutine(List<CardView> cards)
        {
            if (cards.Count == 0)
                yield break;

            foreach (CardView card in cards)
            {
                Transform t = card.transform;
                t.SetParent(playSpot.parent, true); // 손패 올리기 · 내리기에 끌려가지 않게
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
            Debug.Log($"[{nameof(HandPresenter)}] 카드 사용 요청 instanceId={card.InstanceId} cardId={card.CardId} targets=[{string.Join(",", targets)}]", this);
            server.RequestPlayCard(card.InstanceId, targets);
        }
    }
}
