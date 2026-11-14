using System;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 — 3D 층에서 카드를 원호 위에 부채꼴로 놓는다 (10-ui.md 2-1 「손패 · 카드」 · 4절, 프로토타입 v2 baseSlots · layoutCards). 순서는 드로우 순 — 정렬하지 않는다 (05-deck.md 6절).
    /// 자리는 화면 아래 끝을 기준으로 매번 계산한다. 내려간 손패는 내 패널과 찹츄 버튼 사이에 카드 위 절반만, 올라온 손패는 화면 가운데 기준으로 넓게 펼쳐 HUD 를 덮는다 (TopCardLayerView).
    /// 올라오는 때 = 포인터가 손패에 올라와 있거나(SetPointer) 끄는 중. 카드를 고르면(ToggleSelect) 손패는 내려가고 고른 카드만 올라와 양옆에 틈이 생긴다.
    /// 포인터를 직접 읽지 않는다 — HandInput 이 읽어 여기의 함수를 부른다. 어디에 놓았는지 · 사용 요청은 이벤트로만 알리고 판정은 프레젠터가 한다. 이 뷰는 서버를 모른다.
    /// 잠금(SetLocked)은 대상 선택 중 손패 입력 전부를 막는다 (10-ui.md 6절).
    /// </summary>
    public class HandView : MonoBehaviour
    {
        // 자리의 z 간격 — 오른쪽 카드가 카메라 쪽. 겹친 곳은 깊이로 정렬된다.
        private const float CardZStep = 0.02f;
        // 다른 카드보다 앞에 그릴 카드를 카메라 쪽으로 당기는 거리 — 손패 15장(0.3)까지 순서가 지켜진다. 가까워진 만큼 자리 · 크기를 줄여 화면에서는 같은 자리 · 크기로 보인다.
        private const float UsableLift = 0.3f;
        private const float SelectedLift = 0.34f;
        private const float HoverLift = 0.38f;
        private const float DragLift = 0.42f;
        private const float ShowLift = 1f; // 크게 보여 주는 카드 — 올라온 손패 · 끄는 카드보다 앞
        // 시안의 무대 px(960 × 540) 을 유닛으로 — 무대 1px = 캔버스 2px = 0.02 유닛.
        private const float UnitsPerStagePixel = 0.02f;
        private const float TiltReferenceFps = 60f; // 시안의 「매 프레임 0.86배」 기준 주사율

        [Header("종류별 카드 프리팹")]
        [SerializeField] private CardView actionCardPrefab;
        [SerializeField] private CardView counterCardPrefab;
        [SerializeField] private CardView trapCardPrefab;

        [Tooltip("올라온 손패 · 끄는 카드를 HUD 위에 그리는 층.")]
        [SerializeField] private TopCardLayerView topLayer;

        [Header("내려간 손패 — 내 패널과 찹츄 버튼 사이")]
        [Tooltip("화면 왼쪽 끝에서 손패가 시작되는 곳까지 (유닛) — 가장자리 32 + 내 패널 508 + 틈 24.")]
        [SerializeField] private float leftReserved = 5.64f;
        [Tooltip("화면 오른쪽 끝에서 손패가 끝나는 곳까지 (유닛) — 가장자리 32 + 찹츄 버튼 392 + 틈 28.")]
        [SerializeField] private float rightReserved = 4.52f;
        [Tooltip("카드 사이 간격의 상한 (카드 폭 대비).")]
        [SerializeField] private float loweredSpacing = 0.5f;
        [Tooltip("맨 끝 카드의 기울기 — 한 장 늘 때마다 이만큼(도), 상한까지.")]
        [SerializeField] private float loweredAnglePerCard = 1.4f;
        [SerializeField] private float loweredAngleMax = 9f;

        [Header("올라온 손패 — 화면 가운데 기준")]
        [Tooltip("카드 사이 간격의 상한 (카드 폭 대비).")]
        [SerializeField] private float raisedSpacing = 0.92f;
        [SerializeField] private float raisedAnglePerCard = 2.2f;
        [SerializeField] private float raisedAngleMax = 15f;
        [Tooltip("첫 카드와 끝 카드 가운데 사이 거리의 상한 (유닛) — 시안 750 × 2.")]
        [SerializeField] private float raisedMaxSpan = 15f;
        [Tooltip("화면 좌우 끝과 카드 사이 여백 (유닛) — 시안 가장자리 16 × 2 와 여유 12 × 2 의 반.")]
        [SerializeField] private float raisedEdgeMargin = 0.44f;
        [Tooltip("올라온 카드의 아래 가장자리가 화면 아래 끝에서 뜨는 거리 (유닛) — 시안 9 × 2.")]
        [SerializeField] private float raisedBottomGap = 0.18f;

        [Header("카드 상태 (카드 높이 · 폭 대비)")]
        [Tooltip("고른 카드 · 반응 시간에 낼 수 있는 카운터가 더 올라오는 높이 — 올라온 손패에서.")]
        [SerializeField] private float pickLiftRaised = 0.1f;
        [Tooltip("고른 카드 · 반응 시간에 낼 수 있는 카운터가 더 올라오는 높이 — 내려간 손패에서 (거의 다 보이게).")]
        [SerializeField] private float pickLiftLowered = 0.42f;
        [Tooltip("고른 카드 양옆 틈 — 내려간 손패에서 (카드 폭 대비).")]
        [SerializeField] private float pickGapLowered = 0.22f;
        [Tooltip("포인터 아래 카드가 더 올라오는 높이.")]
        [SerializeField] private float hoverLift = 0.08f;
        [Tooltip("포인터 아래 카드의 배율.")]
        [SerializeField] private float hoverScale = 1.12f;
        [Tooltip("반응 시간에 낼 수 있는 카운터의 배율.")]
        [SerializeField] private float usableScale = 1.06f;

        [Header("연출")]
        [Tooltip("카드가 자리를 옮기는 초 — 올리기 · 내리기 · 호버 · 드롭 실패 복귀.")]
        [SerializeField] private float moveDuration = 0.18f;
        [Tooltip("새 카드가 덱에서 자리로 날아가며 뒤집히는 초.")]
        [SerializeField] private float drawDuration = 1f;
        [Tooltip("자리에 놓인 카드의 X 기울기 (위쪽이 멀어지는 쪽이 +). 시안: 테이블 40° 위에 카드 −36° = 4°.")]
        [SerializeField] private float restTilt = 4f;
        [Tooltip("덱에서 나올 때 누워 있는 각도 — 테이블 기울기 40°. 뒷면으로 시작해 자리로 가며 뒤집힌다.")]
        [SerializeField] private float drawTilt = 40f;

        [Header("새 카드 보여 주기 (10-ui.md 2-1 「새 카드 보여 주기」 — 프로토타입 v2 revGeom × 2)")]
        [Tooltip("보여 주는 영역의 위 끝 — 화면 위 끝에서 (유닛). 턴 띠 아래.")]
        [SerializeField] private float showTopMargin = 1f;
        [Tooltip("보여 주는 영역의 아래 끝 — 올라온 손패(고른 카드 높이까지) 위로 띄우는 틈 (유닛).")]
        [SerializeField] private float showBottomGap = 0.28f;
        [Tooltip("카드 아래에 남은 시간 막대 · 안내 글을 둘 높이 (유닛).")]
        [SerializeField] private float showBarSpace = 0.8f;
        [Tooltip("카드 사이 가로 · 세로 틈 (유닛).")]
        [SerializeField] private float showGapX = 0.44f;
        [SerializeField] private float showGapY = 0.28f;
        [Tooltip("한 줄의 최대 폭 (유닛) 과 화면 좌우 끝에서 남기는 여백.")]
        [SerializeField] private float showMaxWidth = 13.6f;
        [SerializeField] private float showEdgeMargin = 0.8f;
        [Tooltip("배율 상한 — 한두 장일 때 너무 커지지 않게. 하한은 없다 (많으면 작아진다).")]
        [SerializeField] private float showMaxScale = 2.2f;
        [Tooltip("줄 수 상한 — 1 줄부터 이 줄 수까지 가운데 카드가 가장 커지는 줄 수를 고른다.")]
        [SerializeField] private int showMaxRows = 3;
        [Tooltip("보여 주는 자리로 가는 초 · 카드마다 늦추는 초.")]
        [SerializeField] private float showEnterDuration = 0.45f;
        [SerializeField] private float showStagger = 0.09f;
        [Tooltip("보여 준 뒤 손패 자리로 내려가는 초.")]
        [SerializeField] private float showReturnDuration = 0.4f;

        [Header("끌기")]
        [Tooltip("끄는 카드가 좌우로 기우는 최대 각도.")]
        [SerializeField] private float dragTiltMax = 18f;
        [Tooltip("움직인 거리(시안 무대 px)에 곱해 기울기에 더하는 값.")]
        [SerializeField] private float dragTiltPerPixel = 0.7f;
        [Tooltip("움직일 때마다 앞 기울기에서 남기는 비율.")]
        [SerializeField] private float dragTiltKeep = 0.6f;
        [Tooltip("프레임(1/60초)마다 기울기가 돌아오는 비율.")]
        [SerializeField] private float dragTiltDecay = 0.86f;
        [Tooltip("사용 기준선 — 올라온 카드의 위 가장자리에서 이만큼 위 (유닛). 끌어서 이 선 위에 놓으면 사용. 시안 24 × 2.")]
        [SerializeField] private float playLineGap = 0.48f;
        [Tooltip("손패 영역의 위쪽 여유 (유닛) — 포인터가 이 안에 있으면 카드 사이 틈에서도 손패가 올라온 채로. 시안 25 × 2.")]
        [SerializeField] private float handZoneGap = 0.5f;

        private readonly List<CardView> _cards = new List<CardView>();
        private readonly List<CardView> _laidOut = new List<CardView>();                        // 자리를 받은 카드 — _cards 에서 따로 둔 카드를 뺀 것
        private readonly List<Vector3> _slots = new List<Vector3>();                            // 계산용 — x · y · 시계 방향 각도
        private readonly Dictionary<CardView, Pose> _poses = new Dictionary<CardView, Pose>();  // 카드가 가고 있는 자리 — 포인터 판정도 이것으로 한다
        private readonly Dictionary<CardView, float> _arriveAt = new Dictionary<CardView, float>(); // 덱에서 날아오는 카드가 닿는 시각
        private readonly HashSet<int> _usable = new HashSet<int>();
        private Camera _camera;
        private CardView _hovered;
        private CardView _selected;
        private CardView _dragging;
        private CardView _held;        // 손패에서 잠깐 따로 둔 카드 (대상 고르는 중) — 자리 계산에서 뺀다
        private readonly HashSet<CardView> _shown = new HashSet<CardView>(); // 새로 들어와 크게 보여 주는 중인 카드 — 손패에는 있지만 자리 계산에서 뺀다
        private Vector2 _dragOffset;   // 잡은 지점을 유지하려고 — 카드 가운데 − 포인터
        private Vector2 _dragCenter;
        private float _dragTilt;
        private float _lastDragX;
        private bool _armed;
        private bool _revealed;        // 포인터가 손패에 올라와 있다
        private bool _reacting;
        private bool _wasRaised;
        private bool _onTop;           // 카드가 HUD 위 층에 있다 — 내려갈 때는 자리로 돌아간 뒤에 내린다
        private float _dropLayerAt = -1f;

        public int Count => _cards.Count;

        /// <summary>손패가 올라와 있는가 — 끄는 중이거나, 고른 카드 없이 포인터가 손패에 있다.</summary>
        public bool IsRaised => _dragging != null || (_selected == null && _revealed);

        /// <summary>카드를 끄는 중인가.</summary>
        public bool IsDragging => _dragging != null;

        /// <summary>잠김 — 손패 입력을 모두 무시한다 (대상 선택 중, 10-ui.md 6절).</summary>
        public bool IsLocked { get; private set; }

        /// <summary>끌던 카드를 놓았다 (카드, 화면 좌표). 받는 쪽이 ReturnCard · Hold 로 카드의 자리를 정한다. 아무도 안 받으면 바로 돌아간다.</summary>
        public event Action<CardView, Vector2> OnCardDropped;

        /// <summary>카드를 끄는 중이다 (카드, 화면 좌표) — 놓으면 사용되는지 표시하라고.</summary>
        public event Action<CardView, Vector2> OnCardDragged;

        /// <summary>끌지 않고 카드 사용을 청했다 (더블클릭 · 고른 카드 다시 탭).</summary>
        public event Action<CardView> OnCardUseRequested;

        /// <summary>손패가 올라오거나 내려갔다.</summary>
        public event Action<bool> OnRaisedChanged;

        /// <summary>끌기가 시작되거나 끝났다.</summary>
        public event Action<bool> OnDraggingChanged;

        private float CardWidth => actionCardPrefab.Width;
        private float CardHeight => actionCardPrefab.Height;
        // 손패 평면에서 본 화면 — 세로 화각이 고정이라 아래 끝은 늘 같고, 좌우는 화면 비에 따라 넓어진다 (15-screen Expand)
        private float PlaneDistance => Mathf.Abs(transform.position.z - _camera.transform.position.z);
        private float ScreenHalfHeight => PlaneDistance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        private float ScreenHalfWidth => ScreenHalfHeight * _camera.aspect;
        private float ScreenBottom => _camera.transform.position.y - ScreenHalfHeight;

        private void Awake()
        {
            _camera = Camera.main;

            if (actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null || topLayer == null)
                Debug.LogError($"[{nameof(HandView)}] 종류별 카드 프리팹 · {nameof(topLayer)} 참조 중 비어 있는 것이 있습니다.", this);
            if (_camera == null)
                Debug.LogError($"[{nameof(HandView)}] MainCamera 태그의 카메라가 없습니다 — 자리 · 포인터 좌표를 못 푼다.", this);
        }

        private void Update()
        {
            if (_dragging != null)
            {
                _dragTilt *= Mathf.Pow(dragTiltDecay, Time.deltaTime * TiltReferenceFps);
                PlaceDragged();
            }

            if (_dropLayerAt >= 0f && Time.time >= _dropLayerAt)
            {
                _dropLayerAt = -1f;
                _onTop = false;
                SyncLayers();
            }
        }

        /// <summary>
        /// 카드 종류에 맞는 프리팹을 spawnWorldPosition(덱 자리)에 뒷면 · 누운 채로 만들어 손패 끝에 붙이고 다시 놓는다.
        /// show 면 자리로 보내지 않고 따로 둔다 — 부른 쪽이 LayoutShown 으로 크게 보여 준 뒤 ReturnCard 로 자리에 내린다.
        /// </summary>
        public CardView AddCard(int instanceId, CardData data, Vector3 spawnWorldPosition, bool show = false)
        {
            CardView card = Instantiate(PrefabOf(data.type), transform);
            card.transform.position = spawnWorldPosition;
            // 덱 위에 뒷면으로 누운 자세 — 먼저 Y 180° 로 뒤집고 X 로 눕혀야 위쪽이 멀어진다(순서가 바뀌면 카메라 쪽으로 기운다). 자리로 가며 뒤집혀 선다.
            card.transform.localRotation = Quaternion.Euler(drawTilt, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
            card.Setup(instanceId, data);
            card.SetRaycastBlocking(!IsLocked);
            if (show)
                _shown.Add(card);
            else
                _arriveAt[card] = Time.time + drawDuration;
            _cards.Add(card);
            Layout();
            return card;
        }

        /// <summary>크게 보여 주는 중인 카드인가 (AddCard show) — 손패에서 빠졌거나 자리로 내려갔으면 false.</summary>
        public bool IsShown(CardView card) => card != null && _shown.Contains(card);

        /// <summary>
        /// 보여 주는 카드들을 화면 가운데 위쪽에 나란히 놓는다 — 장수에 맞춰 줄 수 · 크기를 정한다 (턴 띠 아래 ~ 올라온 손패 위, 카드가 가장 커지는 줄 수).
        /// firstNew 번째부터는 새로 온 카드라 차례로 늦춰 보낸다. 돌려주는 값은 카드 묶음의 아래 끝 (월드 y) — 그 아래에 막대 · 안내 글을 둔다.
        /// </summary>
        public float LayoutShown(IReadOnlyList<CardView> cards, int firstNew)
        {
            int count = cards.Count;
            float cardWidth = CardWidth, cardHeight = CardHeight;
            Vector3 cameraPosition = _camera.transform.position;
            float top = cameraPosition.y + ScreenHalfHeight - showTopMargin;
            float handTop = ScreenBottom + raisedBottomGap + cardHeight * (1f + pickLiftRaised) + showBottomGap;
            float available = top - handTop - showBarSpace;
            float maxWidth = Mathf.Min(showMaxWidth, ScreenHalfWidth * 2f - showEdgeMargin * 2f);

            int rows = 1, perRow = count;
            float scale = 0f;
            for (int r = 1; r <= Mathf.Min(showMaxRows, count); r++)
            {
                int per = Mathf.CeilToInt(count / (float)r);
                float fit = Mathf.Min((available - showGapY * (r - 1)) / (r * cardHeight), (maxWidth - showGapX * (per - 1)) / (per * cardWidth), showMaxScale);
                if (fit <= scale + 0.01f) continue; // 같은 크기면 줄이 적은 쪽

                scale = fit;
                rows = r;
                perRow = per;
            }

            float blockHeight = rows * cardHeight * scale + (rows - 1) * showGapY;
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow, column = i - row * perRow;
                int inRow = Mathf.Min(perRow, count - row * perRow);
                float rowWidth = inRow * cardWidth * scale + (inRow - 1) * showGapX;
                var center = new Vector2(
                    cameraPosition.x - rowWidth / 2f + cardWidth * scale / 2f + column * (cardWidth * scale + showGapX),
                    top - cardHeight * scale / 2f - row * (cardHeight * scale + showGapY));

                var pose = new Pose { Center = center, Angle = 0f, Scale = scale, Lift = ShowLift + i * CardZStep, TiltX = 0f };
                ToTransform(pose, out Vector3 position, out Quaternion rotation, out float size);
                float delay = i >= firstNew ? (i - firstNew) * showStagger : 0f;
                Transform t = cards[i].transform;
                t.DOKill();
                t.DOLocalMove(position, showEnterDuration).SetDelay(delay).SetEase(Ease.OutQuart).SetLink(cards[i].gameObject);
                t.DOLocalRotateQuaternion(rotation, showEnterDuration).SetDelay(delay).SetEase(Ease.OutQuart).SetLink(cards[i].gameObject);
                t.DOScale(size, showEnterDuration).SetDelay(delay).SetEase(Ease.OutQuart).SetLink(cards[i].gameObject);
            }

            return top - blockHeight;
        }

        /// <summary>손패 평면 위 월드 y → 캔버스 좌표 y (화면 가운데가 0, 위가 +) — 캔버스 100px = 1 유닛 (10-ui.md 2-1 「3D 층 구현」).</summary>
        public float WorldToCanvasY(float worldY) => (worldY - _camera.transform.position.y) * 100f;

        /// <summary>보여 주는 자리로 가는 데 걸리는 초 — 새 카드 newCount 장 (마지막 카드의 늦춤 포함).</summary>
        public float ShowEnterSeconds(int newCount) => showEnterDuration + showStagger * Mathf.Max(0, newCount - 1);

        /// <summary>TakeCard 로 떼어 갔던 카드를 손패 오른쪽 끝에 다시 붙이고 자리로 보낸다 — 체인에서 손패로 돌아온 카드 (04-card.md 7절 「손패로 반환」, 05-deck.md 6절).</summary>
        public void PutBack(CardView card)
        {
            card.transform.SetParent(transform, true);
            card.SetRaycastBlocking(!IsLocked);
            _cards.Add(card);
            Layout();
        }

        /// <summary>
        /// 반응 시간 — 이 인스턴스 ID 의 카드(낼 수 있는 카운터)는 빛나며 올라오고 나머지는 어두워진다 (10-ui.md 5절). null 이면 반응 시간이 아니다.
        /// </summary>
        public void ShowReaction(ICollection<int> usableInstanceIds)
        {
            _reacting = usableInstanceIds != null;
            _usable.Clear();
            if (usableInstanceIds != null)
                _usable.UnionWith(usableInstanceIds);
            Layout();
        }

        /// <summary>해당 장을 손패에서 떼어 돌려준다 — 받는 쪽이 연출하고 지운다. 나머지는 다시 놓는다. 없으면 null.</summary>
        public CardView TakeCard(int instanceId)
        {
            int index = _cards.FindIndex(c => c.InstanceId == instanceId);
            if (index < 0) return null;

            CardView card = _cards[index];
            _cards.RemoveAt(index);
            Detach(card);

            Layout();
            return card;
        }

        /// <summary>손패 전부를 떼어 돌려준다 — 받는 쪽이 연출하고 지운다 (최종 사망, 05-deck.md 6절).</summary>
        public List<CardView> TakeAll()
        {
            var taken = new List<CardView>(_cards);
            _cards.Clear();
            foreach (CardView card in taken)
                Detach(card);
            Layout();
            return taken;
        }

        /// <summary>고른 카드 · 호버를 풀고 손패를 내린다 (빈 곳 클릭 · 우클릭 · 사용 요청 뒤).</summary>
        public void Lower()
        {
            if (_selected == null && !_revealed && _hovered == null) return;

            _selected = null;
            _revealed = false;
            _hovered = null;
            Layout();
        }

        /// <summary>
        /// 잠그면 끌기 · 고르기 · 호버를 풀고 손패를 내린 뒤 입력을 무시한다. 카드는 포인터를 가리지 않는다 — 좌석 탭이 통하게. 풀면 다시 받는다.
        /// </summary>
        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            if (locked)
            {
                StopDragging();
                _selected = null;
                _hovered = null;
                _revealed = false;
            }

            foreach (CardView card in _cards)
                card.SetRaycastBlocking(!locked);
            Layout();
        }

        /// <summary>
        /// 손패의 카드를 잠깐 따로 둔다 — 자리 계산에서 빠지고 나머지가 그 자리를 메운다. 카드를 어디에 둘지 · 어느 층에 그릴지는 부른 쪽이 정한다 (대상 고르는 중의 카드, 10-ui.md 6절).
        /// ReturnCard 로 되돌린다. 손패에 없으면 무시.
        /// </summary>
        public void Hold(CardView card)
        {
            if (!_cards.Contains(card)) return;

            if (_dragging == card)
                StopDragging();
            if (_selected == card)
                _selected = null;
            if (_hovered == card)
                _hovered = null;
            _held = card;
            _poses.Remove(card);
            ClearMarks(card);
            Layout();
        }

        /// <summary>끌던 카드 · 따로 둔 카드를 손패의 자기 자리로 돌린다. 손패에 없으면 무시.</summary>
        public void ReturnCard(CardView card)
        {
            if (!_cards.Contains(card)) return;

            if (_dragging == card)
                StopDragging();
            if (_held == card)
                _held = null;
            if (_shown.Remove(card))
            {
                // 크게 보여 주던 카드 — 위에서 내려오는 동안은 HUD 위 층에 둔다 (가는 길에 내 패널 뒤로 꺼지지 않게)
                _arriveAt[card] = Time.time + showReturnDuration;
                _onTop = true;
                if (!IsRaised)
                    _dropLayerAt = Mathf.Max(_dropLayerAt, Time.time + showReturnDuration);
            }

            Layout();
        }

        /// <summary>버튼을 누르지 않은 포인터가 여기 있다 — 카드 위면 손패를 올리고 그 카드를 더 올린다. 손패 영역을 벗어나면 내린다.</summary>
        public void SetPointer(Vector2 screenPosition)
        {
            if (IsLocked || _dragging != null) return;

            CardView card = CardAt(screenPosition);
            bool revealed = card != null || (_revealed && IsInHandZone(screenPosition));
            if (card == _hovered && revealed == _revealed) return;

            _hovered = card;
            _revealed = revealed;
            Layout();
        }

        /// <summary>포인터가 손패를 볼 수 없는 곳에 있다 (HUD 위 · 창 밖) — 호버를 푼다. lower 면 손패도 내린다 (고른 카드는 그대로).</summary>
        public void ClearPointer(bool lower)
        {
            if (_dragging != null) return;
            if (_hovered == null && (!lower || !_revealed)) return;

            _hovered = null;
            if (lower)
                _revealed = false;
            Layout();
        }

        /// <summary>카드를 고르거나, 이미 고른 카드면 푼다. 손패에 없으면 무시.</summary>
        public void ToggleSelect(CardView card)
        {
            if (IsLocked || !_cards.Contains(card)) return;

            _selected = _selected == card ? null : card;
            Layout();
        }

        /// <summary>끌기 시작 — pressScreenPosition(눌렀던 곳)에서 잡은 지점을 유지한 채 따라온다. 손패는 올라온 채.</summary>
        public void BeginDrag(CardView card, Vector2 pressScreenPosition)
        {
            if (IsLocked || !_cards.Contains(card) || card == _held || _shown.Contains(card)) return;

            Vector2 pointer = ToPlane(pressScreenPosition);
            Vector2 center = _poses.TryGetValue(card, out Pose pose) ? pose.Center : pointer;
            _dragging = card;
            _dragOffset = center - pointer;
            _dragCenter = center;
            _dragTilt = 0f;
            _lastDragX = pointer.x;
            _armed = false;
            _hovered = null;
            _revealed = true;
            _poses.Remove(card);
            card.transform.DOKill();

            Layout();
            OnDraggingChanged?.Invoke(true);
        }

        /// <summary>끄는 중 — 움직인 만큼 좌우로 기운다 (10-ui.md 4절 「끌기」).</summary>
        public void Drag(Vector2 screenPosition)
        {
            if (_dragging == null) return;

            Vector2 pointer = ToPlane(screenPosition);
            float moved = (pointer.x - _lastDragX) / UnitsPerStagePixel;
            _lastDragX = pointer.x;
            if (!Mathf.Approximately(moved, 0f))
                _dragTilt = Mathf.Clamp(_dragTilt * dragTiltKeep + moved * dragTiltPerPixel, -dragTiltMax, dragTiltMax);
            _dragCenter = pointer + _dragOffset;
            PlaceDragged();

            OnCardDragged?.Invoke(_dragging, screenPosition);
        }

        /// <summary>끄는 카드에 「놓으면 사용」 링을 켜고 끈다 — 기준선 · 상대 위 판정은 프레젠터가 한다.</summary>
        public void SetArmed(bool armed)
        {
            if (_dragging == null || _armed == armed) return;

            _armed = armed;
            _dragging.SetSelected(armed || _dragging == _selected);
        }

        /// <summary>끌던 카드를 놓았다 — OnCardDropped 로 알린다. 받는 곳이 없으면 자리로.</summary>
        public void EndDrag(Vector2 screenPosition)
        {
            CardView card = _dragging;
            if (card == null) return;

            if (OnCardDropped == null)
            {
                ReturnCard(card);
                return;
            }

            OnCardDropped.Invoke(card, screenPosition);
            // 받는 쪽이 자리를 정하지 않았으면(ReturnCard · Hold 를 부르지 않음) 여기서 돌린다
            if (_dragging == card)
                ReturnCard(card);
        }

        /// <summary>끌기를 그만두고 카드를 자리로 돌린다 (우클릭 · 창 전환).</summary>
        public void CancelDrag()
        {
            if (_dragging != null)
                ReturnCard(_dragging);
        }

        /// <summary>끌지 않고 사용을 청한다 (더블클릭 · 고른 카드 다시 탭) — OnCardUseRequested 로 알린다.</summary>
        public void RaiseCardUseRequested(CardView card)
        {
            if (IsLocked || !_cards.Contains(card)) return;

            OnCardUseRequested?.Invoke(card);
        }

        /// <summary>이 화면 좌표에 있는 손패 카드 — 겹치면 앞에 그려진 것. 카드가 가고 있는 자리로 판정한다 (움직이는 중에도 흔들리지 않게). 없으면 null.</summary>
        public CardView CardAt(Vector2 screenPosition)
        {
            Vector2 point = ToPlane(screenPosition);
            CardView best = null;
            float bestLift = float.MinValue;
            foreach (CardView card in _laidOut)
            {
                if (card == _dragging || !_poses.TryGetValue(card, out Pose pose)) continue;

                Vector2 local = Quaternion.Euler(0f, 0f, -pose.Angle) * (point - pose.Center);
                if (Mathf.Abs(local.x) > CardWidth * pose.Scale / 2f || Mathf.Abs(local.y) > CardHeight * pose.Scale / 2f) continue;
                if (pose.Lift <= bestLift) continue;

                bestLift = pose.Lift;
                best = card;
            }
            return best;
        }

        /// <summary>이 화면 좌표가 사용 기준선 위인가 — 끌어서 여기에 놓으면 사용 (10-ui.md 4절 「끌다가 놓기」).</summary>
        public bool IsAbovePlayLine(Vector2 screenPosition) => ToPlane(screenPosition).y > ScreenBottom + CardHeight + playLineGap;

        /// <summary>
        /// 자리를 다시 계산해 카드를 보낸다. 끄는 카드 · 따로 둔 카드는 건드리지 않는다 — 끄는 중 · 대상 선택 중에 카드가 들어와도 들고 있던 카드가 튀지 않게.
        /// </summary>
        public void Layout()
        {
            _laidOut.Clear();
            foreach (CardView card in _cards)
            {
                if (card != _held && !_shown.Contains(card))
                    _laidOut.Add(card);
            }

            bool raised = IsRaised;
            int selectedIndex = _selected != null ? _laidOut.IndexOf(_selected) : -1;
            ComputeSlots(_laidOut.Count, raised, selectedIndex, _slots);

            float cardHeight = CardHeight;
            for (int i = 0; i < _laidOut.Count; i++)
            {
                CardView card = _laidOut[i];
                bool usable = _reacting && _usable.Contains(card.InstanceId);
                bool dragging = card == _dragging;
                card.SetGlow(usable);
                card.SetDimmed(_reacting && !usable && !dragging);
                card.SetSelected(card == _selected || (dragging && _armed));
                if (dragging) continue;

                Vector3 slot = _slots[i];
                var pose = new Pose { Center = new Vector2(slot.x, slot.y), Angle = -slot.z, Scale = 1f, Lift = i * CardZStep, TiltX = restTilt };
                if (raised)
                    pose.Center.y += cardHeight / 2f + raisedBottomGap;

                float pickLift = cardHeight * (raised ? pickLiftRaised : pickLiftLowered);
                if (card == _selected)
                {
                    pose.Center.y += pickLift;
                    pose.Lift += SelectedLift;
                }

                if (card == _hovered && card != _selected)
                {
                    pose.Center.y += cardHeight * hoverLift;
                    pose.Scale *= hoverScale;
                    pose.Lift += HoverLift;
                }
                else if (usable && card != _selected)
                {
                    // 낼 수 있는 카운터는 내려간 손패에서도 쑥 올라와 다 보인다
                    pose.Center.y += pickLift;
                    pose.Scale *= usableScale;
                    if (!raised)
                        pose.Lift += UsableLift;
                }

                MoveTo(card, pose);
            }

            SyncRaised(raised);
        }

        // 원호 위의 자리 n 개 — x · y(카드 가운데, 화면 아래 끝이 기준) · 시계 방향 각도. 프로토타입 v2 baseSlots · handSlots 와 같은 식.
        // 화면 아래 가상의 중심점을 기준으로 놓아 기울기와 높이가 늘 맞물린다. selected 번째 카드를 골랐으면 그 양옆에 틈을 벌리고, 넘치면 나머지 간격을 좁힌다.
        private void ComputeSlots(int count, bool raised, int selected, List<Vector3> result)
        {
            result.Clear();
            if (count == 0) return;

            float cardWidth = CardWidth;
            float halfWidth = ScreenHalfWidth;
            float cameraX = _camera.transform.position.x;
            float left = cameraX - halfWidth + leftReserved;
            float right = cameraX + halfWidth - rightReserved;

            float centerX = raised ? cameraX : (left + right) / 2f;
            float maxSpan = raised
                ? Mathf.Min(raisedMaxSpan, halfWidth * 2f - raisedEdgeMargin * 2f - cardWidth)
                : Mathf.Max(cardWidth * 0.5f, right - left - cardWidth);
            float spacing = Mathf.Min(cardWidth * (raised ? raisedSpacing : loweredSpacing), maxSpan / Mathf.Max(count - 1, 1));
            float halfArc = spacing * (count - 1) / 2f;
            float edgeAngle = (raised ? Mathf.Min(raisedAngleMax, raisedAnglePerCard * (count - 1)) : Mathf.Min(loweredAngleMax, loweredAnglePerCard * (count - 1))) * Mathf.Deg2Rad;

            var along = new float[count]; // 원호를 따라 잰 가운데에서의 거리
            for (int i = 0; i < count; i++)
                along[i] = (i - (count - 1) / 2f) * spacing;

            if (selected >= 0 && count > 1)
            {
                // 고른 카드가 앞으로 나와 이웃을 가리지 않게 — 겹침이 클수록 더 벌린다. 고른 카드는 제자리에 둔다
                float gap = raised ? Mathf.Max(0f, cardWidth - spacing) * 0.75f + cardWidth * 0.06f : cardWidth * pickGapLowered;
                float gapLeft = selected > 0 ? gap : 0f;
                float gapRight = selected < count - 1 ? gap : 0f;
                float limit = Mathf.Max(halfArc, maxSpan / 2f);
                float step = spacing;
                if ((count - 1) * step + gapLeft + gapRight > limit * 2f)
                    step = Mathf.Max(spacing * 0.4f, (limit * 2f - gapLeft - gapRight) / (count - 1));

                var spread = new float[count];
                for (int i = 1; i < count; i++)
                    spread[i] = spread[i - 1] + step + (i == selected ? gapLeft : 0f) + (i == selected + 1 ? gapRight : 0f);
                float offset = Mathf.Min(Mathf.Max(along[selected] - spread[selected], -limit), limit - spread[count - 1]);
                for (int i = 0; i < count; i++)
                    along[i] = spread[i] + offset;
            }

            float bottom = ScreenBottom;
            bool curved = count > 1 && edgeAngle > 0f && halfArc > 0f;
            float radius = curved ? halfArc / edgeAngle : 0f;
            for (int i = 0; i < count; i++)
            {
                if (!curved)
                {
                    result.Add(new Vector3(centerX + along[i], bottom, 0f));
                    continue;
                }

                float theta = along[i] / radius;
                result.Add(new Vector3(centerX + radius * Mathf.Sin(theta), bottom - radius * (1f - Mathf.Cos(theta)), theta * Mathf.Rad2Deg));
            }
        }

        // 카드를 자리로 보낸다. 같은 자리로 이미 가고 있으면 다시 시작하지 않는다. 덱에서 날아오는 카드는 닿을 때까지의 시간으로.
        private void MoveTo(CardView card, Pose pose)
        {
            if (_poses.TryGetValue(card, out Pose current) && current.Equals(pose)) return;

            _poses[card] = pose;
            float duration = moveDuration;
            if (_arriveAt.TryGetValue(card, out float arriveAt))
            {
                if (arriveAt - Time.time > moveDuration)
                    duration = arriveAt - Time.time;
                else
                    _arriveAt.Remove(card);
            }

            ToTransform(pose, out Vector3 position, out Quaternion rotation, out float scale);
            Transform t = card.transform;
            t.DOKill();
            t.DOLocalMove(position, duration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DOLocalRotateQuaternion(rotation, duration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            t.DOScale(scale, duration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
        }

        // 끄는 카드는 트윈 없이 바로 — 포인터를 따라 똑바로 선 채 좌우로만 기운다.
        private void PlaceDragged()
        {
            var pose = new Pose { Center = _dragCenter, Angle = -_dragTilt, Scale = 1f, Lift = DragLift, TiltX = 0f };
            ToTransform(pose, out Vector3 position, out Quaternion rotation, out float scale);
            Transform t = _dragging.transform;
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = Vector3.one * scale;
        }

        // 자리(손패 평면 위 카드 가운데 · 각도 · 배율) → 카드 루트(기준점 아래 가운데)의 로컬 위치 · 회전 · 배율.
        // Lift 만큼 카메라 쪽으로 당기되, 가까워진 비율만큼 자리 · 배율을 줄여 화면에서는 평면 위에 있는 것과 같게 보인다.
        private void ToTransform(Pose pose, out Vector3 localPosition, out Quaternion localRotation, out float scale)
        {
            Vector3 cameraPosition = _camera.transform.position;
            float distance = PlaneDistance;
            float near = (distance - pose.Lift) / distance;
            var axis = new Vector2(cameraPosition.x, cameraPosition.y);
            Vector2 center = axis + (pose.Center - axis) * near;

            scale = pose.Scale * near;
            localRotation = Quaternion.Euler(pose.TiltX, 0f, pose.Angle);
            Vector3 world = new Vector3(center.x, center.y, transform.position.z - pose.Lift) - localRotation * new Vector3(0f, CardHeight * scale / 2f, 0f);
            localPosition = transform.InverseTransformPoint(world);
        }

        // 올라옴 · 내려감이 바뀌었으면 알리고, 카드를 그릴 층을 맞춘다. 내려갈 때는 카드가 자리로 돌아간 뒤에 HUD 아래 층으로 — 가는 길에 내 패널 뒤로 꺼지지 않게.
        private void SyncRaised(bool raised)
        {
            if (raised)
            {
                _onTop = true;
                _dropLayerAt = -1f;
            }
            else if (_onTop && _dropLayerAt < 0f)
            {
                _dropLayerAt = Time.time + moveDuration;
            }

            SyncLayers();

            if (raised == _wasRaised) return;

            _wasRaised = raised;
            OnRaisedChanged?.Invoke(raised);
        }

        private void SyncLayers()
        {
            foreach (CardView card in _cards)
            {
                if (card != _held && !_shown.Contains(card))
                    topLayer.Set(card, _onTop);
            }
        }

        private void StopDragging()
        {
            if (_dragging == null) return;

            _dragging = null;
            _armed = false;
            OnDraggingChanged?.Invoke(false);
        }

        // 손패에서 뺀 카드의 상태 · 표시 · 트윈을 끊는다. 목록에서 빼기는 부르는 쪽이.
        private void Detach(CardView card)
        {
            if (_dragging == card)
                StopDragging();
            if (_hovered == card)
                _hovered = null;
            if (_selected == card)
                _selected = null;
            if (_held == card)
                _held = null;
            _shown.Remove(card);
            _poses.Remove(card);
            _arriveAt.Remove(card);
            ClearMarks(card);
            topLayer.Set(card, false);
            card.transform.DOKill();
        }

        private static void ClearMarks(CardView card)
        {
            card.SetGlow(false);
            card.SetSelected(false);
            card.SetDimmed(false);
        }

        // 포인터가 손패 영역(올라온 손패가 차지하는 띠) 안인가 — 카드 사이 틈에 있어도 손패가 내려가지 않게.
        private bool IsInHandZone(Vector2 screenPosition)
        {
            ComputeSlots(_laidOut.Count, true, -1, _slots);
            if (_slots.Count == 0) return false;

            Vector2 point = ToPlane(screenPosition);
            float reach = CardWidth * 0.75f;
            return point.y < ScreenBottom + CardHeight + handZoneGap && point.x > _slots[0].x - reach && point.x < _slots[_slots.Count - 1].x + reach;
        }

        // 화면 좌표 → 손패 평면 위의 월드 좌표 (x · y). 카메라에서 쏜 광선을 손패 평면과 만나게 한다.
        private Vector2 ToPlane(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(-transform.forward, transform.position);
            if (!plane.Raycast(ray, out float distance))
                return Vector2.zero;

            Vector3 point = ray.GetPoint(distance);
            return new Vector2(point.x, point.y);
        }

        private CardView PrefabOf(CardType type)
        {
            switch (type)
            {
                case CardType.Counter: return counterCardPrefab;
                case CardType.Trap:    return trapCardPrefab;
                default:               return actionCardPrefab;
            }
        }

        // 카드 한 장의 자리 — 손패 평면 위 카드 가운데(월드 x · y), 반시계 각도, 배율, 카메라 쪽으로 당긴 거리, X 기울기.
        private struct Pose : IEquatable<Pose>
        {
            public Vector2 Center;
            public float Angle;
            public float Scale;
            public float Lift;
            public float TiltX;

            public bool Equals(Pose other) =>
                (Center - other.Center).sqrMagnitude < 1e-8f && Mathf.Approximately(Angle, other.Angle) && Mathf.Approximately(Scale, other.Scale)
                && Mathf.Approximately(Lift, other.Lift) && Mathf.Approximately(TiltX, other.TiltX);
        }
    }
}
