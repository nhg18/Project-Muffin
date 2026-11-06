using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 대상 칼질 — 대상 자리에 칼선을 정한 간격으로 여러 번 긋고 (칼선은 화면을 덮는 이 오브젝트 아래에 찍는다), 그을 때마다 판을 흔든다 (시안 FINISH slashDOM).
    /// 칼선은 왼쪽부터 드러나고, 잠시 머물다, 가늘어지며 사라진다. 지옥참마도는 짧은 칼질 ×7 · 큰 칼질 하나.
    /// 크기는 시안 px — 연출 레이어 PixelScale 을 곱한다.
    /// </summary>
    public class TargetSlashFx : CardFxStep
    {
        [SerializeField] private Sprite sprite;
        [Tooltip("빛을 더해서 그리는 머티리얼.")]
        [SerializeField] private Material additive;
        [Tooltip("그을 때 번쩍일 판. 비우면 번쩍이지 않는다.")]
        [SerializeField] private Image flash;

        [Header("칼질")]
        [Tooltip("긋는 횟수.")]
        [Min(1)]
        [SerializeField] private int count = 7;
        [Tooltip("긋는 간격 (ms).")]
        [SerializeField] private float intervalMs = 45f;
        [Tooltip("대상 가운데에서 흩어지는 거리 (px).")]
        [SerializeField] private float jitter = 14f;
        [Tooltip("길이 (px, 최소 · 최대).")]
        [SerializeField] private Vector2 lengthRange = new Vector2(280f, 400f);
        [Tooltip("빛 굵기 (px, 최소 · 최대).")]
        [SerializeField] private Vector2 widthRange = new Vector2(6f, 11f);
        [Tooltip("기울기 (도, 시계 방향 +, 최소 · 최대).")]
        [SerializeField] private Vector2 angleRange = new Vector2(-80f, 80f);
        [Tooltip("이 색들 중 하나로.")]
        [SerializeField] private Color[] colors = { new Color32(0xff, 0x2a, 0x3a, 0xff), new Color32(0xff, 0x00, 0x22, 0xff), new Color32(0xff, 0x6a, 0x6a, 0xff) };
        [Tooltip("칼선 하나가 나타나서 사라질 때까지 (ms).")]
        [SerializeField] private float lifeMs = 240f;

        [Header("모양")]
        [Tooltip("다 드러나는 때 (칼선 시간 대비 0~1).")]
        [Range(0f, 1f)]
        [SerializeField] private float revealAt = 0.25f;
        [Tooltip("사라지기 시작하는 때 (칼선 시간 대비 0~1).")]
        [Range(0f, 1f)]
        [SerializeField] private float fadeAt = 0.5f;
        [Tooltip("사라질 때 남는 굵기 비율.")]
        [SerializeField] private float endThickness = 0.1f;
        [Tooltip("그림 너비 = 길이 × 이 값 (양끝 번짐).")]
        [SerializeField] private float lengthPadding = 1.15f;
        [Tooltip("그림에서 빛 굵기가 차지하는 비율 — 그림 높이 = 굵기 / 이 값.")]
        [SerializeField] private float coreRatio = 0.08f;

        [Header("충격")]
        [Tooltip("그을 때마다 판 흔들림 (시안 px). 0 = 흔들지 않는다.")]
        [SerializeField] private float shake = 6f;
        [Tooltip("판 튕김 (1 에 더하는 값). 0 = 튕기지 않는다.")]
        [SerializeField] private float punch;
        [Range(0f, 1f)]
        [SerializeField] private float flashAlpha = 0.9f;
        [Tooltip("번쩍임이 사라지는 시간 (ms).")]
        [SerializeField] private float flashMs = 120f;

        public override float DurationMs => (count - 1) * intervalMs + lifeMs;

        private void Awake()
        {
            if (sprite == null || additive == null || colors == null || colors.Length == 0)
                Debug.LogError($"[{nameof(TargetSlashFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            if (flash != null)
                SetAlpha(flash, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            StartRoutine(SlashRoutine(context.Layer, target));
        }

        private IEnumerator SlashRoutine(FxLayer layer, RectTransform target)
        {
            float px = layer.PixelScale;
            var area = (RectTransform)transform;
            Vector2 center = area.InverseTransformPoint(target.TransformPoint(target.rect.center));
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter)) * px;
                Cut(center + offset, Range(lengthRange) * px, Range(widthRange) * px, Range(angleRange), colors[Random.Range(0, colors.Length)]);
                if (shake > 0f)
                    layer.ShakeStage(shake, true);
                if (punch > 0f)
                    layer.PunchStage(punch);
                if (flash != null)
                {
                    // 번쩍임은 찍어 낸 칼선보다 위에
                    flash.transform.SetAsLastSibling();
                    Flash(flash, flashAlpha, flashMs);
                }

                if (i < count - 1)
                    yield return new WaitForSeconds(Seconds(intervalMs));
            }
        }

        private void Cut(Vector2 center, float length, float width, float angle, Color color)
        {
            var go = new GameObject("Slash", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(length * lengthPadding, width / coreRatio);
            rect.localRotation = Quaternion.Euler(0f, 0f, -angle);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.material = additive;
            image.color = color;
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = true;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;

            DOVirtual.Float(0f, 1f, Seconds(lifeMs), t =>
            {
                image.fillAmount = revealAt > 0f ? Mathf.Clamp01(t / revealAt) : 1f;
                float fade = fadeAt < 1f ? Mathf.Clamp01((t - fadeAt) / (1f - fadeAt)) : 0f;
                SetAlpha(image, 1f - fade);
                rect.localScale = new Vector3(1f, Mathf.Lerp(1f, endThickness, fade), 1f);
            }).SetEase(Ease.OutQuad).SetLink(go).OnComplete(() => Destroy(go));
        }

        private static float Range(Vector2 range) => Random.Range(range.x, range.y);
    }
}
