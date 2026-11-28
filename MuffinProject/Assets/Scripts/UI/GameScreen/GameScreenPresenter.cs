using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.Fx;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 인게임 화면의 연출 큐 (CODE_CONVENTION 9절 · 09-network.md 6.1). 방장이 보낸 일어난 일(GameEvents)을 받은 순서대로 줄 세우고,
    /// 하나씩 영역 프레젠터에 넘겨 연출이 끝난 뒤 다음을 꺼낸다. 화면 값은 재생하는 순간 이벤트에 담긴 값으로 바뀐다. 판정은 하지 않는다.
    /// 받는 순서 = 일어난 순서다 (GameServer 가 쌓은 순서대로 PunGameServer 가 보내고, 같은 보낸이의 RPC 는 순서가 지켜진다).
    /// 큐를 타지 않는 것: 내 요청 거절(RejectToastPresenter 가 바로 띄움) · 남은 초(TurnTimerPresenter 가 매 프레임 계산) · 퇴장(RoomEvents) · 늦게 열렸을 때의 스냅샷 복구(각 프레젠터 Start)
    /// · 곧 씬이 바뀌는 알림의 경고 예약과 방 나가기 (내 강제 퇴장 · 판 중지 — GameMenuPresenter 를 받은 자리에서 바로 부른다).
    /// 체인: 카드가 나오면(OnCardPlayed) ChainCardsPresenter 가 크게 보여 주고, 반응 시간(OnReactionStarted)이 오면 그 아래에 반응 타이머. 처리 결과가 오면 카드를 치우고 체인이 비면 타이머를 숨긴다 (10-ui.md 5절).
    /// 버림 더미: 버림이 생기는 일(효과 적용 · 무효화 · 강제 버림 · 함정 파괴 · 사망 · 나감)마다 내 카드가 있으면 더미로 보낸 뒤 맨 위 카드 · 장수를 바꾼다 (10-ui.md 2-1 「버림 더미」).
    /// 사망 대기는 방장이 그 자리에서 끝내 화면에 오지 않는다 (06-health.md 8절) — HP 0 뒤에 살아남(OnDeathAvoided) 또는 최종 사망(OnPlayerDied)이 바로 온다.
    /// 「뽑고 턴 끝」 버튼은 재생한 기준으로 내 턴 · 메인 행동 전 · 체인 없음 · 대상 선택 아님일 때만 보인다 (통합 화면 UI v3 .drawbtn). 표시만 — 판정은 방장.
    /// </summary>
    public class GameScreenPresenter : MonoBehaviour
    {
        private const string MyChapchuShout = "찹츄!";            // 10-ui.md 5-2 #1 — 내가 선언했을 때
        private const string OtherChapchuShoutFormat = "{0} 찹츄!"; // 남이 선언했을 때. {0} = 이름

        [SerializeField] private DeckPresenter deck;
        [SerializeField] private DiscardPilePresenter discardPile;
        [SerializeField] private HandPresenter hand;
        [SerializeField] private MyPanelPresenter myPanel;
        [SerializeField] private OpponentSeatsPresenter seats;
        [Tooltip("오른쪽 세로 턴 순서.")]
        [SerializeField] private TurnOrderPresenter turnOrder;
        [SerializeField] private TurnTimerPresenter turnTimer;
        [SerializeField] private HistoryPresenter history;
        [Tooltip("판 효과 줄 — 화면 위 가운데 (기다려! · 피폭).")]
        [SerializeField] private BoardEffectsPresenter boardEffects;
        [Tooltip("1021 개판 오분전 — 시작 제목 · 자명종 · 판정 팻말.")]
        [SerializeField] private GaepanPresenter gaepan;
        [Tooltip("체인에 올라간 카드 — 낸 카드를 모든 화면에 크게 보여 주고 처리되면 버림 더미로.")]
        [SerializeField] private ChainCardsPresenter chainCards;
        [SerializeField] private ReactionTimerPresenter reactionTimer;
        [Tooltip("카드 효과 고르기 판 (1019 · 카운터의 대가) — 남이 고를 때도 같은 판을 연출 큐 순서대로. 버려진 내 카드의 장 번호도 받은 자리에서 바로 넘긴다.")]
        [SerializeField] private ChoicePopupPresenter choicePopup;
        [SerializeField] private MyTrapsPresenter myTraps;
        [Tooltip("카드 연출 3 단계(카드별 능력 발동) · 개판 등급 4 단계 — 효과가 적용된 카드의 연출을 그때 불러와 화면 전체에 (10-ui.md 5-1).")]
        [SerializeField] private CardFxPresenter cardFx;
        [Tooltip("화면 덮기 — 효과가 적용될 때 카드 데이터의 색으로 덮었다 걷는다 (10-ui.md 5-1, 1013).")]
        [SerializeField] private ScreenFlashView screenFlash;
        [Tooltip("찹츄 선언 연출 — 집중선 + 큰 말풍선 (10-ui.md 5-2 #1).")]
        [SerializeField] private ChapchuShoutView chapchuShout;
        [Tooltip("4 단계 공통 연출 — 피해 · 회복 · 카드 얻기 · 잃기 · 피폭 (10-ui.md 5-1 「4 단계 공통 연출」).")]
        [SerializeField] private ResultFx resultFx;
        [Tooltip("빼앗기 · 주고받기 — 잃는 사람의 「−n장」 뒤 얻는 사람의 「+n장」 까지 초 (시안 steal: 0.18초 + 카드가 건너가는 0.42초).")]
        [SerializeField] private float handMoveSeconds = 0.6f;
        [Tooltip("PunGameServer 와 같은 덱 에셋 — 처리된 카드의 화면 연출 값을 읽는다.")]
        [SerializeField] private DeckData cardDeck;
        [Tooltip("설정 · 나가기 — 강제 퇴장 · 판 중지 알림을 넘긴다. 비어 있으면 씬에서 찾는다.")]
        [SerializeField] private GameMenuPresenter menu;

        // 단계 하나 = 영역에 값을 넣고, 기다릴 연출이 있으면 그 코루틴을 돌려준다 (없으면 null).
        private readonly Queue<Func<IEnumerator>> _steps = new Queue<Func<IEnumerator>>();
        private Coroutine _playing;

        // 대상 표시 (10-ui.md 5-2 #3) — 체인 맨 위 카드의 대상과, 카드에서 대상마다 그리는 빨간 점선 (조준 화살표를 복제)
        private int[] _markedTargets = Array.Empty<int>();
        private readonly List<AimArrowView> _targetArrows = new List<AimArrowView>();

        // 덱의 카드 그림 — 게임 화면이 떠 있는 동안 붙들어 둔다. 판 도중에는 불러오지 않는다 (26-card-data.md 6-3)
        private List<Sprite> _cardArt;

        // 내가 뽑으면 카드 내용(OnMyCardDrawn, 나에게만)이 장수(OnCardDrawn, 전원)보다 먼저 온다 — 둘을 한 단계로 묶는다.
        private bool _hasPendingMyCard;
        private int _pendingCardInstanceId;
        private int _pendingCardId;

        // 화면에 재생한 기준으로 체인에 남은 카드 수 — 0 이 되면 반응 타이머를 숨긴다. 연출 큐 단계 안에서만 바꾼다.
        private int _chainCount;

        // 「뽑고 턴 끝」 버튼 · 손패의 「지금 낼 수 있는 카드」 표시 (시안 v3.5) 의 조건 — 재생한 기준. 연출 큐 단계 안에서만 바꾼다.
        private bool _isMyTurn;
        private bool _myMainActionDone; // 이번 내 턴에 카드를 냈다 — 뽑기는 메인 행동이라 더 못 한다 (03-turn.md 3절)
        private bool _myActionBlocked;  // 이번 턴 행동 카드 금지 (1007 짖기) — 행동 카드를 흐리게
        private readonly HashSet<int> _deadActors = new HashSet<int>();    // 재생한 기준으로 죽은 사람 — 카드 연출에 넘기는 「판에 남은 사람」 에서 뺀다
        private bool _reversed; // 재생한 기준의 턴 방향 — 카드 연출에 넘기는 「받는 사람」 순서 (1018). 연출 큐 단계 안에서만 바꾼다.

        // 4 단계 공통 연출 (10-ui.md 5-1). 재생한 기준의 HP — 바뀐 양으로 피해 · 회복을 고른다. 없으면 처음 HP(MaxHp).
        private readonly Dictionary<int, int> _shownHp = new Dictionary<int, int>();
        // 아래는 받은 기준 — 이벤트가 오는 순서로 정하고, 줄을 세울 때 단계에 담는다
        private bool _resolving;       // 체인 처리 중 — 효과 적용(OnCardResolved)부터 체인이 버림 더미로 갈 때까지. 이때 뽑은 카드만 「+n장」
        private HpSource _hpSource;    // 뒤따르는 HP 변화가 무엇에서 왔는지 — 다른 단계를 줄 세우면 보통으로 돌아간다
        private int _gaepanLoser = -1; // 개판 판정의 HP 0 은 피해가 아니라 설정 (11-card-list 1021) — 연출 없이
        private DrawBatch _drawBatch;  // 바로 앞에 줄 세운 단계가 효과로 뽑은 카드면 그 묶음 — 같은 사람이 이어 뽑으면 붙인다

        private enum HpSource { Normal, Fallout, Quiet }

        // 효과 하나로 여러 장을 뽑으면 한 장씩 오지만(OnCardDrawn) 「+n장」 은 마지막 장이 닿을 때 한 번 (시안 draw)
        private sealed class DrawBatch
        {
            public int Actor;
            public int Count;
            public int Played;
        }

        private void Awake()
        {
            if (deck == null || discardPile == null || hand == null || myPanel == null || seats == null || turnOrder == null || turnTimer == null || history == null || boardEffects == null || chainCards == null || reactionTimer == null || choicePopup == null || myTraps == null || screenFlash == null || chapchuShout == null || resultFx == null || cardDeck == null || cardFx == null)
                Debug.LogError($"[{nameof(GameScreenPresenter)}] 영역 프레젠터 참조 중 비어 있는 것이 있습니다.", this);

            if (cardDeck != null)
                _cardArt = cardDeck.LoadArt();

            if (menu == null)
                menu = FindFirstObjectByType<GameMenuPresenter>(FindObjectsInactive.Include); // 씬에 아직 연결하지 않았을 때
            if (menu == null)
                Debug.LogError($"[{nameof(GameScreenPresenter)}] {nameof(GameMenuPresenter)} 를 찾지 못했습니다 — 강제 퇴장 · 판 중지를 처리하지 못합니다.", this);
        }

        // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값 (09-network.md 6.1) — 피해 · 회복 양의 기준 HP, 내 턴인지. 봇의 값은 방 속성에 있다 (18-bot.md 9절).
        private void Start()
        {
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때

            foreach (int actor in PlayerLook.OrderedActors())
                if (BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object hp) && hp is int hpValue)
                    _shownHp[actor] = Mathf.Clamp(hpValue, 0, GameServer.MaxHp); // 방 · Player 속성은 남이 쓸 수 있다

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue)
                _isMyTurn = IsMe(turnActorValue);
            _reversed = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.TurnDirection, out object reversed) && reversed is bool reversedValue && reversedValue;
            if (_playing == null)
                ShowHandPlayState(false);
        }

        private void OnEnable()
        {
            hand.OnRaisedChanged += HandleHandRaised;
            chainCards.OnBottomChanged += reactionTimer.SetCardsBottom;
            chainCards.OnTopTargetsChanged += HandleTopTargetsChanged;
            hand.OnReactionAsked += reactionTimer.SetSkipVisible;
            reactionTimer.OnSkipClicked += hand.SkipReaction;
            GameEvents.OnHpChanged += HandleHpChanged;
            GameEvents.OnCardDrawn += HandleCardDrawn;
            GameEvents.OnMyCardDrawn += HandleMyCardDrawn;
            GameEvents.OnDeckRefilled += HandleDeckRefilled;
            GameEvents.OnTurnStarted += HandleTurnStarted;
            GameEvents.OnTurnResumed += HandleTurnResumed;
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
            GameEvents.OnChainDiscarded += HandleChainDiscarded;
            GameEvents.OnCardReturnedToHand += HandleCardReturnedToHand;
            GameEvents.OnDiscardTaken += HandleDiscardTaken;
            GameEvents.OnTurnDirectionChanged += HandleTurnDirectionChanged;
            GameEvents.OnTurnSkipped += HandleTurnSkipped;
            GameEvents.OnTurnSkipCleared += HandleTurnSkipCleared;
            GameEvents.OnTurnSkipMarked += HandleTurnSkipMarked;
            RoomEvents.OnMasterClientSwitched += HandleMasterClientSwitched;
            GameEvents.OnActionBlockChanged += HandleActionBlockChanged;
            GameEvents.OnWaitChanged += HandleWaitChanged;
            GameEvents.OnChapchuWinHeld += HandleChapchuWinHeld;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnFalloutChanged += HandleFalloutChanged;
            GameEvents.OnGaepanChanged += HandleGaepanChanged;
            GameEvents.OnChoiceStarted += HandleChoiceStarted;
            GameEvents.OnChoiceMade += HandleChoiceMade;
            GameEvents.OnGaepanJudged += HandleGaepanJudged;
            GameEvents.OnCardsDiscarded += HandleCardsDiscarded;
            GameEvents.OnTrapDestroyed += HandleTrapDestroyed;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnDeathAvoided += HandleDeathAvoided;
            GameEvents.OnPlayerLeftGame += HandlePlayerLeftGame;
            GameEvents.OnGameHalted += HandleGameHalted;
            GameEvents.OnChapchuChanged += HandleChapchuChanged;
        }

        private void OnDisable()
        {
            hand.OnRaisedChanged -= HandleHandRaised;
            chainCards.OnBottomChanged -= reactionTimer.SetCardsBottom;
            chainCards.OnTopTargetsChanged -= HandleTopTargetsChanged;
            hand.OnReactionAsked -= reactionTimer.SetSkipVisible;
            reactionTimer.OnSkipClicked -= hand.SkipReaction;
            GameEvents.OnHpChanged -= HandleHpChanged;
            GameEvents.OnCardDrawn -= HandleCardDrawn;
            GameEvents.OnMyCardDrawn -= HandleMyCardDrawn;
            GameEvents.OnDeckRefilled -= HandleDeckRefilled;
            GameEvents.OnTurnStarted -= HandleTurnStarted;
            GameEvents.OnTurnResumed -= HandleTurnResumed;
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
            GameEvents.OnChainDiscarded -= HandleChainDiscarded;
            GameEvents.OnCardReturnedToHand -= HandleCardReturnedToHand;
            GameEvents.OnDiscardTaken -= HandleDiscardTaken;
            GameEvents.OnTurnDirectionChanged -= HandleTurnDirectionChanged;
            GameEvents.OnTurnSkipped -= HandleTurnSkipped;
            GameEvents.OnTurnSkipCleared -= HandleTurnSkipCleared;
            GameEvents.OnTurnSkipMarked -= HandleTurnSkipMarked;
            RoomEvents.OnMasterClientSwitched -= HandleMasterClientSwitched;
            GameEvents.OnActionBlockChanged -= HandleActionBlockChanged;
            GameEvents.OnWaitChanged -= HandleWaitChanged;
            GameEvents.OnChapchuWinHeld -= HandleChapchuWinHeld;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnFalloutChanged -= HandleFalloutChanged;
            GameEvents.OnGaepanChanged -= HandleGaepanChanged;
            GameEvents.OnChoiceStarted -= HandleChoiceStarted;
            GameEvents.OnChoiceMade -= HandleChoiceMade;
            GameEvents.OnGaepanJudged -= HandleGaepanJudged;
            GameEvents.OnCardsDiscarded -= HandleCardsDiscarded;
            GameEvents.OnTrapDestroyed -= HandleTrapDestroyed;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnDeathAvoided -= HandleDeathAvoided;
            GameEvents.OnPlayerLeftGame -= HandlePlayerLeftGame;
            GameEvents.OnGameHalted -= HandleGameHalted;
            GameEvents.OnChapchuChanged -= HandleChapchuChanged;

            if (_playing != null)
            {
                StopCoroutine(_playing);
                _playing = null;
            }

            _steps.Clear();
            _hasPendingMyCard = false;
            _chainCount = 0;
            _myActionBlocked = false;
            _shownHp.Clear();
            _resolving = false;
            _hpSource = HpSource.Normal;
            _gaepanLoser = -1;
            _drawBatch = null;
        }

        // 「뽑고 턴 끝」 버튼 · 마지막 초 째깍 소리 — 재생한 상태로 매 프레임 (대상 선택은 큐 밖에서 열리고 닫힌다)
        private void LateUpdate()
        {
            bool canAct = _isMyTurn && !_myMainActionDone && _chainCount == 0;
            deck.ShowCanDraw(canAct && !seats.IsSelectingTargets);
            turnTimer.TickForMe = canAct;
            UpdateTargetArrows();
        }

        // 체인 맨 위 카드의 대상이 바뀌었다 — 대상 좌석 · 내 패널을 빨갛게, 점선은 LateUpdate 가 카드를 따라 그린다. 화면만의 연출 (판정은 방장)
        private void HandleTopTargetsChanged(int[] targets)
        {
            _markedTargets = targets;
            seats.ShowTargeted(targets);
            myPanel.ShowTargeted(Array.IndexOf(targets, PhotonNetwork.LocalPlayer.ActorNumber) >= 0);

            AimArrowView template = hand.AimArrow;
            while (template != null && _targetArrows.Count < targets.Length)
            {
                AimArrowView arrow = Instantiate(template, template.transform.parent);
                arrow.name = "TargetArrow";
                arrow.transform.SetSiblingIndex(template.transform.GetSiblingIndex()); // 끄는 카드의 조준 화살표가 위에 그려지게
                arrow.Hide();
                _targetArrows.Add(arrow);
            }

            for (int i = targets.Length; i < _targetArrows.Count; i++)
                _targetArrows[i].Hide();
        }

        // 카드 가장자리에서 대상 캐릭터까지 — 카드가 비키거나(손패 올림) 줄이 바뀌어도 따라간다. 대상 자리가 없으면(나간 사람) 그 점선만 끈다
        private void UpdateTargetArrows()
        {
            for (int i = 0; i < _markedTargets.Length && i < _targetArrows.Count; i++)
            {
                int target = _markedTargets[i];
                Vector2 to;
                bool found;
                if (IsMe(target))
                {
                    to = myPanel.CharacterScreenPoint;
                    found = true;
                }
                else
                {
                    found = seats.TryGetCharacterScreenPoint(target, out to);
                }

                if (found && chainCards.TryGetTopCardEdge(to, out Vector2 from))
                    _targetArrows[i].Show(from, to, true);
                else
                    _targetArrows[i].Hide();
            }
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
            HpSource batchSource = _hpSource; // 피폭 · 새 판이면 뒤따르는 모두의 HP 가 같은 묶음
            HpSource source = batchSource;
            if (actorNumber == _gaepanLoser && hp == 0)
            {
                source = HpSource.Quiet;
                _gaepanLoser = -1;
            }

            Enqueue(() =>
            {
                if (isMe)
                    myPanel.ShowHp(hp);
                else
                    seats.ShowHp(actorNumber, hp);
                turnOrder.ShowHp(actorNumber, hp);
                history.ShowHpChanged(actorNumber, hp);
                PlayHpResult(actorNumber, hp, source);
                return null;
            });
            _hpSource = batchSource;
        }

        // 4 단계 공통 연출 — 줄면 피해(피폭이면 자주), 늘면 회복. 양 = 재생한 HP 와의 차 (10-ui.md 5-1 · 5-2)
        private void PlayHpResult(int actorNumber, int hp, HpSource source)
        {
            int before = _shownHp.TryGetValue(actorNumber, out int shown) ? shown : GameServer.MaxHp;
            _shownHp[actorNumber] = hp;
            int change = hp - before;
            if (change == 0 || source == HpSource.Quiet || !TryGetFxSeat(actorNumber, out ResultFx.Seat seat))
                return;

            if (change > 0)
                resultFx.Heal(seat, change);
            else if (source == HpSource.Fallout)
                resultFx.Zap(seat, -change);
            else
                resultFx.Hit(seat, -change);
        }

        private void HandleMyCardDrawn(int cardInstanceId, int cardId)
        {
            if (_hasPendingMyCard)
                Debug.LogWarning($"[{nameof(GameScreenPresenter)}] 카드 내용이 연달아 왔다 — 앞 카드(instanceId={_pendingCardInstanceId})의 장수 이벤트가 없었다.", this);

            _hasPendingMyCard = true;
            _pendingCardInstanceId = cardInstanceId;
            _pendingCardId = cardId;
            if (choicePopup != null)
                choicePopup.NoteLeftDiscard(cardInstanceId);
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

            // 효과로 뽑았으면(체인 처리 중) 묶음에 넣는다 — 턴의 뽑기 · 시간 초과 뽑기 · 새 판 배분은 4 단계가 아니다
            DrawBatch batch = null;
            if (_resolving)
            {
                batch = _drawBatch != null && _drawBatch.Actor == actorNumber ? _drawBatch : new DrawBatch { Actor = actorNumber };
                batch.Count++;
            }

            Enqueue(() =>
            {
                deck.ShowDeckCount(deckCount);
                history.ShowCardDrawn(actorNumber);
                IEnumerator fly;
                if (!isMe)
                    fly = seats.DrawRoutine(actorNumber, handCount);
                else
                {
                    myPanel.ShowHandCount(handCount);
                    fly = hasCard ? hand.DrawRoutine(cardInstanceId, cardId) : null;
                }
                SoundManager.Play(Sfx.CardDraw);
                return batch == null ? fly : DrawnRoutine(fly, batch);
            });
            _drawBatch = batch;
        }

        // 뽑은 카드가 닿은 뒤 — 묶음의 마지막 장이면 「+n장」
        private IEnumerator DrawnRoutine(IEnumerator fly, DrawBatch batch)
        {
            if (fly != null)
                yield return fly;
            batch.Played++;
            if (batch.Played == batch.Count && TryGetFxSeat(batch.Actor, out ResultFx.Seat seat))
                resultFx.Gain(seat, batch.Count);
        }

        private void HandleDeckRefilled(int deckCount)
        {
            if (choicePopup != null)
                choicePopup.ClearDiscarded(); // 버림 더미가 통째로 덱이 됐다 — 누가 뽑아 가도 번호가 남지 않게

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
            _resolving = false;

            Enqueue(() =>
            {
                _isMyTurn = isMyTurn;
                _myMainActionDone = false;
                deck.ShowTurnChanged();
                myPanel.ShowTurn(isMyTurn);
                seats.ShowTurn(actorNumber);
                turnOrder.ShowTurn(actorNumber);
                turnTimer.ShowTurn(deadline);
                history.ShowTurnStarted(actorNumber);
                if (isMyTurn)
                    SoundManager.Play(Sfx.MyTurn);
                return null;
            });
        }

        // 체인 · 함정으로 멈췄던 턴이 이어졌다 (메인 행동 전) — 같은 사람의 턴, 새 마감으로 다시 센다.
        private void HandleTurnResumed(int actorNumber, double deadline)
        {
            Enqueue(() =>
            {
                turnTimer.ShowTurn(deadline);
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
                if (isMe && _isMyTurn)
                    _myMainActionDone = true;
                reactionTimer.Hide(); // 3 단계 동안은 숨는다 (시안 .g3big) — 새 반응 시간이 바로 뒤따라온다
                hand.EndReaction();
                hand.NoteChainTop(targets != null ? targets.Length : 0);
                history.ShowCardPlayed(actorNumber, cardInstanceId, cardId, targets);
                if (isMe)
                    myPanel.ShowHandCount(handCount);
                else
                    seats.ShowHandCount(actorNumber, handCount);

                return DevStageMeter.Wrap(DevStageMeter.Play, chainCards.PlayRoutine(actorNumber, cardInstanceId, cardId, targets, isMe ? hand.TakeCard(cardInstanceId) : null));
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
                history.ShowTrapActivated(actorNumber, cardInstanceId, cardId, targets);

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

                // 함정은 반응 시간이 없다 — 공개된 뒤 읽는 시간(방장과 같은 값)이 지나야 뒤따르는 처리 결과를 재생한다. 여러 장이 터져도 한 장씩 (04-card.md 6절)
                return chainCards.PlayRoutine(actorNumber, cardInstanceId, cardId, targets, null, slot, (float)GameServer.TrapReadSeconds);
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
                history.ShowTrapSet(actorNumber);
                SoundManager.Play(Sfx.TrapSet); // 내 것은 손패의 카드가 칸에 닿은 뒤 (OnMyTrapSet 이 먼저 줄 선다)

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
                history.ShowHandCardsMoved(from, to, count);
                if (count == 0)
                {
                    ShowHandCount(to, toHandCount);
                    return null;
                }
                if (TryGetFxSeat(from, out ResultFx.Seat seat))
                    resultFx.Lose(seat, count);
                return CardsArriveRoutine(to, toHandCount, count);
            });
        }

        // 시안 steal — 잃는 사람 「−n장」 → 카드가 건너가는 사이 → 얻는 사람의 장수가 바뀌며 「+n장」
        private IEnumerator CardsArriveRoutine(int actorNumber, int handCount, int count)
        {
            yield return new WaitForSeconds(handMoveSeconds);
            ShowHandCount(actorNumber, handCount);
            if (TryGetFxSeat(actorNumber, out ResultFx.Seat seat))
                resultFx.Gain(seat, count);
        }

        private void HandleMyCardsGained(int[] cardInstanceIds, int[] cardIds)
        {
            if (choicePopup != null)
                choicePopup.NoteLeftDiscard(cardInstanceIds);

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

        // 카드가 체인에 오를 때마다 반응 시간이 새로 정해진다. 남은 시간은 보이지 않는다 — 내가 낼 수 있는 카운터가 있으면
        // 열린 동안 초록 빛 + 「스킵」 버튼(hand.OnReactionAsked)만 뜬다 (10-ui.md 5절).
        private void HandleReactionStarted(double opensAt)
        {
            Enqueue(() =>
            {
                reactionTimer.SetCardName(chainCards.TopCardName);
                hand.ShowReaction(opensAt);
                DevStageMeter.Begin(DevStageMeter.Wait); // 다음 단계가 시작되면 끝난다
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

        // 효과 적용 · 무효화 — 카드는 그 자리에 남고, 체인이 끝나면 쓴 순서대로 버림 더미로 간다 (HandleChainDiscarded, 04-card.md 9.1). 도장 연출은 여기에 넣는다 (10-ui.md 5-1).
        // 효과 적용이면 3 단계(카드별 연출, 개판 등급은 카드만의 4 단계까지)를 끝까지 재생한 뒤 다음 단계(뒤따르는 결과 = 4 단계 공통 연출)로.
        // 화면 덮기가 있으면 3 단계 뒤 덮고 기다리지 않는다 — 뒤따르는 효과(새 손패)가 걷히는 사이 보인다 (1013).
        private void HandleCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount)
        {
            Enqueue(() =>
            {
                history.ShowCardResolved(actorNumber, cardInstanceId, cardId, targets);
                LeaveChain();
                CardData data = cardDeck.GetCard(cardId);
                return DevStageMeter.Wrap(DevStageMeter.Resolve, data != null ? CardResolvedRoutine(data, actorNumber, targets) : null, DevStageMeter.After);
            });
            _resolving = true;
        }

        private IEnumerator CardResolvedRoutine(CardData data, int caster, int[] targets)
        {
            List<CardFxSeat> fxSeats = CreateFxSeats();
            CardFxSeat casterSeat = fxSeats.Find(seat => seat.Actor == caster) ?? GaepanCasterSeat(data, caster);
            if (casterSeat != null && !DevToolsPanel.EffectsOff) // 개발자 도구 「이펙트 끄기」 — 카드별 연출을 건너뛴다
            {
                var targetSeats = new List<CardFxSeat>();
                foreach (int target in targets)
                {
                    CardFxSeat seat = fxSeats.Find(other => other.Actor == target);
                    if (seat != null)
                        targetSeats.Add(seat);
                }
                yield return cardFx.PlayRoutine(data, casterSeat, targetSeats, fxSeats, ReceiversOf(fxSeats, casterSeat, targetSeats));
            }
            screenFlash.Play(data.resolveFlashColor, data.resolveFlashSeconds);
        }

        // 1021 개판 시작 컷인은 쓴 사람이 나가거나 죽었어도 시작 연출이다 (10-ui.md 「개판 시작 연출」) — 판에 자리가 없으면 연출 자리를 대신 둔다. 이 컷인은 쓴 사람 자리를 쓰지 않는다
        private CardFxSeat GaepanCasterSeat(CardData data, int caster)
        {
            if (!data.effects.Exists(effect => effect is Chapchu.Game.Effects.StartGaepanEffect))
                return null;
            return new CardFxSeat
            {
                Actor = caster,
                Anchor = cardFx.transform as RectTransform,
                Name = PlayerLook.NameOf(caster),
                Color = PlayerLook.ColorOf(caster),
                Hp = _shownHp.TryGetValue(caster, out int hp) ? hp : 0,
                IsMe = IsMe(caster),
            };
        }

        // 카드 연출이 아는 사람들 — 판에 남은 사람(재생한 기준), 좌석 순서(나 먼저). HP 는 재생한 기준(결과가 적용되기 전)
        private List<CardFxSeat> CreateFxSeats()
        {
            var fxSeats = new List<CardFxSeat>();
            foreach (int actor in PlayerLook.OrderedActors())
            {
                if (_deadActors.Contains(actor) || !TryGetFxSeat(actor, out ResultFx.Seat seat))
                    continue;
                fxSeats.Add(new CardFxSeat
                {
                    Actor = actor,
                    Anchor = seat.Body,
                    Name = PlayerLook.NameOf(actor),
                    Color = PlayerLook.ColorOf(actor),
                    Hp = _shownHp.TryGetValue(actor, out int hp) ? hp : GameServer.MaxHp,
                    IsMe = IsMe(actor),
                });
            }
            return fxSeats;
        }

        // 카드를 한 장씩 받는 사람 (1018 무야호 — 쓴 사람부터 턴 순서대로, 대상은 빼고). 대상 손패가 모자라면 재생한 기준의 대상 장수까지만 받는다.
        // 턴 순서 = 방장이 섞은 순서(PlayerLook.TurnOrderActors), 역방향이면 거꾸로 (03-turn.md 6절 #4 — 재생한 기준의 방향). 받는 사람을 정하는 카드가 아니면 연출이 쓰지 않는다.
        private List<CardFxSeat> ReceiversOf(List<CardFxSeat> fxSeats, CardFxSeat caster, List<CardFxSeat> targets)
        {
            List<int> turnOrder = PlayerLook.TurnOrderActors();
            fxSeats = fxSeats.OrderBy(seat => turnOrder.IndexOf(seat.Actor) is int index && index >= 0 ? index : int.MaxValue).ToList();
            int limit = targets.Count == 1 && TryGetShownHandCount(targets[0].Actor, out int targetHand) ? targetHand : int.MaxValue;
            int start = fxSeats.IndexOf(caster);
            var receivers = new List<CardFxSeat>();
            for (int i = 0; i < fxSeats.Count && receivers.Count < limit; i++)
            {
                int index = _reversed ? start - i : start + i;
                CardFxSeat seat = fxSeats[(index % fxSeats.Count + fxSeats.Count) % fxSeats.Count];
                if (!targets.Contains(seat))
                    receivers.Add(seat);
            }
            return receivers;
        }

        // 재생한 기준의 손패 장수 — 화면에 보이는 값
        private bool TryGetShownHandCount(int actorNumber, out int handCount)
        {
            if (IsMe(actorNumber))
            {
                handCount = myPanel.HandCount;
                return true;
            }
            return seats.TryGetHandCount(actorNumber, out handCount);
        }

        private void HandleCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount)
            => Enqueue(() =>
            {
                history.ShowCardNegated(cardInstanceId, cardId);
                LeaveChain();
                return null;
            });

        // 체인(또는 함정)의 카드들이 버림 더미에 쌓였다 — 쓴 순서대로 한 장씩 날아가, 마지막에 쓴 카드가 맨 위에 보인다 (04-card.md 9.1).
        private void HandleChainDiscarded(int[] cardInstanceIds, int[] cardIds, int discardCount)
        {
            _resolving = false;
            Enqueue(() =>
            {
                history.ShowChainEnded();
                return DevStageMeter.Wrap(DevStageMeter.After, ChainCardsToDiscardRoutine(cardInstanceIds, cardIds, discardCount));
            });
        }

        private IEnumerator ChainCardsToDiscardRoutine(int[] cardInstanceIds, int[] cardIds, int discardCount)
        {
            int before = discardCount - cardIds.Length;
            for (int i = 0; i < cardInstanceIds.Length; i++)
            {
                yield return chainCards.DiscardRoutine(cardInstanceIds[i]);
                discardPile.Show(cardIds[i], before + i + 1);
            }
        }

        // 버림 더미 맨 위 카드를 손패로 (1009 아 맞다!) — 내 것은 더미 자리에서 꺼내 손패에 붙이고, 남의 것은 장수만. 더미는 그 뒤의 맨 위 · 장수로.
        private void HandleDiscardTaken(int actorNumber, int cardInstanceId, int cardId, int handCount, int discardCount, int topCardId)
        {
            bool isMe = IsMe(actorNumber);
            if (choicePopup != null)
                choicePopup.NoteLeftDiscard(cardInstanceId); // 누가 가져가든 버림 더미를 떠났다

            Enqueue(() =>
            {
                if (isMe)
                    hand.TakeFromDiscard(cardInstanceId, cardId, discardPile.TopPosition);
                ShowHandCount(actorNumber, handCount);
                history.ShowDiscardTaken();
                if (TryGetFxSeat(actorNumber, out ResultFx.Seat seat))
                    resultFx.Gain(seat, 1);
                if (topCardId > 0)
                    discardPile.Show(topCardId, discardCount);
                else
                    discardPile.Clear();
                return null;
            });
        }

        // 턴 진행 방향 (1045 뒤로 돌아) — 턴 순서의 알약 · 화살표 · 꺾쇠.
        private void HandleTurnDirectionChanged(bool reversed, int extraTurn)
        {
            Enqueue(() =>
            {
                _reversed = reversed;
                turnOrder.ShowDirection(reversed);
                history.ShowDirectionChanged();
                return null;
            });
        }

        // 턴 스킵 (1020 얼음!) · 행동 카드 금지 (1007 짖기) · 기다려 (1023) — 상태 표시 시안이 없어 기록에만 (낸 카드 기록 줄의 결과 한 마디, 11-card-list 4절 #10)
        // 방장이 바뀌었다 — 기록에 한 줄만 (팝업 없음, 09-network.md 9.1)
        private void HandleMasterClientSwitched(Photon.Realtime.Player newMaster) => Enqueue(() => { history.ShowHostChanged(); return null; });

        // 다음 턴 스킵 — 걸린 동안 이름표 옆 「스킵」, 스킵된 턴이 지나가면 뗀다 (10-ui.md 3절 「턴 스킵 표시」)
        private void HandleTurnSkipped(int actorNumber) => Enqueue(() => { history.ShowTurnSkipped(actorNumber); ShowSkipMarked(actorNumber, false); return null; });

        private void HandleTurnSkipMarked(int actorNumber) => Enqueue(() => { history.ShowTurnSkipMarked(actorNumber); ShowSkipMarked(actorNumber, true); return null; });

        // 행동 카드 금지 (1007 짖기) — 다음 턴 · 이번 턴 동안 이름표 옆 「행동 금지」
        private void HandleActionBlockChanged(int actorNumber, ActionBlock state) => Enqueue(() =>
        {
            history.ShowActionBlock(actorNumber, state);
            ShowBlocked(actorNumber, state != ActionBlock.None);
            if (IsMe(actorNumber))
                _myActionBlocked = state == ActionBlock.ThisTurn;
            return null;
        });

        private void HandleTurnSkipCleared(int actorNumber) => Enqueue(() => { ShowSkipMarked(actorNumber, false); return null; });

        private void ShowSkipMarked(int actorNumber, bool marked)
        {
            if (IsMe(actorNumber))
                myPanel.ShowSkipMarked(marked);
            else
                seats.ShowSkipMarked(actorNumber, marked);
        }

        private void ShowBlocked(int actorNumber, bool blocked)
        {
            if (IsMe(actorNumber))
                myPanel.ShowBlocked(blocked);
            else
                seats.ShowBlocked(actorNumber, blocked);
        }

        private void HandleWaitChanged(int[] casters) => Enqueue(() => { history.ShowWaitChanged(casters); boardEffects.ShowWait(casters); return null; });

        private void HandleChapchuWinHeld(int actorNumber) => Enqueue(() => { history.ShowChapchuWinHeld(actorNumber); return null; });

        // 대소동 (1012) — 판 상태를 처음으로: 가운데 체인 카드 · 내 손패는 버림 더미로 날린 뒤 더미를 비우고, 함정 · 찹츄 · 방향 · 판 효과를 지운다.
        // 새 HP · 손패 · 첫 턴은 뒤따르는 알림이 시작 때처럼 채운다. 기록은 이어진다. 컷인 · 테이블 빛 연출은 넣지 않는다 (사용자 2026-11-13).
        // 새 판 — 체인은 처리 도중 버려져 ChainDiscarded 가 오지 않는다. 뒤따르는 HP 100 · 손패 배분은 4 단계가 아니다 (조용히)
        private void HandleGameRestarted(int user, int count, int[] order)
        {
            _resolving = false;
            choicePopup.ClearDiscarded();
            Enqueue(() => GameRestartedRoutine(user, count, order));
            _hpSource = HpSource.Quiet;
        }

        private IEnumerator GameRestartedRoutine(int user, int count, int[] order)
        {
            history.ShowGameRestarted(user, count, order);
            boardEffects.ShowRestarted();
            gaepan.ShowRestarted();
            chapchuShout.Hide();
            hand.EndReaction();
            reactionTimer.Hide();
            _chainCount = 0;
            _myActionBlocked = false; // 새 판은 상태가 없다 (11-card-list 1012)

            myTraps.Clear();
            myTraps.ShowCount(0);
            myPanel.ShowChapchu(false);
            myPanel.ShowHandCount(0);
            turnOrder.ShowDirection(false);
            _reversed = false;
            foreach (int actor in order)
            {
                ShowLife(actor, LifeState.Alive);
                turnOrder.ShowChapchu(actor, false);
                ShowSkipMarked(actor, false);
                ShowBlocked(actor, false);
                if (IsMe(actor))
                    continue;
                seats.ShowChapchu(actor, false);
                seats.ShowHandCount(actor, 0);
                seats.ShowTrapCount(actor, 0);
            }

            yield return chainCards.DiscardAllRoutine(); // 가운데에 남은 대소동 · 그 위 카운터 — 처리 도중 버려진 체인이라 ChainDiscarded 가 오지 않는다
            yield return hand.DiscardHandRoutine();
            discardPile.Clear();
        }

        // 피폭 (1012) — 턴이 끝나 피해가 들어가면(turns > 0) 화면 가장자리가 자주로 번지고, 뒤따르는 모두의 HP 변화는 자주 피해 (시안 fallout · zap)
        private void HandleFalloutChanged(int n, int turns)
        {
            Enqueue(() =>
            {
                history.ShowFallout(n, turns);
                boardEffects.ShowFallout(n, turns);
                if (turns > 0)
                    resultFx.FalloutGlow();
                return null;
            });
            _hpSource = turns > 0 ? HpSource.Fallout : HpSource.Normal;
        }

        // 대상이 고르는 중 (1019 · 2013 · 2021 의 대가) — 고르는 사람은 ChoicePopupPresenter 가 판을 바로 연다. 나머지는 같은 판을 누를 수 없게 — 고르는 사람의 손패 · 재생한 HP 로 (PM 2026-11-27)
        private void HandleChoiceStarted(int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline, int kind)
        {
            if (IsMe(actor))
                return;
            Enqueue(() =>
            {
                reactionTimer.Hide();
                choicePopup.ShowWatching(actor, user, cardId, handCount, _shownHp.TryGetValue(actor, out int hp) ? hp : GameServer.MaxHp, damage, startsAt, deadline, kind);
                return null;
            });
        }

        // 골랐다 — 남이 골랐으면 고른 카드가 튀어나온 뒤 기록 줄 (내 판은 ChoicePopupPresenter 가 바로 닫는다)
        private void HandleChoiceMade(int actor, int option, bool timedOut, int kind) => Enqueue(() => ChoiceMadeRoutine(actor, option, timedOut, kind));

        private IEnumerator ChoiceMadeRoutine(int actor, int option, bool timedOut, int kind)
        {
            reactionTimer.Hide();
            yield return choicePopup.PickRoutine(actor, option);
            history.ShowChoiceMade(actor, option, timedOut, kind);
        }

        // 개판 오분전 (1021, 프로토타입 v2) — 시작 제목 → 자명종, 바퀴마다 자명종, 판정 팻말. 판정 기록 줄은 팻말이 꼴찌를 보인 뒤에 (먼저 쓰면 알림 칩이 결과를 미리 알린다)
        private void HandleGaepanChanged(int caster, int left, bool again) =>
            Enqueue(() => { history.ShowGaepan(caster, left, again, left == Chapchu.Game.GameServer.GaepanRounds); return gaepan.ChangedRoutine(left); });

        private void HandleGaepanJudged(int loser, bool byHp, int value, int[] tied, int[] actors, int[] values)
        {
            Enqueue(() => GaepanJudgedRoutine(loser, byHp, value, tied, actors, values));
            _gaepanLoser = loser;
        }

        private IEnumerator GaepanJudgedRoutine(int loser, bool byHp, int value, int[] tied, int[] actors, int[] values)
        {
            yield return gaepan.JudgedRoutine(loser, byHp, actors, values);
            history.ShowGaepanJudged(loser, byHp, value, tied);
        }

        // 체인의 카드가 버림 더미 대신 시전자 손패로 (04-card.md 7절 「손패로 반환」) — 내 것은 손패에 다시 붙고, 남의 것은 그 좌석으로 돌아간다.
        private void HandleCardReturnedToHand(int actorNumber, int cardInstanceId, int cardId, int handCount)
        {
            bool isMe = IsMe(actorNumber);
            if (isMe && choicePopup != null)
                choicePopup.NoteLeftDiscard(cardInstanceId); // 예전에 버려졌던 내 카드가 남의 손을 거쳐 돌아온 경우 (2020)

            Enqueue(() =>
            {
                history.ShowCardLeftChain(cardId);
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
            if (isMe && choicePopup != null)
                choicePopup.NoteDiscarded(cardInstanceIds);

            Enqueue(() =>
            {
                history.ShowCardsDiscarded(cardIds.Length);
                if (TryGetFxSeat(actorNumber, out ResultFx.Seat seat))
                    resultFx.Lose(seat, cardIds.Length);
                return CardsToDiscardRoutine(actorNumber, isMe, handCount, cardIds, discardCount, isMe ? hand.DiscardFromHandRoutine(cardInstanceIds) : null);
            });
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
                history.ShowTrapDestroyed();
                return null;
            });
        }

        // 사망 대기 능력으로 살아났다 (06-health.md 8절) — HP 는 이 알림의 값.
        private void HandleDeathAvoided(int actorNumber, int hp)
        {
            Enqueue(() =>
            {
                ShowLife(actorNumber, LifeState.Alive);
                if (IsMe(actorNumber))
                    myPanel.ShowHp(hp);
                else
                    seats.ShowHp(actorNumber, hp);
                turnOrder.ShowHp(actorNumber, hp);
                return null;
            });
        }

        private void ShowLife(int actorNumber, LifeState life)
        {
            if (life == LifeState.Dead)
                _deadActors.Add(actorNumber);
            else
                _deadActors.Remove(actorNumber);
            if (IsMe(actorNumber))
                myPanel.ShowLife(life);
            else
                seats.ShowLife(actorNumber, life);
            turnOrder.ShowLife(actorNumber, life);
        }

        // 최종 사망 · 나감 — 손패(와 함정) 전부가 버림 더미로 (05-deck.md 6 · 7절). 죽은 사람은 좌석 · 내 HUD · 턴 순서에 「사망」.
        private void HandlePlayerDied(int actorNumber, int[] cardIds, int discardCount)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                ShowLife(actorNumber, LifeState.Dead);
                SoundManager.Play(Sfx.PlayerOut);

                // 함정도 전부 버림 더미로 (05-deck.md 6절)
                if (isMe)
                    myTraps.Clear();
                else
                    seats.ShowTrapCount(actorNumber, 0);

                return CardsToDiscardRoutine(actorNumber, isMe, 0, cardIds, discardCount, isMe ? hand.DiscardHandRoutine() : null);
            });
        }

        // 나간 사람의 좌석은 RoomEvents 가 지운다 — 여기서는 더미만.
        private void HandlePlayerLeftGame(int actorNumber, int[] cardIds, int discardCount, bool forced)
        {
            Enqueue(() =>
            {
                discardPile.Show(cardIds, discardCount);
                return null;
            });

            // 내가 강제 퇴장됐으면 큐를 기다리지 않고 바로 — 경고 예약 · 방 나가기 (곧 씬이 바뀐다)
            if (menu != null)
                menu.ShowPlayerLeftGame(actorNumber, forced);
        }

        // 판 중지 — 곧 방 씬으로 옮겨지므로 큐를 기다리지 않고 바로 경고를 예약한다 (09-network.md 9.1)
        private void HandleGameHalted()
        {
            if (menu != null)
                menu.ShowGameHalted();
        }

        // 찹츄 선언 · 해제(턴 시작 판정의 무효) — 내 HUD 이름표 또는 그 사람 좌석, 턴 순서 꼬리표, 기록.
        // 찹츄 상태가 되면(자동 선언 · 카드로 부여) 모든 화면에 선언 연출 — 큐를 세우지 않는다 (10-ui.md 5-2 #1 · 7절).
        private void HandleChapchuChanged(int actorNumber, bool isChapchu)
        {
            bool isMe = IsMe(actorNumber);

            Enqueue(() =>
            {
                if (isMe)
                    myPanel.ShowChapchu(isChapchu);
                else
                    seats.ShowChapchu(actorNumber, isChapchu);
                turnOrder.ShowChapchu(actorNumber, isChapchu);
                if (!isChapchu)
                    return null;

                history.ShowChapchuDeclared(actorNumber);
                return chapchuShout.PlayRoutine(isMe ? MyChapchuShout : string.Format(OtherChapchuShoutFormat, PlayerLook.NameOf(actorNumber)));
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

        // 4 단계 공통 연출 자리 — 나는 내 HUD, 남은 좌석. 좌석이 없으면(나간 사람) false.
        private bool TryGetFxSeat(int actorNumber, out ResultFx.Seat seat)
        {
            if (IsMe(actorNumber))
            {
                seat = myPanel.FxSeat;
                return true;
            }
            return seats.TryGetFxSeat(actorNumber, out seat);
        }

        private void Enqueue(Func<IEnumerator> step)
        {
            // 받은 기준의 「바로 앞 단계」 표시는 다른 단계를 줄 세우면 끝난다 — 이어 가야 하는 처리기는 줄 세운 뒤 다시 넣는다
            _hpSource = HpSource.Normal;
            _drawBatch = null;
            _steps.Enqueue(step);

            if (_playing == null)
                _playing = StartCoroutine(PlayRoutine());
        }

        // 앞에서부터 하나씩. 연출이 있는 단계는 끝날 때까지 다음을 꺼내지 않는다.
        // 한 단계가 예외를 내면 그 단계만 버리고 다음으로 — 아니면 코루틴이 죽어 그 뒤 알림이 화면에 영영 나오지 않는다
        private IEnumerator PlayRoutine()
        {
            while (_steps.Count > 0)
            {
                IEnumerator step = null;
                try
                {
                    step = _steps.Dequeue()();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                if (step != null)
                {
                    ShowHandPlayState(true);
                    yield return Guarded(step);
                }
            }

            _playing = null;
            ShowHandPlayState(false);
        }

        // 손패의 「지금 낼 수 있는 카드」 표시 (시안 v3.5) — 지금 낼 수 없는 카드는 모두 흐리다 (HandView.IsUnplayable, PM 2026-11-28).
        // 연출이 있는 단계를 재생하는 동안 · 체인이 남은 동안(반응 시간이 끝나 처리하는 사이 포함)은 진행 중이라 아무것도 낼 수 없다 — 반응 시간에 낼 수 있는 카운터만 예외.
        // 이번 턴에 카드를 이미 냈으면(메인 행동 끝) 턴이 넘어갈 때까지 진행 중이다 — 「뽑고 턴 끝」 버튼과 같은 조건 (03-turn.md 3절).
        // 큐가 빌 때와 연출 단계를 시작할 때만 고친다 — 단계 사이마다 고치면 연달아 오는 단계 사이에 카드가 잠깐 커졌다 줄어든다.
        private void ShowHandPlayState(bool stepRunning) =>
            hand.ShowPlayState(_isMyTurn, stepRunning || _chainCount > 0 || _myMainActionDone, myTraps.IsFull, _myActionBlocked);

        // 연출 코루틴을 직접 돌린다 — 안쪽 코루틴(yield return IEnumerator)까지 MoveNext 를 감싸, 예외가 나면 그 단계를 끝낸다
        private IEnumerator Guarded(IEnumerator step)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(step);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool moved;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    yield break;
                }

                if (!moved)
                {
                    stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator inner)
                    stack.Push(inner);
                else
                    yield return top.Current; // WaitForSeconds · Tween 대기 등은 Unity 가 기다린다
            }
        }

        private static bool IsMe(int actorNumber) => PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
    }
}
