using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 반짝이 — 가는 네 갈래 별 (시안 sparkle(x, y, r, rot, a, col)). 가운데 = 이 RectTransform 의 피벗, 기울기(rot) = 이 RectTransform.
    /// 색 · 투명도(시안 col · a) = Graphic.color. 크기는 <see cref="Radius"/>.
    /// </summary>
    public class ComicSparkle : ComicGraphic
    {
        // 시안: 꼭짓점 8 개 — 바깥 r, 안쪽 r × 0.18
        private const int Corners = 8;
        private const float InnerRatio = 0.18f;

        [Tooltip("반지름 (설계 px, 시안 r).")]
        [SerializeField] private float radius = 30f;

        /// <summary>반지름 (설계 px). 바뀌면 다시 그린다.</summary>
        public float Radius
        {
            get => radius;
            set
            {
                if (Mathf.Approximately(radius, value))
                    return;
                radius = value;
                Redraw();
            }
        }

        protected override void Paint(ComicPen pen)
        {
            if (radius <= 0f)
                return;
            pen.BeginPath();
            for (int i = 0; i < Corners; i++)
            {
                float angle = (float)i / Corners * ComicKit.Tau;
                float r = i % 2 == 1 ? radius * InnerRatio : radius;
                pen.LineTo(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }
            pen.ClosePath();
            pen.Fill(Color.white);
        }
    }
}
