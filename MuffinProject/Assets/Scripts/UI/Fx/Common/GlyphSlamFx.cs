using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 글자 내려찍기 — 큰 글자 그림이 흐린 채 내려앉으며 번쩍 → 살짝 튕기고, 흰 고리가 퍼진다. 머무는 시간이 지나면 사라진다 (지옥참마도: 絶).
    /// 번짐(glow)은 시안의 blur 8 · brightness 3 → 1 을 흉내 낸다. 글자는 glyph · glow 그림만 바꾸면 된다.
    /// </summary>
    public class GlyphSlamFx : CardFxStep
    {
        [Tooltip("글자 그림 (카드마다 바꾼다).")]
        [SerializeField] private Image glyph;
        [Tooltip("글자 위 번짐 (더하기) — 글자를 흐리게 키운 그림. glyph 의 자식.")]
        [SerializeField] private Image glow;
        [Tooltip("글자와 함께 퍼지는 흰 고리 (시안 ringAt).")]
        [SerializeField] private Image ring;

        [Header("크기")]
        [Tooltip("글자 크기 = 화면 짧은 변 × 이 값.")]
        [SerializeField] private float sizeRatio = 0.56f;
        [Tooltip("글자 크기 상한 (px).")]
        [SerializeField] private float maxSize = 520f;
        [Tooltip("고리 지름 = 화면 짧은 변 × 이 값 (시안 30vmin).")]
        [SerializeField] private float ringSizeRatio = 0.3f;

        [Header("글자")]
        [SerializeField] private float fromScale = 1.6f;
        [SerializeField] private float landScale = 0.94f;
        [Tooltip("내려앉는 시간 (ms). 이 동안 나타난다.")]
        [SerializeField] private float landMs = 140f;
        [SerializeField] private float bounceScale = 1.02f;
        [SerializeField] private float bounceMs = 84f;
        [SerializeField] private float settleScale = 0.98f;
        [SerializeField] private float settleMs = 336f;
        [Tooltip("번짐이 사라지는 시간 (ms).")]
        [SerializeField] private float glowMs = 560f;
        [Tooltip("글자가 보이는 시간 (ms). 지나면 사라진다 — 지옥참마도는 난도질이 시작할 때까지.")]
        [SerializeField] private float holdMs = 590f;

        [Header("고리")]
        [SerializeField] private float ringFromScale = 0.1f;
        [SerializeField] private float ringToScale = 3f;
        [Tooltip("고리가 퍼지며 사라지는 시간 (ms).")]
        [SerializeField] private float ringMs = 500f;

        private Sequence _sequence;

        public override float DurationMs => Mathf.Max(holdMs, ringMs);

        private void Awake()
        {
            if (glyph == null || glow == null || ring == null)
                Debug.LogError($"[{nameof(GlyphSlamFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            _sequence?.Kill();
            glow.DOKill();
            ring.DOKill();
            ring.rectTransform.DOKill();
            SetAlpha(glyph, 0f);
            SetAlpha(glow, 0f);
            SetAlpha(ring, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            Vector2 screen = ScreenSize;
            float shortSide = Mathf.Min(screen.x, screen.y);
            float size = Mathf.Min(shortSide * sizeRatio, maxSize);
            RectTransform rect = glyph.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.one * fromScale;

            _sequence?.Kill();
            SetAlpha(glyph, 0f);
            SetAlpha(glow, 1f);
            _sequence = DOTween.Sequence().SetLink(gameObject)
                .Append(rect.DOScale(landScale, Seconds(landMs)).SetEase(Ease.OutCubic))
                .Join(glyph.DOFade(1f, Seconds(landMs)))
                .Join(glow.DOFade(0f, Seconds(glowMs)).SetEase(Ease.OutCubic))
                .Insert(Seconds(landMs), rect.DOScale(bounceScale, Seconds(bounceMs)))
                .Insert(Seconds(landMs + bounceMs), rect.DOScale(settleScale, Seconds(settleMs)).SetEase(Ease.OutCubic))
                .InsertCallback(Seconds(holdMs), () =>
                {
                    SetAlpha(glyph, 0f);
                    SetAlpha(glow, 0f);
                });

            RectTransform ringRect = ring.rectTransform;
            ringRect.sizeDelta = Vector2.one * shortSide * ringSizeRatio;
            ringRect.localScale = Vector3.one * ringFromScale;
            SetAlpha(ring, 1f);
            ringRect.DOScale(ringToScale, Seconds(ringMs)).SetEase(Ease.OutQuad).SetLink(gameObject);
            ring.DOFade(0f, Seconds(ringMs)).SetEase(Ease.OutQuad).SetLink(gameObject);
        }
    }
}
