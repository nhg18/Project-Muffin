using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 빛 터짐 — 한 점에서 납작한 고리 · 가로 줄 빛 · 별이 터진다 (지옥참마도: 칼이 뜰 때 칼집 이음새). 크기는 그림 칸 대비 배율 (가로, 세로).
    /// </summary>
    public class FlareBurstFx : CardFxStep
    {
        [SerializeField] private Image ring;
        [SerializeField] private Image streak;
        [SerializeField] private Image spark;

        [Tooltip("고리가 퍼지는 시간 (ms). 줄 빛 · 별은 이 시간에 비율을 곱한다.")]
        [SerializeField] private float durationMs = 520f;

        [Header("고리")]
        [SerializeField] private Vector2 ringStart = new Vector2(0.2f, 0.09f);
        [Tooltip("이 크기까지 빠르게 커진 뒤 사라지며 끝 크기로.")]
        [SerializeField] private Vector2 ringPeak = new Vector2(1f, 0.45f);
        [SerializeField] private Vector2 ringEnd = new Vector2(1.5f, 0.68f);
        [Tooltip("최대 크기에 닿는 때 (전체 시간 대비 0~1).")]
        [Range(0f, 1f)]
        [SerializeField] private float ringPeakAt = 0.35f;

        [Header("줄 빛")]
        [Tooltip("줄 빛 시간 = 고리 시간 × 이 값.")]
        [SerializeField] private float streakTime = 0.85f;
        [Tooltip("처음 가로 배율. 세로는 1 에서 시작.")]
        [SerializeField] private float streakStart = 0.05f;
        [SerializeField] private Vector2 streakEnd = new Vector2(1.4f, 0.15f);
        [Tooltip("다 뻗는 때 (줄 빛 시간 대비 0~1).")]
        [Range(0f, 1f)]
        [SerializeField] private float streakPeakAt = 0.3f;

        [Header("별")]
        [Tooltip("별 시간 = 고리 시간 × 이 값.")]
        [SerializeField] private float sparkTime = 0.8f;
        [SerializeField] private float sparkPeak = 1.3f;
        [Tooltip("사라질 때까지 도는 각도 (시계 방향 -).")]
        [SerializeField] private float sparkSpin = -90f;
        [Tooltip("가장 커지는 때 (별 시간 대비 0~1).")]
        [Range(0f, 1f)]
        [SerializeField] private float sparkPeakAt = 0.3f;

        public override float DurationMs => durationMs * Mathf.Max(1f, streakTime, sparkTime);

        private void Awake()
        {
            if (ring == null || streak == null || spark == null)
                Debug.LogError($"[{nameof(FlareBurstFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            SetAlpha(ring, 0f);
            SetAlpha(streak, 0f);
            SetAlpha(spark, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            float duration = Seconds(durationMs);
            RectTransform ringRect = ring.rectTransform;
            RectTransform streakRect = streak.rectTransform;
            RectTransform sparkRect = spark.rectTransform;

            DOVirtual.Float(0f, 1f, duration, t =>
            {
                float fade = Phase(t, ringPeakAt, out float grow);
                Vector2 size = t < ringPeakAt ? Vector2.Lerp(ringStart, ringPeak, grow) : Vector2.Lerp(ringPeak, ringEnd, fade);
                ringRect.localScale = new Vector3(size.x, size.y, 1f);
                SetAlpha(ring, 1f - fade);
            }).SetEase(Ease.OutCubic).SetLink(gameObject);

            DOVirtual.Float(0f, 1f, duration * streakTime, t =>
            {
                float fade = Phase(t, streakPeakAt, out float grow);
                streakRect.localScale = t < streakPeakAt
                    ? new Vector3(Mathf.Lerp(streakStart, 1f, grow), 1f, 1f)
                    : new Vector3(Mathf.Lerp(1f, streakEnd.x, fade), Mathf.Lerp(1f, streakEnd.y, fade), 1f);
                SetAlpha(streak, 1f - fade);
            }).SetLink(gameObject);

            DOVirtual.Float(0f, 1f, duration * sparkTime, t =>
            {
                float fade = Phase(t, sparkPeakAt, out float grow);
                sparkRect.localScale = Vector3.one * (t < sparkPeakAt ? Mathf.Lerp(0f, sparkPeak, grow) : Mathf.Lerp(sparkPeak, 0f, fade));
                sparkRect.localRotation = Quaternion.Euler(0f, 0f, sparkSpin * t);
                SetAlpha(spark, 1f - fade);
            }).SetLink(gameObject);
        }

        // t 를 커지는 구간(grow 0~1)과 사라지는 구간(반환 0~1)으로 나눈다
        private static float Phase(float t, float peakAt, out float grow)
        {
            grow = peakAt > 0f ? Mathf.Clamp01(t / peakAt) : 1f;
            return peakAt < 1f ? Mathf.Clamp01((t - peakAt) / (1f - peakAt)) : 0f;
        }
    }
}
