using UnityEngine;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 대상 터짐 — 대상 자리에 별 모양 바탕의 글자 그림이 튀어나오고, 사방으로 줄 빛 · 둥근 번짐 · 흰 고리. 연출 레이어(FxLayer)에 그린다.
    /// 크기는 시안 px — 연출 레이어 PixelScale 을 곱한다.
    /// </summary>
    public class TargetBoomFx : CardFxStep
    {
        // FxLayer 터짐 글자가 튀어나왔다 사라지는 시간
        private const float BoomMs = 950f;

        [Tooltip("터짐 글자 그림.")]
        [SerializeField] private Sprite glyph;
        [Tooltip("글자 뒤 별 모양 색.")]
        [SerializeField] private Color boomColor = new Color32(0xe0, 0x00, 0x1a, 0xff);
        [Tooltip("대상 가운데에서 글자가 놓이는 범위 (px, 가로 최소 · 최대).")]
        [SerializeField] private Vector2 offsetX = new Vector2(-30f, 30f);
        [Tooltip("대상 가운데에서 글자가 놓이는 범위 (px, 위 +, 최소 · 최대).")]
        [SerializeField] private Vector2 offsetY = new Vector2(30f, 60f);

        [Header("줄 빛")]
        [SerializeField] private int streakCount = 34;
        [SerializeField] private Color[] streakColors = { Color.white, new Color32(0xff, 0x2a, 0x3a, 0xff), new Color32(0xff, 0x6a, 0x6a, 0xff) };

        [Header("번짐")]
        [SerializeField] private Color bloomColor = new Color32(0xff, 0x1a, 0x2a, 0xff);
        [Tooltip("번짐 크기 (px).")]
        [SerializeField] private float bloomSize = 340f;
        [Tooltip("번짐이 사라지는 시간 (ms).")]
        [SerializeField] private float bloomMs = 450f;

        [Header("고리")]
        [SerializeField] private Color ringColor = Color.white;
        [Tooltip("고리가 퍼지는 반지름 (px).")]
        [SerializeField] private float ringRadius = 280f;
        [Tooltip("고리가 사라지는 시간 (ms).")]
        [SerializeField] private float ringMs = 450f;
        [Tooltip("처음 고리 두께 (px). 퍼지며 가늘어진다.")]
        [SerializeField] private float ringWidth = 14f;

        public override float DurationMs => Mathf.Max(BoomMs, bloomMs, ringMs);

        private void Awake()
        {
            if (glyph == null || streakColors == null || streakColors.Length == 0)
                Debug.LogError($"[{nameof(TargetBoomFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            FxLayer layer = context.Layer;
            Vector2 center = layer.CenterOf(target);
            Vector2 offset = new Vector2(Random.Range(offsetX.x, offsetX.y), Random.Range(offsetY.x, offsetY.y)) * layer.PixelScale;
            layer.Boom(center + offset, boomColor, glyph);
            layer.Streaks(center, streakCount, streakColors);
            layer.Bloom(center, bloomColor, bloomSize, Seconds(bloomMs));
            layer.Ring(center, ringColor, ringRadius, Seconds(ringMs), ringWidth);
        }
    }
}
