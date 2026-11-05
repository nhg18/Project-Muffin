using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 카드 연출 테스트 씬 (RPG 스킬 시험장처럼). 순서와 값은 아티팩트 「찹츄 카드 이펙트」 의 runCard 와 같다:
    /// 체력 초기화 → 250ms → 카드가 가운데로 날아가 놓임(playCard) → 카드 연출 → 피해(hit) → 900ms → 버림 더미로(clearSlot).
    /// 규칙 · 네트워크는 거치지 않는다. 새 카드 연출은 <see cref="CardFx"/> 를 상속한 프리팹을 만들고 <see cref="entries"/> 에 한 줄 더한다.
    /// 키: 1~9 = 그 카드, Space = 마지막 카드 다시, R = 체력 초기화. 배속은 인스펙터 <see cref="speed"/>.
    /// </summary>
    public class CardFxTestBench : MonoBehaviour
    {
        // 시안 카드 너비(px). 프리팹 카드를 이 크기 × 레이어 PixelScale 로 맞춘다
        private const float DesignCardWidth = 110f;

        /// <summary>시험할 카드 한 장. 카드 에셋 없이 화면에 보일 것만 적는다.</summary>
        [Serializable]
        public class Entry
        {
            public string cardName;
            public CardType type;
            public Sprite art;
            [TextArea] public string description;
            [Tooltip("Resources/CardFx/ 의 연출 프리팹 이름 (= 연출 컴포넌트 이름, 예: HellBladeFx). 재생할 때 불러온다. 비우면 결과만 보인다.")]
            public string fxName;
            [Tooltip("테스트 표시용 피해. 규칙 수치가 아니다.")]
            public int damage;
            [Tooltip("큰 피해 연출 (시안 hit 의 big — 파편 40 · 번짐 · 판 흔들림 14).")]
            public bool bigHit;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

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
        [SerializeField] private Color hitColor = new Color32(0xff, 0x4f, 0x2e, 0xff);
        [SerializeField] private Color[] hitColors = { new Color32(0xff, 0x4f, 0x2e, 0xff), new Color32(0xff, 0xd2, 0x3f, 0xff), Color.white };

        private readonly List<HandCardView> _handCards = new List<HandCardView>();
        private int _lastIndex = -1;
        private bool _busy;

        private float CardScale => fxLayer.PixelScale * DesignCardWidth / ((RectTransform)actionCardPrefab.transform).sizeDelta.x;

        private void Awake()
        {
            if (actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null || handArea == null || playSpot == null
                || discardPile == null || fxLayer == null || caster == null || target == null || captionText == null
                || replayButton == null || resetButton == null || playedGlow == null)
                Debug.LogError($"[{nameof(CardFxTestBench)}] 참조 중 비어 있는 것이 있습니다.", this);
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
            for (int i = 0; i < entries.Count; i++)
                _handCards.Add(CreateHandCard(i));
            captionText.text = entries.Count > 0 ? "카드를 누르면 연출이 나옵니다." : "entries 에 카드를 추가하세요.";
        }

        private void Update()
        {
            for (int i = 0; i < Mathf.Min(entries.Count, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    Play(i);
            }

            if (Input.GetKeyDown(KeyCode.Space))
                HandleReplayClicked();
            if (Input.GetKeyDown(KeyCode.R))
                HandleResetClicked();
        }

        /// <summary>목록의 index 번 카드를 낸다. 연출 중이면 무시한다.</summary>
        public void Play(int index)
        {
            if (_busy || index < 0 || index >= entries.Count)
                return;
            StartCoroutine(PlayRoutine(index));
        }

        private IEnumerator PlayRoutine(int index)
        {
            _busy = true;
            _lastIndex = index;
            Entry entry = entries[index];
            captionText.text = $"{entry.cardName} — {entry.description}";
            HandleResetClicked();

            yield return new WaitForSeconds(0.25f);
            RectTransform played = CreateFlyingCard(entry, index);
            yield return FlyRoutine(played, fxLayer.CenterOf((RectTransform)_handCards[index].transform), fxLayer.CenterOf(playSpot),
                0.46f, 0.45f, 1.1f, -20f, 0f, 80f);
            Land(played);

            CardFx fx = null;
            if (!string.IsNullOrEmpty(entry.fxName))
                yield return CardFxLoader.CreateRoutine(entry.fxName, fxLayer.transform, created => fx = created);

            if (fx != null)
            {
                Stretch((RectTransform)fx.transform);
                var context = new CardFxContext
                {
                    Layer = fxLayer,
                    Caster = (RectTransform)caster.transform,
                    Targets = new[] { (RectTransform)target.transform },
                    Impact = _ => Hit(entry),
                };
                yield return fx.PlayRoutine(context);
                yield return ClearRoutine(played);
                StartCoroutine(CardFxLoader.ReleaseRoutine(fx));
                fx = null;
            }
            else
            {
                Debug.LogWarning($"[{nameof(CardFxTestBench)}] {entry.cardName} 에 연출 프리팹이 없다 — 결과만 보인다.");
                Hit(entry);
                yield return new WaitForSeconds(0.9f);
                yield return ClearRoutine(played);
            }

            _busy = false;
        }

        // 시안 hit: 터짐 숫자 · 파편 · (큰 피해면) 번짐 · 판 흔들림 · 자리 흔들림 + 체력
        private void Hit(Entry entry)
        {
            var seat = (RectTransform)target.transform;
            Vector2 center = fxLayer.CenterOf(seat);
            float px = fxLayer.PixelScale;
            fxLayer.Boom(center + new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(30f, 60f)) * px, hitColor, $"-{entry.damage}");
            fxLayer.Burst(center, entry.bigHit ? 40 : 20, hitColors, new Vector2(200f, 520f), 500f);
            if (entry.bigHit)
                fxLayer.Bloom(center, hitColor, 260f, 0.4f);
            fxLayer.ShakeStage(entry.bigHit ? 14f : 6f, false);
            target.TakeDamage(entry.damage);
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
        private RectTransform CreateFlyingCard(Entry entry, int index)
        {
            HandCardView card = Instantiate(PrefabOf(entry.type), fxLayer.transform);
            card.Setup(index, CreateCardData(entry));
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsFirstSibling();

            if (entry.type == CardType.Action)
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

            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
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
            Entry entry = entries[index];
            HandCardView card = Instantiate(PrefabOf(entry.type), handArea);
            card.Setup(index, CreateCardData(entry));

            // 손패 카드에는 입력이 없다 — 시험장에서만 누를 수 있게 투명 판 + 버튼을 붙인다.
            Image hit = card.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() => Play(index));
            return card;
        }

        // 화면에 보이는 값만 채운 임시 카드 정의 — 에셋으로 저장하지 않는다.
        private static CardData CreateCardData(Entry entry)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            data.cardName = entry.cardName;
            data.type = entry.type;
            data.cardImage = entry.art;
            data.description = entry.description;
            return data;
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
        }
    }
}
