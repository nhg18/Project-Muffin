using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Game.Effects;
using DG.Tweening;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 체인에 올라간 카드를 모든 화면에 보여 준다 (10-ui.md 5절 「현재 처리 카드」 · 5-1 단계 3 · 2-1 「카드 연출 자리」, 프로토타입 v2 FXV.show → settle).
    /// 내 카드는 손패에서 넘겨받고, 남의 카드는 그 좌석의 손 자리(발동한 함정은 그 칸)에서 앞면으로 만들어 바로 가운데 큰 자리로 보낸다 — 테이블에 눕히는 1 단계는 없다 (사용자 결정 2026-11-11).
    /// 3 단계: 가운데로 세워 크게 — 3D 층(테이블 · 손패)만 어둡게 하고 카드 아래에 주인 태그. 끝나면 체인의 제자리에 둔다 (반응 시간 · 체인 처리 동안) — 혼자면 그 크기 그대로.
    /// 처리 결과가 오면 버림 더미로 보내 지운다 — 「손패로 반환」 이면 주인에게 돌려준다. 2 · 4 · 5 단계 · 무효 도장은 아직 없다.
    /// 카운터가 끼어들면 앞 카드 오른쪽에 붙는다 — 몇 장이든 이어지고, 무효될 카드의 묶음은 옆으로 비킨다 (ComputeLayout, 프로토타입 v2 chainLayout).
    /// 언제 무엇을 하는지는 GameScreenPresenter 가 연출 큐 순서로 부른다. 판정은 하지 않는다.
    /// </summary>
    public class ChainCardsPresenter : MonoBehaviour
    {
        private const string MeName = "나";
        private const string OwnerOnlyFormat = "{0}";
        private const string OwnerToTargetFormat = "{0} → {1}";
        private const string TargetSeparator = " · ";
        private const string NameFormat = "<color=#{0}><noparse>{1}</noparse></color>";

        [Tooltip("PunGameServer 와 같은 덱 에셋 — 카드 종류 ID 로 카드 내용을 찾는다 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;

        [Header("종류별 카드 프리팹 (HandView 와 같은 것) — 남이 낸 카드를 만든다")]
        [SerializeField] private CardView actionCardPrefab;
        [SerializeField] private CardView counterCardPrefab;
        [SerializeField] private CardView trapCardPrefab;

        [Tooltip("남이 낸 카드가 출발하는 좌석의 손 자리를 묻는다.")]
        [SerializeField] private OpponentSeatsPresenter seats;
        [Tooltip("3 단계 동안 카드 아래에 뜨는 주인 태그.")]
        [SerializeField] private CardOwnerTagView ownerTag;
        [Tooltip("3 단계 동안 3D 층(테이블 · 덱 · 버림 더미 · 내 손패)을 어둡게 덮는 판 — 손패보다 앞, 크게 둔 카드보다 뒤에 있다. HUD 는 덮지 않는다.")]
        [SerializeField] private MeshRenderer dimmer;

        [Header("자리")]
        [Tooltip("3 단계 · 홀드 — 크게 둔 카드의 자리. 카드 기준점(아래 가운데)의 위치 · 회전 · 배율.")]
        [SerializeField] private Transform showSpot;
        [Tooltip("버림 더미의 맨 위 카드 자리 (DiscardPileView.TopAnchor). 위치 · 회전 · 배율 그대로 닿는다.")]
        [SerializeField] private Transform discardSpot;

        [Header("연출 (10-ui.md 5-1 — 3 단계 1.0 = 카드 데이터의 등장 시간 1.0초. 1 단계는 없다)")]
        [Tooltip("3 단계 — 손(좌석 · 함정 칸)에서 가운데로 커지며 가는 초. 옛 1 단계의 이동 시간과 같다.")]
        [SerializeField] private float zoomMoveDuration = 0.46f;
        [Tooltip("3 단계 전체 초 — 커지는 시간 포함. 이 동안 어둡게 + 주인 태그.")]
        [SerializeField] private float zoomDuration = 1f;
        [Tooltip("버림 더미로 가는 초.")]
        [SerializeField] private float discardDuration = 0.38f;
        [Tooltip("남의 카드가 좌석의 손 자리에서 나올 때의 배율 — 뒷면 부채꼴 카드(36 × 54) 크기.")]
        [SerializeField] private float seatCardScale = 0.16f;
        [Tooltip("체인에 카드가 여러 장일 때 뒤에 낸 카드를 카메라 쪽으로 띄우는 z — 같은 자리라 면이 겹치지 않게.")]
        [SerializeField] private float stackZ = -0.05f;
        [Tooltip("3 단계 동안 3D 층을 덮는 어둠의 알파 (시안 C3D.dim .42).")]
        [SerializeField] private float dimAlpha = 0.42f;
        [SerializeField] private float dimFadeDuration = 0.2f;

        [Header("체인 배치 (10-ui.md 2-1 「카드 연출 자리」 — 프로토타입 v2 DOCK · HANDUP_LAY. 길이는 테이블 면 유닛 = 캔버스 px ÷ 100)")]
        [Tooltip("배치가 바뀔 때 카드가 옮겨 가는 초 — 시안 0.2.")]
        [SerializeField] private float layoutDuration = 0.2f;
        [Tooltip("앞 카드에 붙는 카드의 배율 (묶음의 첫 카드 대비).")]
        [SerializeField] private float dockScale = 0.84f;
        [Tooltip("붙는 카드와 앞 카드 사이 — 캔버스 24.")]
        [SerializeField] private float dockGap = 0.24f;
        [Tooltip("붙는 카드가 내려앉는 높이 — 캔버스 16.")]
        [SerializeField] private float dockDrop = 0.16f;
        [Tooltip("붙는 카드의 기울기 (도) — 한 장씩 번갈아 반대로.")]
        [SerializeField] private float dockTilt = 5f;
        [Tooltip("묶음 사이 — 카드 폭 대비.")]
        [SerializeField] private float groupGap = 0.36f;
        [Tooltip("한 줄의 최대 폭 — 넘으면 배율을 줄인다. 캔버스 1280.")]
        [SerializeField] private float maxRowWidth = 12.8f;
        [Tooltip("배율의 아래 끝 — 이보다 줄이지 않는다.")]
        [SerializeField] private float minScale = 0.66f;
        [Tooltip("처음 낸 카드를 가운데 둔 채 오른쪽으로 늘어놓을 때 화면 오른쪽 끝에 남길 여백 — 넘으면 줄 전체를 가운데로. 캔버스 44.")]
        [SerializeField] private float edgeMargin = 0.44f;
        [Tooltip("한 줄에 놓는 장수 — 넘으면 다음 줄로 (사용자 2026-11-11). 묶음이 많아 그 전에 화면 폭을 넘게 되면 그때 넘긴다.")]
        [SerializeField] private int maxPerRow = 14;
        [Tooltip("줄 수의 위 끝 — 마지막 줄은 장수 제한 없이 화면 밖으로 넘어간다 (사용자 2026-11-11).")]
        [SerializeField] private int maxRows = 2;
        [Tooltip("줄 사이 — 캔버스 24.")]
        [SerializeField] private float rowGap = 0.24f;

        [Header("손패를 올렸을 때 (프로토타입 v2 HANDUP_LAY)")]
        [Tooltip("손패가 올라와 있는 동안 배율의 위 끝 — 평소는 showSpot 의 배율(1.85).")]
        [SerializeField] private float handUpMaxScale = 1.15f;
        [Tooltip("체인 전체가 올라가는 높이 — 시안 40 × 2 = 캔버스 80.")]
        [SerializeField] private float handUpRaise = 0.8f;

        private const float HalfCanvasWidth = 9.6f; // 기준 화면 1920 의 절반
        private const float CanvasPerUnit = 100f;

        // 체인의 카드 한 장. 처리돼 떠난 카드도 체인이 다 끝날 때까지 자리를 남긴다 (Card = null) — 처리 도중 남은 카드가 다시 줄 서지 않게.
        private sealed class Entry
        {
            public int Id;
            public CardView Card;
            public string Name;
            public bool Negates; // 바로 아래 카드를 무효화하는 카운터인가 (본인만 무효는 아니다)
        }

        private struct Slot
        {
            public Vector2 Center; // 큰 자리(showSpot)의 카드 가운데에서 본 자리 — 테이블 면 유닛
            public float Scale;
            public float Tilt;     // 시계 방향 도
        }

        // 체인에 올라가 처리 결과를 기다리는 카드 — 낸 순서, 맨 뒤가 맨 위 카드. 함정 칸에서 나온 카드는 아직 그리지 않아 여기 없다.
        private readonly List<Entry> _chain = new List<Entry>();
        private Entry _big;         // 3 단계 — 가운데에서 크게 보여 주는 중인 카드
        private bool _handRaised;
        private float _near;        // showSpot 깊이의 원근 보정 — 테이블 면 길이에 곱하면 그 깊이의 월드 길이
        private float _bigScale;    // 큰 자리의 배율 (1.85)
        private float _cardWidth;
        private float _cardHeight;

        /// <summary>배치가 바뀌었다 — 체인 맨 아래 줄 카드의 아래 끝 (캔버스 y, 화면 가운데가 0 · 위가 +). 반응 타이머가 따라온다.</summary>
        public event System.Action<float> OnBottomChanged;

        private Camera _camera;
        private Tween _dimTween;

        private void Awake()
        {
            _camera = Camera.main;

            if (deck == null || actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null || seats == null || ownerTag == null || dimmer == null
                || showSpot == null || discardSpot == null || _camera == null)
            {
                Debug.LogError($"[{nameof(ChainCardsPresenter)}] 참조 중 비어 있는 것이 있습니다 (메인 카메라 포함).", this);
                return;
            }

            float cameraZ = _camera.transform.position.z;
            _near = (showSpot.position.z - cameraZ) / -cameraZ;
            _bigScale = showSpot.lossyScale.x / _near;

            SetDimAlpha(0f);
            dimmer.gameObject.SetActive(false);
        }

        /// <summary>체인 맨 위 카드(반응을 기다리는 카드)의 이름. 체인이 비었으면 null.</summary>
        public string TopCardName
        {
            get
            {
                for (int i = _chain.Count - 1; i >= 0; i--)
                {
                    if (_chain[i].Card != null) return _chain[i].Name;
                }

                return null;
            }
        }

        /// <summary>
        /// 내 손패가 올라왔다 · 내려갔다 — 올라와 있는 동안 체인의 카드를 작게 · 위로 비켜 손패와 겹치지 않게 한다. 판정과 상관없는 연출.
        /// 3 단계(크게 보여 주는 중)에도 바로 따른다.
        /// </summary>
        public void SetHandRaised(bool raised)
        {
            if (_handRaised == raised) return;

            _handRaised = raised;
            Place(null);
        }

        /// <summary>
        /// 카드가 체인에 올라갔다 (OnCardPlayed) — 3 단계(가운데에서 크게) 뒤 그 자리에 둔다. 연출 큐가 기다릴 코루틴.
        /// fromHand = 내 손패에서 떼어 온 카드. null 이면 새로 만든다 — fromSlot(발동한 함정이 있던 칸)이 있으면 그 자리에서, 없으면(남의 카드) 낸 사람 좌석의 손 자리에서.
        /// </summary>
        public IEnumerator PlayRoutine(int actorNumber, int cardInstanceId, int cardId, int[] targets, CardView fromHand, Transform fromSlot = null)
        {
            CardData data = deck.GetCard(cardId);
            CardView card = fromHand != null ? fromHand : Spawn(actorNumber, cardInstanceId, data, fromSlot);
            if (card == null || Find(cardInstanceId) != null)
            {
                Debug.LogWarning($"[{nameof(ChainCardsPresenter)}] 카드를 보여 주지 못했다. instanceId={cardInstanceId} cardId={cardId}", this);
                yield break;
            }

            Transform t = card.transform;
            t.SetParent(transform, true); // 손패 올리기 · 내리기에 끌려가지 않게
            card.SetRaycastBlocking(false);

            _chain.RemoveAll(e => e.Card == null); // 앞 체인의 처리 도중에 새 카드가 나왔다 — 떠난 카드의 빈자리를 걷고 다시 줄 세운다
            var entry = new Entry { Id = cardInstanceId, Card = card, Name = data != null ? data.cardName : null, Negates = IsNegating(data) };
            _chain.Add(entry);
            _big = entry;
            _cardWidth = card.Width;
            _cardHeight = card.Height;

            // 3 단계 — 손에서 바로 가운데로 크게. 모두가 카드를 읽는 구간
            ownerTag.Show(data != null ? data.type : CardType.Action, OwnerText(actorNumber, targets));
            FadeDim(true);
            Place(entry); // 앞서 나온 카드는 새 카드의 자리를 비우며 옮겨 간다
            yield return new WaitForSeconds(zoomDuration);

            // 어둠과 주인 태그를 걷고 체인의 제자리로 — 혼자면 그 크기 그대로, 앞 카드가 있으면 그 옆에
            ownerTag.Hide();
            FadeDim(false);
            if (_big == entry)
            {
                _big = null;
                Place(null);
            }
        }

        /// <summary>체인의 카드가 처리됐다 (효과 적용 · 무효화) — 버림 더미로 보내 지운다. 연출 큐가 기다릴 코루틴. 여기 없는 카드(함정 칸에서 나온 카드)면 바로 끝난다.</summary>
        public IEnumerator DiscardRoutine(int cardInstanceId)
        {
            CardView card = Take(cardInstanceId);
            if (card == null)
                yield break;

            card.SetGradeVisible(false); // 닿으면 바꿔 끼우는 더미의 맨 위 카드와 같게 — 등급 연출은 버림 더미에서 보이지 않는다
            MoveTo(card, discardSpot.position, discardSpot.rotation, discardSpot.lossyScale, discardDuration, Ease.InQuad);
            yield return new WaitForSeconds(discardDuration);
            Destroy(card.gameObject);
        }

        /// <summary>체인에서 카드를 떼어 넘긴다 — 내 카드가 손패로 돌아올 때 (04-card.md 7절 「손패로 반환」). 여기 없으면 null.</summary>
        public CardView Take(int cardInstanceId)
        {
            Entry entry = Find(cardInstanceId);
            if (entry == null)
                return null;

            CardView card = entry.Card;
            entry.Card = null; // 자리는 남긴다 — 남은 카드는 제자리
            if (_big == entry) _big = null;
            if (_chain.TrueForAll(e => e.Card == null)) _chain.Clear();
            return card;
        }

        private Entry Find(int cardInstanceId) => _chain.Find(e => e.Id == cardInstanceId && e.Card != null);

        // 앞 카드를 무효화하는 카운터 — 그 앞 카드는 「죽은」 카드로 보고 묶음을 가른다. 본인만 무효(C05)는 다른 사람에게 그대로 적용되므로 아니다.
        private static bool IsNegating(CardData data)
            => data != null && data.type == CardType.Counter && data.effects.Any(effect => effect is NegateEffect negate && !negate.selfOnly);

        // 체인의 카드를 모두 제자리로 보낸다. entering = 방금 낸 카드 (손에서 오는 시간으로), 나머지는 layoutDuration.
        // 3 단계인 카드는 배치와 상관없이 가운데 큰 자리 — 손패가 올라와 있으면 바로 제자리로.
        private void Place(Entry entering)
        {
            if (_chain.Count == 0) return;

            Slot[] slots = ComputeLayout(out float bottom);
            Vector3 bigCenter = showSpot.position + showSpot.up * (_cardHeight * showSpot.lossyScale.y / 2f);
            for (int i = 0; i < _chain.Count; i++)
            {
                Entry entry = _chain[i];
                if (entry.Card == null) continue;

                Slot slot = entry == _big && !_handRaised ? new Slot { Scale = _bigScale } : slots[i];
                // 카드 기준점이 아래 가운데라, 가운데 자리에서 기운 방향으로 반 높이만큼 내려 잡는다. 뒤에 낸 카드일수록 카메라 쪽으로.
                Quaternion rotation = showSpot.rotation * Quaternion.Euler(0f, 0f, -slot.Tilt);
                float scale = slot.Scale * _near;
                Vector3 center = bigCenter + (showSpot.right * slot.Center.x + showSpot.up * slot.Center.y) * _near + showSpot.forward * (stackZ * i);
                Vector3 position = center - rotation * Vector3.up * (_cardHeight * scale / 2f);
                MoveTo(entry.Card, position, rotation, Vector3.one * scale, entry == entering ? zoomMoveDuration : layoutDuration, Ease.OutQuart);
            }

            OnBottomChanged?.Invoke((bigCenter.y / _near + bottom) * CanvasPerUnit);
        }

        // 체인 배치 (프로토타입 v2 chainLayout) — 살아 있는 카드는 앞 카드 오른쪽에 작게 · 기울여 붙이고, 죽은 카드(살아 있는 무효화 카운터 바로 아래)는 새 묶음을 시작한다.
        // 「행동 → 카운터」 는 한 묶음, 「행동 → 카운터 → 카운터」 는 [행동] [카운터 · 카운터] — 카운터 둘이 비켜 행동이 살아 있음이 보인다.
        // 처음 낸 카드는 될 수 있으면 가운데 그대로, 오른쪽이 넘치면 줄 전체를 가운데로. maxPerRow 장을 넘으면 다음 줄 (여러 줄이면 줄마다 가운데).
        // bottom = 맨 아래 줄 카드의 아래 끝 (큰 자리의 카드 가운데 기준).
        private Slot[] ComputeLayout(out float bottom)
        {
            int count = _chain.Count;
            var alive = new bool[count];
            for (int i = count - 1; i >= 0; i--)
                alive[i] = i == count - 1 || !(alive[i + 1] && _chain[i + 1].Negates);

            // 줄 나누기 — maxPerRow 장이 찼거나, 가장 작은 배율로도 화면 폭을 넘게 되면 다음 줄로. 마지막 줄은 넘쳐도 그대로 둔다.
            var rowOf = new int[count];
            var head = new bool[count];             // 묶음의 첫 카드인가
            var first = new bool[count];            // 줄의 첫 카드인가
            var widths = new List<float> { 0f };    // 배율 1 일 때 줄의 폭
            float dockWidth = _cardWidth * dockScale;
            int inRow = 0;
            for (int i = 0; i < count; i++)
            {
                int row = widths.Count - 1;
                float add = alive[i] && inRow > 0 ? dockGap + dockWidth : (inRow > 0 ? _cardWidth * groupGap : 0f) + _cardWidth;
                if (inRow > 0 && row < maxRows - 1 && (inRow == maxPerRow || (widths[row] + add) * minScale > HalfCanvasWidth * 2f))
                {
                    widths.Add(0f);
                    row++;
                    inRow = 0;
                    add = _cardWidth;
                }

                rowOf[i] = row;
                first[i] = inRow == 0;
                head[i] = first[i] || !alive[i];
                widths[row] += add;
                inRow++;
            }

            int rows = widths.Count;

            float scale = Mathf.Max(minScale, Mathf.Min(_handRaised ? handUpMaxScale : _bigScale, maxRowWidth / widths.Max()));
            float rowStep = (_cardHeight + rowGap) * scale;
            float raise = _handRaised ? handUpRaise : 0f;

            var slots = new Slot[count];
            float x = 0f;
            int docked = 0;
            for (int i = 0; i < count; i++)
            {
                int row = rowOf[i];
                float y = raise + ((rows - 1) / 2f - row) * rowStep;
                if (first[i])
                {
                    float width = widths[row] * scale;
                    x = -_cardWidth * scale / 2f;
                    if (rows > 1 || x + width > HalfCanvasWidth - edgeMargin) x = -width / 2f;
                }

                if (head[i])
                {
                    if (!first[i]) x += _cardWidth * groupGap * scale;
                    slots[i] = new Slot { Center = new Vector2(x + _cardWidth * scale / 2f, y), Scale = scale };
                    x += _cardWidth * scale;
                    docked = 0;
                }
                else
                {
                    docked++;
                    x += dockGap * scale;
                    slots[i] = new Slot
                    {
                        Center = new Vector2(x + dockWidth * scale / 2f, y - dockDrop * scale),
                        Scale = scale * dockScale,
                        Tilt = docked % 2 == 1 ? dockTilt : -dockTilt,
                    };
                    x += dockWidth * scale;
                }
            }

            bottom = raise - (rows - 1) / 2f * rowStep - _cardHeight * scale / 2f;
            return slots;
        }

        /// <summary>남의 카드가 그 사람 손패로 돌아갔다 (「손패로 반환」) — 좌석의 손 자리로 보내 지운다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator ReturnToSeatRoutine(int actorNumber, int cardInstanceId)
        {
            CardView card = Take(cardInstanceId);
            if (card == null)
                yield break;

            MoveTo(card, SeatHandPoint(actorNumber), Quaternion.identity, Vector3.one * seatCardScale, discardDuration, Ease.InQuad);
            yield return new WaitForSeconds(discardDuration);
            Destroy(card.gameObject);
        }

        // 남이 낸 카드 — 그 좌석의 손 자리에 작게 만든다. 손패 내용은 비공개라 낸 순간에 앞면이 처음 보인다.
        private CardView Spawn(int actorNumber, int cardInstanceId, CardData data, Transform fromSlot)
        {
            if (data == null)
                return null;

            CardView card = Instantiate(PrefabOf(data.type), transform);
            card.Setup(cardInstanceId, data);
            if (fromSlot != null)
            {
                // 함정 칸에 뒷면으로 누워 있던 카드가 공개되며 나온다
                card.transform.SetPositionAndRotation(fromSlot.position, fromSlot.rotation * Quaternion.Euler(0f, 180f, 0f));
                card.transform.localScale = fromSlot.lossyScale;
            }
            else
            {
                card.transform.SetPositionAndRotation(SeatHandPoint(actorNumber), Quaternion.identity);
                card.transform.localScale = Vector3.one * seatCardScale;
            }

            return card;
        }

        // 좌석의 손 자리(HUD 화면 좌표)를 테이블 면(z 0)의 월드 좌표로. 좌석이 없으면(나 · 나간 사람) 테이블 가운데.
        private Vector3 SeatHandPoint(int actorNumber)
        {
            Vector3 tableCenter = new Vector3(transform.position.x, transform.position.y, 0f);
            if (!seats.TryGetHandScreenPoint(actorNumber, out Vector2 screenPoint))
                return tableCenter;

            Ray ray = _camera.ScreenPointToRay(screenPoint);
            return Mathf.Approximately(ray.direction.z, 0f) ? tableCenter : ray.GetPoint(-ray.origin.z / ray.direction.z);
        }

        private static void MoveTo(CardView card, Vector3 position, Quaternion rotation, Vector3 scale, float duration, Ease ease)
        {
            Transform t = card.transform;
            t.DOKill();
            t.DOMove(position, duration).SetEase(ease).SetLink(card.gameObject);
            t.DORotateQuaternion(rotation, duration).SetEase(ease).SetLink(card.gameObject);
            t.DOScale(scale, duration).SetEase(ease).SetLink(card.gameObject);
        }

        private void FadeDim(bool on)
        {
            _dimTween?.Kill();
            dimmer.gameObject.SetActive(true);
            _dimTween = DOVirtual.Float(dimmer.material.color.a, on ? dimAlpha : 0f, dimFadeDuration, SetDimAlpha).SetLink(dimmer.gameObject)
                .OnComplete(() => dimmer.gameObject.SetActive(on));
        }

        private void SetDimAlpha(float alpha)
        {
            Color color = dimmer.material.color;
            color.a = alpha;
            dimmer.material.color = color;
        }

        // 「낸 사람 → 대상」, 이름은 플레이어 색 (10-ui.md 2-1 「카드 연출 자리」). 대상이 없으면 낸 사람만.
        private static string OwnerText(int actorNumber, int[] targets)
        {
            if (targets == null || targets.Length == 0)
                return string.Format(OwnerOnlyFormat, NameOf(actorNumber));

            return string.Format(OwnerToTargetFormat, NameOf(actorNumber), string.Join(TargetSeparator, targets.Select(NameOf)));
        }

        private static string NameOf(int actorNumber)
        {
            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            return string.Format(NameFormat, ColorUtility.ToHtmlStringRGB(PlayerLook.InkOf(actorNumber)), isMe ? MeName : PlayerLook.NameOf(actorNumber));
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
    }
}
