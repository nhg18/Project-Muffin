using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 난도질 — 칼선 판(이 오브젝트의 <see cref="SlashLayer"/>)의 붉은 바탕 위로 화면 끝에서 끝까지 이어진 칼선이 쉴 새 없이 통째로 나타났다 사라진다 (시안 mkSlash).
    /// 가는 칼선은 매번, 굵은 칼선은 자주, 몇 번째마다 묵직한 칼선 + 번쩍. 길이가 짧아 끝이 화면 안에 보이면 날아가는 물체처럼 보인다.
    /// 칼선 굵기는 그림의 빛 부분 굵기 — 그림 높이는 바깥 번짐까지 더해 이 값 × 그림 배율.
    /// </summary>
    [RequireComponent(typeof(SlashLayer))]
    public class SlashFlurryFx : CardFxStep
    {
        [Tooltip("묵직한 칼선 때 번쩍일 판.")]
        [SerializeField] private Image flash;
        [SerializeField] private Sprite bandSprite;
        [SerializeField] private Sprite thinSprite;

        [Header("시간")]
        [Tooltip("난도질 길이 (ms). 지나면 칼선 판을 걷는다 — 다음 이펙트(일섬)가 시작하는 시각에 맞춘다.")]
        [SerializeField] private float durationMs = 1460f;
        [Tooltip("처음 칼질 간격 (ms).")]
        [SerializeField] private float gapStartMs = 80f;
        [Tooltip("가장 짧은 칼질 간격 (ms).")]
        [SerializeField] private float gapMinMs = 45f;
        [Tooltip("1ms 지날 때마다 간격이 줄어드는 양 (ms). 갈수록 빨라진다.")]
        [SerializeField] private float gapShrink = 0.025f;

        [Header("바탕")]
        [Tooltip("이 색들 중 하나로 바뀐다 (가운데 색, 가장자리는 검정).")]
        [SerializeField] private Color[] backgrounds =
        {
            new Color32(0x4a, 0x06, 0x10, 0xff), new Color32(0x2a, 0x03, 0x08, 0xff),
            new Color32(0x5c, 0x0a, 0x14, 0xff), new Color32(0x1a, 0x02, 0x05, 0xff),
        };
        [Tooltip("바탕 색이 바뀌는 간격 (ms).")]
        [SerializeField] private float backgroundMs = 110f;

        [Header("자리")]
        [Tooltip("칼선 가운데가 놓이는 범위 (화면 크기 대비 ±).")]
        [SerializeField] private float area = 0.3f;
        [Tooltip("수평에서 기우는 최대 각도.")]
        [SerializeField] private float maxAngle = 69f;
        [Tooltip("칼선 길이 (화면 대각선 배수, 최소 · 최대).")]
        [SerializeField] private Vector2 lengthRange = new Vector2(1.6f, 2f);

        [Header("가는 칼선")]
        [Tooltip("굵기 (px, 최소 · 최대).")]
        [SerializeField] private Vector2 thinWidth = new Vector2(3f, 8f);
        [Tooltip("그림 높이 = 굵기 × 이 값.")]
        [SerializeField] private float thinSpriteScale = 4f;
        [Tooltip("그대로 보이는 시간 (ms, 최소 · 최대).")]
        [SerializeField] private Vector2 thinDrawMs = new Vector2(50f, 90f);
        [Tooltip("나타나서 다 사라질 때까지 (ms, 최소 · 최대).")]
        [SerializeField] private Vector2 thinLifeMs = new Vector2(200f, 320f);
        [Tooltip("한 번에 가는 칼선을 하나 더 그을 확률.")]
        [Range(0f, 1f)]
        [SerializeField] private float extraThinChance = 0.5f;
        [Tooltip("옆에 더 가는 칼선이 붙어 두 가닥이 될 확률.")]
        [Range(0f, 1f)]
        [SerializeField] private float twinChance = 0.5f;
        [Tooltip("두 가닥 사이 (굵기 배수).")]
        [SerializeField] private float twinOffset = 1.8f;
        [Tooltip("옆 가닥 그림 높이 = 굵기 × 이 값.")]
        [SerializeField] private float twinSpriteScale = 2.4f;

        [Header("굵은 칼선")]
        [Tooltip("이 번째마다 반드시 굵은 칼선.")]
        [SerializeField] private int bandEvery = 2;
        [Tooltip("그 밖의 칼질에서 굵은 칼선이 나올 확률.")]
        [Range(0f, 1f)]
        [SerializeField] private float bandChance = 0.55f;
        [Tooltip("굵기 (px, 최소 · 최대). 화면 대각선 / widthReference 배로 키운다.")]
        [SerializeField] private Vector2 bandWidth = new Vector2(16f, 38f);
        [Tooltip("굵기가 1배인 화면 대각선 (px).")]
        [SerializeField] private float widthReference = 1500f;
        [Tooltip("화면이 커도 굵기는 이 배수까지.")]
        [SerializeField] private float maxWidthScale = 1.4f;
        [Tooltip("그림 높이 = 굵기 × 이 값.")]
        [SerializeField] private float bandSpriteScale = 3.4f;
        [Tooltip("그대로 보이는 시간 (ms, 최소 · 최대).")]
        [SerializeField] private Vector2 bandDrawMs = new Vector2(60f, 100f);
        [Tooltip("나타나서 다 사라질 때까지 (ms, 최소 · 최대).")]
        [SerializeField] private Vector2 bandLifeMs = new Vector2(240f, 380f);
        [Tooltip("굵은 칼선 때 흔들림 (px).")]
        [SerializeField] private float bandShake = 11f;
        [Tooltip("흔들리는 시간 (ms).")]
        [SerializeField] private float shakeMs = 140f;

        [Header("묵직한 칼선")]
        [Tooltip("이 번째마다 묵직한 칼선 (굵은 칼선을 더 굵고 길게 + 번쩍 + 판 튕김).")]
        [SerializeField] private int heavyEvery = 5;
        [SerializeField] private float heavyWidthScale = 1.7f;
        [Tooltip("그대로 보이는 시간 (ms).")]
        [SerializeField] private float heavyDrawMs = 55f;
        [Tooltip("굵은 칼선보다 더 남는 시간 (ms).")]
        [SerializeField] private float heavyExtraLifeMs = 120f;
        [Tooltip("흔들림 (px).")]
        [SerializeField] private float heavyShake = 22f;
        [Range(0f, 1f)]
        [SerializeField] private float heavyFlashAlpha = 0.45f;
        [Tooltip("번쩍임이 사라지는 시간 (ms).")]
        [SerializeField] private float heavyFlashMs = 70f;
        [Tooltip("판이 튕기는 크기 (1 에 더하는 값).")]
        [SerializeField] private float heavyPunch = 0.05f;
        [Tooltip("판이 튕기는 시간 (ms).")]
        [SerializeField] private float heavyPunchMs = 160f;

        private SlashLayer _slashes;
        private FxShaker _shaker;

        public override float DurationMs => durationMs;

        private SlashLayer Slashes => _slashes != null ? _slashes : _slashes = GetComponent<SlashLayer>();

        private void Awake()
        {
            _shaker = GetComponentInParent<FxShaker>(true);
            if (flash == null || bandSprite == null || thinSprite == null
                || backgrounds == null || backgrounds.Length == 0)
                Debug.LogError($"[{nameof(SlashFlurryFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            Slashes.ResetState();
            SetAlpha(flash, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            StopRoutines();
            Slashes.Alpha = 1f;
            StartRoutine(FlurryRoutine());
        }

        private IEnumerator FlurryRoutine()
        {
            float start = Time.time;
            float lastSpawn = float.NegativeInfinity;
            float lastBackground = float.NegativeInfinity;
            int spawned = 0;

            while (true)
            {
                float elapsedMs = (Time.time - start) * 1000f;
                if (elapsedMs > durationMs)
                {
                    Slashes.ResetState();
                    yield break;
                }

                if (elapsedMs - lastBackground > backgroundMs)
                {
                    lastBackground = elapsedMs;
                    Slashes.Background.color = backgrounds[Random.Range(0, backgrounds.Length)];
                }

                if (elapsedMs - lastSpawn > Mathf.Max(gapMinMs, gapStartMs - elapsedMs * gapShrink))
                {
                    lastSpawn = elapsedMs;
                    spawned++;
                    bool heavy = heavyEvery > 0 && spawned % heavyEvery == 0;

                    SpawnThin();
                    if (Random.value < extraThinChance)
                        SpawnThin();

                    if (heavy || (bandEvery > 0 && spawned % bandEvery == 0) || Random.value < bandChance)
                    {
                        SpawnBand(heavy);
                        _shaker?.Shake(heavy ? heavyShake : bandShake, shakeMs);
                        if (heavy)
                        {
                            Flash(flash, heavyFlashAlpha, heavyFlashMs);
                            _shaker?.Punch(heavyPunch, heavyPunchMs);
                        }
                    }
                }

                yield return null;
            }
        }

        private void SpawnBand(bool heavy)
        {
            PickLine(out Vector2 center, out float length, out float angle, out float diagonal);
            float width = Range(bandWidth) * Mathf.Min(maxWidthScale, diagonal / widthReference) * (heavy ? heavyWidthScale : 1f);
            float draw = heavy ? heavyDrawMs : Range(bandDrawMs);
            float life = Range(bandLifeMs) + (heavy ? heavyExtraLifeMs : 0f);
            Fade(Slashes.Spawn(bandSprite, center, length, width * bandSpriteScale, angle), draw, life);
        }

        private void SpawnThin()
        {
            PickLine(out Vector2 center, out float length, out float angle, out _);
            float width = Range(thinWidth);
            float draw = Range(thinDrawMs);
            float life = Range(thinLifeMs);
            Fade(Slashes.Spawn(thinSprite, center, length, width * thinSpriteScale, angle), draw, life);

            if (Random.value >= twinChance)
                return;
            Vector2 side = Quaternion.Euler(0f, 0f, angle) * Vector2.down * width * twinOffset;
            Fade(Slashes.Spawn(thinSprite, center + side, length, width * twinSpriteScale, angle), draw, life);
        }

        // 화면 가운데 범위 안 아무 곳, 거의 수평 (좌우 어느 쪽으로든)
        private void PickLine(out Vector2 center, out float length, out float angle, out float diagonal)
        {
            Vector2 screen = ScreenSize;
            diagonal = screen.magnitude;
            center = new Vector2(screen.x * Random.Range(-area, area), screen.y * Random.Range(-area, area));
            length = diagonal * Range(lengthRange);
            angle = Random.Range(-maxAngle, maxAngle) + (Random.value < 0.5f ? 0f : 180f);
        }

        // 그어지는 시간(draw) 동안 그대로 있다가 남은 시간 동안 사라진다
        private static void Fade(Image slash, float drawMs, float lifeMs)
        {
            DOTween.Sequence().SetLink(slash.gameObject)
                .AppendInterval(Seconds(drawMs))
                .Append(slash.DOFade(0f, Seconds(lifeMs - drawMs)))
                .OnComplete(() => Destroy(slash.gameObject));
        }

        private static float Range(Vector2 range) => Random.Range(range.x, range.y);
    }
}
