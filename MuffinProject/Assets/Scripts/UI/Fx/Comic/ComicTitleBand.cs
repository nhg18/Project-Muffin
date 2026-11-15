using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 제목 뒤를 붓질처럼 쓱 지나가는 색 띠 — 진한 색 띠 위에 밝은 색 띠 + 먹 테 (시안 titleBlock 의 band).
    /// 띠 왼쪽 끝 = 이 RectTransform 의 피벗 (시안 x0 − 120, y0 + 14, 기울기 −0.05). 길이 · 뻗은 정도는 <see cref="ComicTitleBlock"/> 가 정한다.
    /// </summary>
    public class ComicTitleBand : ComicGraphic
    {
        // 시안 band(dx, dy): (dx, −56+dy) → ((L+150)·bw+dx, −70+dy) → ((L+110)·bw+dx, 52+dy) → (dx−10, 46+dy)
        private const float TopLeft = -56f;
        private const float TopRight = -70f;
        private const float BottomRight = 52f;
        private const float BottomLeft = 46f;
        private const float TopReach = 150f;
        private const float BottomReach = 110f;
        private const float BottomLeftInset = -10f;
        private static readonly Vector2 DeepOffset = new Vector2(10f, 12f);

        [Tooltip("띠 길이 — 제목 그림 너비 × 0.82 + 70 (설계 px, 시안 L).")]
        [SerializeField] private float length = 300f;
        [Tooltip("뻗은 정도 (0 ~ 1, 시안 bw).")]
        [SerializeField] private float progress = 1f;
        [Tooltip("밝은 띠 색 (장면 accent).")]
        [SerializeField] private Color accent = new Color32(0xff, 0x8a, 0x1f, 0xff);
        [Tooltip("밑에 깔린 진한 띠 색 (장면 deep).")]
        [SerializeField] private Color deep = new Color32(0x8a, 0x32, 0x00, 0xff);
        [Tooltip("먹 테 두께 (설계 px, 시안 6).")]
        [SerializeField] private float lineWidth = 6f;

        /// <summary>띠 길이 (설계 px, 시안 L).</summary>
        public float Length
        {
            get => length;
            set => SetValue(ref length, value);
        }

        /// <summary>뻗은 정도 (0 ~ 1, 시안 bw = eo(τ/0.22)).</summary>
        public float Progress
        {
            get => progress;
            set => SetValue(ref progress, value);
        }

        /// <summary>띠 색 (밝은 색 · 진한 색).</summary>
        public void SetColors(Color newAccent, Color newDeep)
        {
            accent = newAccent;
            deep = newDeep;
            Redraw();
        }

        protected override void Paint(ComicPen pen)
        {
            if (progress <= 0f)
                return;
            Band(pen, DeepOffset.x, DeepOffset.y, deep);
            Band(pen, 0f, 0f, accent);
            pen.Stroke(ComicKit.Ink, lineWidth);
        }

        private void Band(ComicPen pen, float dx, float dy, Color c)
        {
            pen.BeginPath();
            pen.MoveTo(dx, TopLeft + dy);
            pen.LineTo((length + TopReach) * progress + dx, TopRight + dy);
            pen.LineTo((length + BottomReach) * progress + dx, BottomRight + dy);
            pen.LineTo(dx + BottomLeftInset, BottomLeft + dy);
            pen.ClosePath();
            pen.Fill(c);
        }

        private void SetValue(ref float field, float value)
        {
            if (Mathf.Approximately(field, value))
                return;
            field = value;
            Redraw();
        }
    }
}
