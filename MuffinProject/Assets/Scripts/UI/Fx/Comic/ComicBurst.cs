using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 터짐 말풍선 — 뾰족한 별 모양 바탕 + 먹 테 (시안 burst(x, y, rx, ry, n, seed, fill, {rot, lw, stroke})).
    /// 자리 · 기울기(rot) = 이 RectTransform. 크기는 <see cref="SetRadius"/> (테 두께는 크기를 따라 커지지 않는다 — 시안과 같다).
    /// 투명도 = Graphic.color.a.
    /// </summary>
    public class ComicBurst : ComicGraphic
    {
        // 시안: 바깥 꼭짓점 반지름 1 ~ 1.18, 안쪽 꼭짓점 0.62 ~ 0.74
        private const float OuterMin = 1f;
        private const float OuterRange = 0.18f;
        private const float InnerMin = 0.62f;
        private const float InnerRange = 0.12f;

        [Tooltip("가로 · 세로 반지름 (설계 px, 시안 rx · ry).")]
        [SerializeField] private Vector2 radius = new Vector2(270f, 160f);
        [Tooltip("뾰족한 꼭짓점 수 (시안 n).")]
        [SerializeField] private int points = 13;
        [Tooltip("씨앗 — 꼭짓점 길이가 들쭉날쭉한 모양 (시안 seed).")]
        [SerializeField] private int seed = 7;
        [Tooltip("바탕 색 (시안 fill).")]
        [SerializeField] private Color fill = Color.white;
        [Tooltip("테 색 (시안 stroke).")]
        [SerializeField] private Color stroke = ComicKit.Ink;
        [Tooltip("테 두께 (설계 px, 시안 lw).")]
        [SerializeField] private float lineWidth = 7f;

        private Vector2 _scale = Vector2.one;

        /// <summary>프리팹에 적은 반지름에 곱하는 배율 (시안에서 rx · ry 를 키우는 값, 예: back(k)). 바뀌면 다시 그린다.</summary>
        public void SetRadius(float scaleX, float scaleY)
        {
            var scale = new Vector2(scaleX, scaleY);
            if (scale == _scale)
                return;
            _scale = scale;
            Redraw();
        }

        protected override void Paint(ComicPen pen)
        {
            float rx = radius.x * _scale.x, ry = radius.y * _scale.y;
            if (rx <= 0f || ry <= 0f)
                return;
            var random = new ComicRandom(seed);
            int n = points * 2;
            pen.BeginPath();
            for (int i = 0; i < n; i++)
            {
                float angle = (float)i / n * ComicKit.Tau;
                float k = i % 2 == 1 ? InnerMin + random.Next() * InnerRange : OuterMin + random.Next() * OuterRange;
                pen.LineTo(Mathf.Cos(angle) * rx * k, Mathf.Sin(angle) * ry * k);
            }
            pen.ClosePath();
            pen.Fill(fill);
            pen.Stroke(stroke, lineWidth);
        }
    }
}
