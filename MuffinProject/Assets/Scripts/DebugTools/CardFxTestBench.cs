using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Game.Effects;
using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 카드 연출 시험장 (CardFxTestScene, RPG 스킬 시험장처럼). 덱의 카드가 모두 손패로 깔리고, 누르면 그 카드의 연출
    /// (Resources/CardFx/Card_{카드 ID})을 게임과 같은 순서로 튼다 (10-ui 5-1):
    /// 체력 초기화 → 250ms → 카드가 가운데로 날아가 놓임 → 3 단계(<see cref="CardFx.PlayRoutine"/>) →
    /// 4 단계: 카드 장식(<see cref="CardFx.PlayPostRoutine"/>)과 같이 공통 결과(맞음 · 회복) → 장식이 끝나고 900ms → 버림 더미로.
    /// 규칙 · 네트워크는 거치지 않는다. 공통 결과는 카드 효과 데이터의 피해(DamageEffect) · 회복(HealEffect) 값으로 흉내 낸다 — 손패 이동 등 그 밖의 결과는 그리지 않는다.
    /// 자리는 둘 — 나(낸 사람) · 상대(대상). 연출 프리팹이 없는 카드는 3 단계 없이 결과만.
    /// 키: Space = 마지막 카드 다시, R = 체력 초기화. 배속은 인스펙터 <see cref="speed"/>. 자동 확인(run.sh fx)은 <see cref="PlayCard"/> 로 튼다.
    /// </summary>
    public class CardFxTestBench : MonoBehaviour
    {
        // 시안 카드 너비(px). 프리팹 카드를 이 크기 × 레이어 PixelScale 로 맞춘다
        private const float DesignCardWidth = 110f;
        private const int MeActor = 1;
        private const int OtherActor = 2;

        [Header("카드")]
        [Tooltip("손패에 깔 카드 — 게임과 같은 덱 에셋 (Deck_Main). 이름 · 그림 · 종류 · 효과 값을 읽는다.")]
        [SerializeField] private DeckData cardDeck;
        [Tooltip("카드 뒷면 — 연출의 날아가는 카드 · 컷인 속 카드.")]
        [SerializeField] private Sprite cardBack;
        [Tooltip("캐릭터 그림 — 컷인에 나오는 강아지.")]
        [SerializeField] private Sprite character;
        [Tooltip("한 번에 이만큼 이상 줄면 큰 피해 연출 (10-ui 5-2 「큰 피해(30 이상)」, 시안 hit 의 big — 파편 40 · 번짐 · 판 흔들림 14).")]
        [Min(1)] [SerializeField] private int bigHitDamage = 30;

        [Header("재생 속도")]
        [Tooltip("모든 카드 연출의 배속 (Time.timeScale). 1 = 원래 속도. 플레이 중에 바꿔도 바로 적용된다.")]
        [Range(0.05f, 2f)]
        [SerializeField] private float speed = 1f;

        [Header("카드 프리팹 (손패와 같은 것)")]
        [SerializeField] private HandCardView actionCardPrefab;
        [SerializeField] private HandCardView counterCardPrefab;
        [SerializeField] private HandCardView trapCardPrefab;

        [Header("화면")]
        [SerializeField] private RectTransform handArea;
        [Tooltip("낸 카드가 놓이는 자리 (가운데).")]
        [SerializeField] private RectTransform playSpot;
        [Tooltip("다 쓴 카드가 날아가는 곳.")]
        [SerializeField] private RectTransform discardPile;
        [Tooltip("연출 레이어. 화면 전체를 덮고, 연출 프리팹 · 날아가는 카드도 여기에 만든다.")]
        [SerializeField] private FxLayer fxLayer;
        [SerializeField] private FxDummySeat caster;
        [SerializeField] private FxDummySeat target;
        [SerializeField] private TMP_Text captionText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button resetButton;

        [Header("놓인 카드")]
        [Tooltip("행동 카드가 놓였을 때 테두리 빛 (시안 glow-act).")]
        [SerializeField] private Sprite playedGlow;

        [Header("색")]
        [SerializeField] private Color[] landColors = { new Color32(0xff, 0xd2, 0x3f, 0xff), Color.white, new Color32(0xff, 0x5c, 0x8a, 0xff) };

        private readonly List<CardData> _cards = new List<CardData>();
        private readonly List<HandCardView> _handCards = new List<HandCardView>();
        private int _lastIndex = -1;
        private bool _busy;
        private bool _ready;

        /// <summary>3 단계를 재생 중인 카드 ID — 없으면 0. 자동 확인(스크린샷 시각 맞추기)용.</summary>
        public int PlayingCardId { get; private set; }

        /// <summary>지금 재생 중인 3 단계가 시작한 시각 (Time.time).</summary>
        public float PlayingSince { get; private set; }

        /// <summary>카드 하나를 내는 중(날아감 · 3 단계 · 4 단계 · 버림)이면 true.</summary>
        public bool IsBusy => _busy;

        private float CardScale => fxLayer.PixelScale * DesignCardWidth / ((RectTransform)actionCardPrefab.transform).sizeDelta.x;

        private void Awake()
        {
            var missing = new List<string>();
            void Check(Object value, string name)
            {
                if (value == null)
                    missing.Add(name);
            }

            Check(cardDeck, nameof(cardDeck));
            Check(cardBack, nameof(cardBack));
            Check(character, nameof(character));
            Check(actionCardPrefab, nameof(actionCardPrefab));
            Check(counterCardPrefab, nameof(counterCardPrefab));
            Check(trapCardPrefab, nameof(trapCardPrefab));
            Check(handArea, nameof(handArea));
            Check(playSpot, nameof(playSpot));
            Check(discardPile, nameof(discardPile));
            Check(fxLayer, nameof(fxLayer));
            Check(caster, nameof(caster));
            Check(target, nameof(target));
            Check(captionText, nameof(captionText));
            Check(replayButton, nameof(replayButton));
            Check(resetButton, nameof(resetButton));
            Check(playedGlow, nameof(playedGlow));
            _ready = missing.Count == 0;
            if (!_ready)
                Debug.LogError($"[{nameof(CardFxTestBench)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
        }

        private void OnEnable()
        {
            replayButton.onClick.AddListener(HandleReplayClicked);
            resetButton.onClick.AddListener(HandleResetClicked);
        }

        private void OnDisable()
        {
            replayButton.onClick.RemoveListener(HandleReplayClicked);
            resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        // 인스펙터에서 배속을 바꾸면 플레이 중에도 바로 적용
        private void OnValidate()
        {
            if (Application.isPlaying)
                Time.timeScale = speed;
        }

        // timeScale 은 씬을 넘어 남는다 — 시험장을 벗어나면 원래 속도로
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void Start()
        {
            Time.timeScale = speed;
            if (!_ready)
                return;
            // 마우스로 누른 버튼이 선택돼 남으면 Space 가 Submit 으로도 들어간다 — 시험장 버튼은 선택되지 않게
            replayButton.navigation = new Navigation { mode = Navigation.Mode.None };
            resetButton.navigation = new Navigation { mode = Navigation.Mode.None };

            foreach (DeckData.Entry entry in cardDeck.entries)
            {
                if (entry.card != null && !_cards.Contains(entry.card))
                    _cards.Add(entry.card);
            }
            _cards.Sort((a, b) => a.id.CompareTo(b.id));
            for (int i = 0; i < _cards.Count; i++)
                _handCards.Add(CreateHandCard(i));
            FitHand();
            captionText.text = _cards.Count > 0 ? "카드를 누르면 연출이 나옵니다." : "덱에 카드가 없습니다.";
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                HandleReplayClicked();
            if (Input.GetKeyDown(KeyCode.R))
                HandleResetClicked();
        }

        /// <summary>그 카드 ID 의 카드를 낸다. 덱에 없거나 연출 중이면 false.</summary>
        public bool PlayCard(int cardId)
        {
            int index = _cards.FindIndex(card => card.id == cardId);
            if (index < 0 || _busy)
                return false;
            Play(index);
            return true;
        }

        private void Play(int index)
        {
            if (_busy || index < 0 || index >= _cards.Count)
                return;
            StartCoroutine(PlayRoutine(index));
        }

        private IEnumerator PlayRoutine(int index)
        {
            _busy = true;
            _lastIndex = index;
            CardData data = _cards[index];
            captionText.text = $"{data.id} {data.cardName} — {data.description}";
            HandleResetClicked();
            // 체력이 가득 차 있으면 회복이 보이지 않는다 — 회복 카드는 회복량만큼 먼저 깎아 둔다
            foreach (CardEffect effect in data.effects)
            {
                if (effect is HealEffect heal)
                    (heal.subject == EffectSubject.Self ? caster : target).ResetHp(GameServer.MaxHp - heal.amount);
            }

            yield return new WaitForSeconds(0.25f);
            RectTransform played = CreateFlyingCard(data, index);
            yield return FlyRoutine(played, fxLayer.CenterOf((RectTransform)_handCards[index].transform), fxLayer.CenterOf(playSpot),
                0.46f, 0.45f, 1.1f, -20f, 0f, 80f);
            Land(played);

            CardFx fx = null;
            yield return CardFxLoader.CreateRoutine(CardFxLoader.NameOf(data.id), fxLayer.transform, created => fx = created, false);
            CardFxContext context = CreateContext(data);
            // 3 단계가 결과를 보여 줄 순간을 정하면(Impact) 그때, 아니면 3 단계 뒤에 (게임에서는 결과가 3 단계 뒤에 온다)
            var shown = new HashSet<CardFxSeat>();
            context.Impact = seat => ShowResults(data, context, shown, seat);
            Coroutine post = null;

            if (fx != null)
            {
                Stretch((RectTransform)fx.transform);
                PlayingCardId = data.id;
                PlayingSince = Time.time;
                yield return fx.PlayRoutine(context);
                Debug.Log($"[{nameof(CardFxTestBench)}] 카드 {data.id} {data.cardName} 3 단계 {Time.time - PlayingSince:0.00}초");
                PlayingCardId = 0;
                // StartCoroutine 은 첫 yield 까지 바로 돈다 — 장식이 결과 알림을 구독한 뒤에 결과를 보여 준다
                post = StartCoroutine(fx.PlayPostRoutine(context));
            }
            else
            {
                Debug.Log($"[{nameof(CardFxTestBench)}] 카드 {data.id} {data.cardName} — 연출 프리팹 없음 (Resources/CardFx/{CardFxLoader.NameOf(data.id)}), 결과만");
            }

            ShowResults(data, context, shown, null);
            if (post != null)
                yield return post;
            yield return new WaitForSeconds(0.9f);
            yield return ClearRoutine(played);
            if (fx != null)
            {
                StartCoroutine(CardFxLoader.ReleaseRoutine(fx));
                fx = null;
            }

            _busy = false;
        }

        private CardFxContext CreateContext(CardData data)
        {
            var me = new CardFxSeat { Actor = MeActor, Anchor = (RectTransform)caster.transform, Name = "나", Color = PlayerLook.MeColor, IsMe = true };
            var other = new CardFxSeat { Actor = OtherActor, Anchor = (RectTransform)target.transform, Name = "상대", Color = PlayerLook.SeatColors[0], IsMe = false };
            return new CardFxContext
            {
                Layer = fxLayer,
                Caster = me.Anchor,
                Targets = new[] { other.Anchor },
                CardId = data.id,
                CardName = data.cardName,
                CardArt = data.cardImage,
                CardBack = cardBack,
                Character = character,
                CasterSeat = me,
                TargetSeats = new[] { other },
                Seats = new[] { me, other },
                DrawRange = DrawRangeOf(data),
            };
        }

        // 카드 효과의 뽑기 장수 (최소, 최대) — 무작위 장수(116)면 count ~ randomMax. 뽑기가 없으면 (0, 0)
        private static Vector2Int DrawRangeOf(CardData data)
        {
            foreach (CardEffect effect in data.effects)
            {
                if (effect is DrawEffect draw && !draw.useTriggerAmount)
                    return new Vector2Int(draw.count, Mathf.Max(draw.count, draw.randomMax));
            }
            return Vector2Int.zero;
        }

        // 공통 결과 흉내 — 카드 효과의 피해 · 회복 값을 그 자리에 (시안 hit · heal). onlyAnchor 면 그 자리만. 자리마다 한 번만
        private void ShowResults(CardData data, CardFxContext context, HashSet<CardFxSeat> shown, RectTransform onlyAnchor)
        {
            foreach (CardEffect effect in data.effects)
            {
                if (effect is DamageEffect damage)
                    Apply(context, shown, onlyAnchor, damage.subject, -damage.amount);
                else if (effect is HealEffect heal)
                    Apply(context, shown, onlyAnchor, heal.subject, heal.amount);
            }
        }

        private void Apply(CardFxContext context, HashSet<CardFxSeat> shown, RectTransform onlyAnchor, EffectSubject subject, int delta)
        {
            IReadOnlyList<CardFxSeat> seats = subject == EffectSubject.Self ? new[] { context.CasterSeat } : context.TargetSeats;
            foreach (CardFxSeat seat in seats)
            {
                if ((onlyAnchor != null && seat.Anchor != onlyAnchor) || shown.Contains(seat))
                    continue;
                shown.Add(seat);

                FxDummySeat dummy = seat.Anchor.GetComponent<FxDummySeat>();
                int before = dummy.Hp;
                dummy.ShowHp(before + delta);
                // 10-ui 5-2: 양 = 실제로 바뀐 HP (0 ~ MaxHp 로 잘린 뒤). 바뀌지 않았으면 그리지 않는다
                int change = dummy.Hp - before;
                if (change == 0)
                    continue;
                if (change < 0)
                    fxLayer.Hit(seat.Anchor, -change, -change >= bigHitDamage);
                else
                    fxLayer.Heal(seat.Anchor, change);
                context.NotifyHpChanged(seat, before, dummy.Hp);
            }
        }

        // 시안 playCard 의 착지: 흰 고리 · 파편 16
        private void Land(RectTransform card)
        {
            Vector2 center = fxLayer.CenterOf(card);
            fxLayer.Ring(center, Color.white, 150f, 0.4f, 8f);
            fxLayer.Burst(center, 16, landColors, new Vector2(120f, 320f), 300f);
        }

        // 시안 clearSlot: 버림 더미로 날아가며 작아진다 (380ms) → 400ms 기다림
        private IEnumerator ClearRoutine(RectTransform card)
        {
            StartCoroutine(FlyRoutine(card, card.anchoredPosition, fxLayer.CenterOf(discardPile), 0.38f, 1.1f, 0.6f, 0f, 8f, 30f, true));
            yield return new WaitForSeconds(0.4f);
        }

        // 시안 fly(): 가운데에서 위로 휘는 3점 궤적, 크기 · 기울기 보간, 전체 cubic-bezier(.3,.6,.25,1)
        private IEnumerator FlyRoutine(RectTransform card, Vector2 from, Vector2 to, float duration, float scaleFrom, float scaleTo,
            float angleFrom, float angleTo, float arc, bool destroyAtEnd = false)
        {
            float baseScale = CardScale;
            Vector2 middle = new Vector2((from.x + to.x) / 2f, Mathf.Max(from.y, to.y) + arc * fxLayer.PixelScale);
            float middleScale = (scaleFrom + scaleTo) / 2f + 0.15f;
            float middleAngle = angleFrom / 2f;

            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = CubicBezier(0.3f, 0.6f, 0.25f, 1f, time / duration);
                bool first = t < 0.5f;
                float f = first ? t / 0.5f : (t - 0.5f) / 0.5f;
                card.anchoredPosition = first ? Vector2.Lerp(from, middle, f) : Vector2.Lerp(middle, to, f);
                card.localScale = Vector3.one * baseScale * (first ? Mathf.Lerp(scaleFrom, middleScale, f) : Mathf.Lerp(middleScale, scaleTo, f));
                // 시안은 시계 방향 + — 유니티는 반시계 방향 +
                card.localRotation = Quaternion.Euler(0f, 0f, -(first ? Mathf.Lerp(angleFrom, middleAngle, f) : Mathf.Lerp(middleAngle, angleTo, f)));
                yield return null;
            }

            card.anchoredPosition = to;
            card.localScale = Vector3.one * baseScale * scaleTo;
            card.localRotation = Quaternion.Euler(0f, 0f, -angleTo);
            if (destroyAtEnd)
                Destroy(card.gameObject);
        }

        // 손패 카드와 같은 모양을 연출 레이어에 하나 더 만든다 (손패 카드는 남겨 둔다 — 몇 번이고 다시 낼 수 있게)
        private RectTransform CreateFlyingCard(CardData data, int index)
        {
            HandCardView card = Instantiate(PrefabOf(data.type), fxLayer.transform);
            card.Setup(index, data);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsFirstSibling();

            if (data.type == CardType.Action)
            {
                // 시안 glow-act: 노란 테두리 + 번짐 (카드 뒤)
                var glow = new GameObject("PlayedGlow", typeof(RectTransform), typeof(Image));
                var glowRect = (RectTransform)glow.transform;
                glowRect.SetParent(rect, false);
                glowRect.SetAsFirstSibling();
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                Vector2 margin = rect.sizeDelta * new Vector2(0.36f, 0.24f);
                glowRect.offsetMin = -margin;
                glowRect.offsetMax = margin;
                Image image = glow.GetComponent<Image>();
                image.sprite = playedGlow;
                image.raycastTarget = false;
            }

            CanvasGroup group = card.GetComponent<CanvasGroup>(); // HandCardView 가 RequireComponent 로 갖고 있다
            group.blocksRaycasts = false;
            return rect;
        }

        private void HandleReplayClicked()
        {
            Play(_lastIndex < 0 ? 0 : _lastIndex);
        }

        private void HandleResetClicked()
        {
            caster.ResetHp();
            target.ResetHp();
        }

        private HandCardView CreateHandCard(int index)
        {
            HandCardView card = Instantiate(PrefabOf(_cards[index].type), handArea);
            card.Setup(index, _cards[index]);

            // 손패 카드에는 입력이 없다 — 시험장에서만 누를 수 있게 투명 판 + 버튼을 붙인다.
            Image hit = card.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Play(index));
            return card;
        }

        // 덱의 카드가 많아 손패 줄을 넘치면 카드끼리 겹쳐 한 줄에 들어가게 한다
        private void FitHand()
        {
            var layout = handArea.GetComponent<HorizontalLayoutGroup>();
            if (layout == null || _handCards.Count < 2)
                return;
            float cardWidth = ((RectTransform)_handCards[0].transform).rect.width * _handCards[0].transform.localScale.x;
            float room = handArea.rect.width - layout.padding.horizontal;
            layout.spacing = Mathf.Min(layout.spacing, (room - cardWidth * _handCards.Count) / (_handCards.Count - 1));
        }

        private HandCardView PrefabOf(CardType type)
        {
            switch (type)
            {
                case CardType.Counter: return counterCardPrefab;
                case CardType.Trap: return trapCardPrefab;
                default: return actionCardPrefab;
            }
        }

        // CSS cubic-bezier(x1, y1, x2, y2) — 진행률 x 에 대한 y
        private static float CubicBezier(float x1, float y1, float x2, float y2, float x)
        {
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float current = Bezier(x1, x2, t) - x;
                float slope = 3f * (1f - t) * (1f - t) * x1 + 6f * (1f - t) * t * (x2 - x1) + 3f * t * t * (1f - x2);
                if (Mathf.Abs(slope) < 1e-5f)
                    break;
                t = Mathf.Clamp01(t - current / slope);
            }
            return Bezier(y1, y2, t);
        }

        private static float Bezier(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
