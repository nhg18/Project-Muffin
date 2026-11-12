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
    /// 사용 요청은 세 길로 온다 (10-ui.md 4절 「사용 요청」): 끌어서 사용 기준선 위에 놓기 · 끌어서 상대 위에 놓기(그 상대가 대상) · 더블클릭.
    /// 대상 타입에 따라 바로 요청하거나(자동 대상 — 방장이 정한다) 상대 좌석에서 대상을 고르게 한 뒤 요청한다. 고르는 동안 그 카드는 테이블 가운데에, 가림판 위에 밝게 둔다 (10-ui.md 6절).
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
        [Tooltip("대상을 고르는 카드(OneOther · TwoOthers)일 때 좌석 탭을 받는 곳. 끄는 카드가 어느 상대 위에 있는지도 묻는다.")]
        [SerializeField] private OpponentSeatsPresenter seats;
        [Tooltip("끄는 동안 보이는 사용 기준선.")]
        [SerializeField] private PlayLineView playLine;
        [Tooltip("손패가 올라와 덮는 HUD (내 패널 · 찹츄 버튼) — 덮인 동안 눌리지 않게 한다.")]
        [SerializeField] private CanvasGroup[] coveredHud = new CanvasGroup[0];

        [Header("대상 고르는 중의 카드 (10-ui.md 6절)")]
        [Tooltip("대상을 고르는 동안 카드를 HUD 가림판 위에 그리는 층.")]
        [SerializeField] private TopCardLayerView topLayer;
        [Tooltip("카드 가운데가 놓이는 자리 (손패 루트의 부모 기준 유닛) — 테이블 가운데, 캔버스 (960, 620).")]
        [SerializeField] private Vector3 targetCardCenter = new Vector3(0f, -0.8f, 0f);
        [Tooltip("그 자리에서의 배율 — 시안 0.74.")]
        [SerializeField] private float targetCardScale = 0.74f;
        [Tooltip("그 자리에서의 기울기 (반시계 도) — 시안 −4° (시계 방향이 +).")]
        [SerializeField] private float targetCardAngle = 4f;
        [Tooltip("손패에서 그 자리로 가는 초 — 시안 0.38.")]
        [SerializeField] private float targetCardDuration = 0.38f;

        [Header("새 카드 보여 주기 (10-ui.md 2-1 「새 카드 보여 주기」)")]
        [Tooltip("보여 주는 카드 아래의 남은 시간 막대 · 안내 글 (HUD).")]
        [SerializeField] private HandRevealView revealView;
        [Tooltip("보여 주는 초 — 기본 + 장마다 더하는 초, 상한까지.")]
        [SerializeField] private float showHoldBase = 2.6f;
        [SerializeField] private float showHoldPerCard = 0.7f;
        [SerializeField] private float showHoldMax = 8f;
        [Tooltip("앞 카드가 온 뒤 이 초 안에 온 카드는 같은 묶음으로 함께 보여 준다. 그 뒤에 오면 앞 묶음을 먼저 손패로 내린다.")]
        [SerializeField] private float showJoinWindow = 0.6f;
        [Tooltip("새 카드가 온 직후 이 초 동안은 눌러도 닫지 않는다 — 뽑으려고 누른 클릭에 닫히지 않게.")]
        [SerializeField] private float showClickGuard = 0.3f;

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

        // 새로 들어와 크게 보여 주는 중인 카드 묶음 — 손패(HandView)에는 이미 들어 있고 자리만 따로다. 판정과 상관없는 연출.
        private readonly List<CardView> _showing = new List<CardView>();
        private float _showLastAddAt;
        private Coroutine _showRoutine;

        private HandView _view;
        private HandInput _input;
        private bool _hudRestorePending; // 손패는 내려갔지만 더블클릭 창이 지날 때까지 덮였던 HUD 를 막아 둔다

        /// <summary>손패가 올라왔다 · 내려갔다 — 가운데 카드 · 반응 타이머가 비킨다 (10-ui.md 2-1 「카드 연출 자리」).</summary>
        public event System.Action<bool> OnRaisedChanged;

        private void Awake()
        {
            _view = GetComponent<HandView>();
            _input = GetComponent<HandInput>();

            if (deck == null || drawOrigin == null || server == null || seats == null || discardSpot == null || playLine == null || topLayer == null || revealView == null)
                Debug.LogError($"[{nameof(HandPresenter)}] {nameof(deck)} · {nameof(drawOrigin)} · {nameof(server)} · {nameof(seats)} · {nameof(discardSpot)} · {nameof(playLine)} · {nameof(topLayer)} · {nameof(revealView)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.OnCardDropped += HandleCardDropped;
            _view.OnCardDragged += HandleCardDragged;
            _view.OnCardUseRequested += HandleCardUseRequested;
            _view.OnRaisedChanged += HandleRaisedChanged;
            _view.OnDraggingChanged += HandleDraggingChanged;
        }

        private void OnDisable()
        {
            _view.OnCardDropped -= HandleCardDropped;
            _view.OnCardDragged -= HandleCardDragged;
            _view.OnCardUseRequested -= HandleCardUseRequested;
            _view.OnRaisedChanged -= HandleRaisedChanged;
            _view.OnDraggingChanged -= HandleDraggingChanged;

            // 씬이 내려가는 중이면 카드 재배치 · 층 전환 · HUD 호출을 하지 않는다 — 카메라 · 캔버스가 먼저 파괴됐을 수 있다
            if (gameObject.scene.isLoaded)
            {
                FinishShow();
                return;
            }

            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }

            _showing.Clear();
        }

        private void Update()
        {
            // 더블클릭 창이 지났다 — 막아 두었던 HUD 를 되살린다
            if (_hudRestorePending && !_input.IsAwaitingDoubleClick)
            {
                _hudRestorePending = false;
                foreach (CanvasGroup group in coveredHud)
                    group.blocksRaycasts = true;
            }

            // 보여 주는 중에 화면을 누르면 바로 손패로
            if (_showing.Count > 0 && Input.GetMouseButtonDown(0) && Time.time - _showLastAddAt > showClickGuard)
                FinishShow();

            double now = PhotonNetwork.Time;
            bool glow = _usableCounters.Count > 0 && now >= _reactionOpensAt && now < _reactionDeadline;
            if (glow == _glowing) return;

            _glowing = glow;
            _view.ShowReaction(glow ? _usableCounters : null);
        }

        /// <summary>반응 시간이 정해졌다 (OnReactionStarted) — 앞 반응 시간의 「낼 수 있는 카운터」 는 버린다. 새 목록은 ShowUsableCounters 로 따로 온다.</summary>
        public void ShowReaction(double opensAt, double deadline)
        {
            FinishShow(); // 카운터를 낼 손패 · 체인 카드를 가리지 않게
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
            _view.ShowReaction(null);
        }

        /// <summary>반응 시간이 끝났다 (새 카드가 체인에 오름 · 체인 처리 시작) — 빛을 끈다.</summary>
        public void EndReaction()
        {
            _usableCounters.Clear();
            _glowing = false;
            _view.ShowReaction(null);
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

        /// <summary>내 손패로 카드가 들어왔다 (훔침 · 교환 · 함정 반환, OnMyCardsGained) — 화면 아래에서 올라와 크게 보여 준 뒤 손패 오른쪽 끝에 붙는다.</summary>
        public void GainCards(int[] cardInstanceIds, int[] cardIds)
        {
            for (int i = 0; i < cardInstanceIds.Length; i++)
            {
                CardData data = deck.GetCard(cardIds[i]);
                if (data == null)
                    Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardIds[i]}", this);
                else
                    AddAndShow(cardInstanceIds[i], data, transform.position);
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

        /// <summary>버림 더미에서 가져온 카드 (109 아 맞다!) — 더미 자리에서 꺼내 뽑은 카드처럼 크게 보여 준 뒤 손패 끝에 붙인다.</summary>
        public void TakeFromDiscard(int cardInstanceId, int cardId, Vector3 from)
        {
            CardData data = deck.GetCard(cardId);
            if (data == null)
                Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardId}", this);
            else
                AddAndShow(cardInstanceId, data, from);
        }

        /// <summary>내가 뽑은 카드를 덱 자리에서 꺼내 크게 보여 준 뒤 손패 끝에 붙인다. 연출 큐는 꺼내는 간격만 기다린다 — 보여 주는 동안 다음 일은 이어진다.</summary>
        public IEnumerator DrawRoutine(int cardInstanceId, int cardId)
        {
            CardData data = deck.GetCard(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardId}", this);
                yield break;
            }

            AddAndShow(cardInstanceId, data, drawOrigin.position);
            yield return new WaitForSeconds(dealInterval);
        }

        // 손패에 새 카드를 넣되, 자리로 바로 보내지 않고 화면 가운데 위쪽에 크게 보여 준다 — 몇 장이든 같은 길 (10-ui.md 2-1 「새 카드 보여 주기」).
        // 잇달아 온 카드는 한 묶음으로 모아 함께 보여 주고(장수에 맞춰 크기를 다시 잡는다), 시간이 지나거나 화면을 누르면 손패로 내린다.
        // 반응 시간이 걸려 있으면(체인이 있다) 가운데를 가리지 않게 보여 주지 않고 바로 손패로.
        private void AddAndShow(int cardInstanceId, CardData data, Vector3 spawnWorldPosition)
        {
            if (PhotonNetwork.Time < _reactionDeadline)
            {
                _view.AddCard(cardInstanceId, data, spawnWorldPosition);
                return;
            }

            _showing.RemoveAll(c => !_view.IsShown(c)); // 보여 주는 사이 손패에서 빠진 카드 (강제 버림 · 훔쳐짐)
            if (_showing.Count > 0 && Time.time - _showLastAddAt > showJoinWindow)
                FinishShow();

            CardView card = _view.AddCard(cardInstanceId, data, spawnWorldPosition, true);
            topLayer.Set(card, true);
            _showing.Add(card);
            _showLastAddAt = Time.time;

            float bottom = _view.LayoutShown(_showing, _showing.Count - 1);
            float enter = _view.ShowEnterSeconds(1);
            float hold = Mathf.Min(showHoldMax, showHoldBase + showHoldPerCard * (_showing.Count - 1));
            revealView.Show(_view.WorldToCanvasY(bottom), enter, hold);

            if (_showRoutine != null)
                StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(ShowRoutine(enter + hold));
        }

        private IEnumerator ShowRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _showRoutine = null;
            FinishShow();
        }

        // 보여 주던 카드를 손패 자리로 내린다. 보여 주는 것이 없으면 아무 일도 없다.
        private void FinishShow()
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }

            if (_showing.Count == 0) return;

            foreach (CardView card in _showing)
            {
                if (_view.IsShown(card))
                    _view.ReturnCard(card);
            }

            _showing.Clear();
            revealView.Hide();
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

        // 끄는 동안 — 놓으면 사용되는 자리(기준선 위 · 상대 위)면 카드 링 · 기준선 색 · 그 좌석 강조로 알린다 (10-ui.md 4절).
        private void HandleCardDragged(CardView card, Vector2 screenPosition)
        {
            int seatActor = SeatUnder(card, screenPosition);
            bool armed = seatActor != 0 || _view.IsAbovePlayLine(screenPosition);
            _view.SetArmed(armed);
            playLine.SetArmed(armed);
            seats.ShowAim(seatActor);
        }

        private void HandleDraggingChanged(bool dragging)
        {
            playLine.SetVisible(dragging);
            if (!dragging)
                seats.ShowAim(0);
        }

        // 올라온 손패가 덮는 HUD 는 그동안 눌리지 않는다 — 카드를 고르려다 찹츄 버튼이 눌리지 않게 (프로토타입 v2 #game.handup).
        private void HandleRaisedChanged(bool raised)
        {
            // 카드를 골라 내려간 직후에는 더블클릭의 둘째 클릭이 올 수 있다 — 그 클릭이 찹츄 버튼에 떨어지지 않게 창이 지난 뒤에 되살린다 (Update)
            _hudRestorePending = !raised && _input != null && _input.IsAwaitingDoubleClick;
            if (!_hudRestorePending)
            {
                foreach (CanvasGroup group in coveredHud)
                    group.blocksRaycasts = !raised;
            }

            OnRaisedChanged?.Invoke(raised);
        }

        // 놓은 자리가 사용 기준선 위이거나 상대 위면 사용, 아니면 손패로 (10-ui.md 4절 「끌다가 놓기」).
        private void HandleCardDropped(CardView card, Vector2 screenPosition)
        {
            int seatActor = SeatUnder(card, screenPosition);
            if (seatActor == 0 && !_view.IsAbovePlayLine(screenPosition))
            {
                _view.ReturnCard(card);
                return;
            }

            Use(card, seatActor);
        }

        // 더블클릭 · 고른 카드 다시 탭 — 놓은 상대가 없다.
        private void HandleCardUseRequested(CardView card) => Use(card, 0);

        // 끄는 카드 아래의 상대 (actorNumber, 없으면 0). 함정은 대상이 없어 상대 위에 놓는 것이 뜻이 없다.
        private int SeatUnder(CardView card, Vector2 screenPosition)
        {
            CardData data = deck.GetCard(card.CardId);
            return data != null && data.type != CardType.Trap ? seats.ActorAt(screenPosition) : 0;
        }

        // 사용 요청 (04-card.md 5 ~ 7 · 11절). 자동 타입은 빈 대상으로 바로 요청(방장이 정한다), 고르는 타입은 놓은 상대가 있으면 그 상대로 · 없으면 좌석 탭으로.
        // 로컬 조건은 보지 않는다 — 방장이 거절하면 토스트 (10-ui.md 4절).
        private void Use(CardView card, int seatActor)
        {
            _view.Lower();

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
                case TargetType.OneOther when seatActor != 0:
                    RequestPlay(card, new[] { seatActor });
                    _view.ReturnCard(card);
                    break;
                case TargetType.OneOther:
                    StartCoroutine(PlayWithTargetsRoutine(card, data.cardName, 1, false));
                    break;
                case TargetType.TwoOthers:
                    StartCoroutine(PlayWithTargetsRoutine(card, data.cardName, 2, false));
                    break;
                case TargetType.OneAny when seatActor != 0: // 상대 위에 놓았다 — 그 상대로
                    RequestPlay(card, new[] { seatActor });
                    _view.ReturnCard(card);
                    break;
                case TargetType.OneAny: // 나를 포함한 1명 — 내 패널도 고를 수 있다
                    StartCoroutine(PlayWithTargetsRoutine(card, data.cardName, 1, true));
                    break;
                default:
                    RequestPlay(card, Array.Empty<int>());
                    _view.ReturnCard(card);
                    break;
            }
        }

        // 대상 선택 동안 손패를 잠그고, 카드는 손패에서 빼 테이블 가운데에 가림판 위로 밝게 둔다. 안내 바에 카드 이름을 보여 준다.
        // 고르면 요청, 취소 · 시간 초과면 요청하지 않는다. 끝나면 풀고 카드를 손패 자리로 돌린다 (10-ui.md 6절).
        private IEnumerator PlayWithTargetsRoutine(CardView card, string cardName, int targetCount, bool includeMe)
        {
            _view.SetLocked(true);
            _view.Hold(card);
            topLayer.Set(card, true);
            ShowAtTargetSpot(card);

            int[] targets = null;
            yield return seats.SelectTargetsRoutine(cardName, targetCount, includeMe, picked => targets = picked);

            if (card == null) // 고르는 사이 손패에서 빠져 지워졌다 (강제 버림 · 사망)
            {
                _view.SetLocked(false);
                yield break;
            }

            if (targets != null)
                RequestPlay(card, targets);
            else
                Debug.Log($"[{nameof(HandPresenter)}] 대상 선택 취소 — 카드 {card.InstanceId} 손패로", this);

            topLayer.Set(card, false);
            _view.SetLocked(false);
            _view.ReturnCard(card);
        }

        // 카드를 테이블 가운데로 — 똑바로 세워 조금 작게 · 살짝 기울여 (프로토타입 v2 startTarget).
        private void ShowAtTargetSpot(CardView card)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, targetCardAngle);
            Transform parent = transform.parent;
            Vector3 center = parent != null ? parent.TransformPoint(targetCardCenter) : targetCardCenter;
            Vector3 position = center - rotation * new Vector3(0f, card.Height * targetCardScale / 2f, 0f); // 카드 기준점은 아래 가운데

            Transform t = card.transform;
            t.DOKill();
            t.DOMove(position, targetCardDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DORotateQuaternion(rotation, targetCardDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DOScale(targetCardScale, targetCardDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
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
