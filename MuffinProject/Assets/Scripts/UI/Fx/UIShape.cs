using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 면 + 테두리를 한 메시로 그리는 UI 도형 — 둥근 사각형 · 타원. 면 색 = Graphic.color.
    /// 테두리가 면 밑에 깔린 판이 아니라 면 바깥 띠라서, CanvasGroup 으로 흐리게 해도 테두리가 면 너머로 비치지 않는다 (CSS 의 opacity 와 같게 보인다).
    /// 바깥 가장자리는 feather 폭만큼 투명해진다 — 1 이면 계단 없는 가장자리, 크게 하면 흐린 그림자. 테두리는 점선으로도 그린다 (dash > 0, 틈에는 면 색).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다 — 없으면 아무것도 그려지지 않는다
    public class UIShape : MaskableGraphic
    {
        public enum ShapeKind { RoundRect, Ellipse }

        [Tooltip("모양 — 둥근 사각형 또는 타원.")]
        [SerializeField] private ShapeKind shape = ShapeKind.RoundRect;
        [Tooltip("둥근 사각형의 모서리 반지름 (타원에는 쓰지 않는다).")]
        [SerializeField] private float radius = 16f;
        [Tooltip("테두리 두께 — 0 이면 테두리 없음.")]
        [SerializeField] private float borderWidth;
        [Tooltip("테두리 색 — 점선이면 칸의 색 (틈은 면 색).")]
        [SerializeField] private Color borderColor =new Color32(0x2a, 0x24, 0x20, 0xFF);
        [Tooltip("점선 한 칸 길이 — 0 이면 실선.")]
        [SerializeField] private float dash;
        [Tooltip("점선 사이 틈 길이.")]
        [SerializeField] private float gap;
        [Tooltip("바깥 가장자리가 투명해지는 폭 (가장자리를 가운데로 안팎 절반씩) — 1 이면 매끈한 가장자리, 크게 하면 흐린 그림자.")]
        [SerializeField] private float feather = 1f;

        private readonly List<Vector2> _a = new List<Vector2>();
        private readonly List<Vector2> _b = new List<Vector2>();
        private readonly List<Vector2> _c = new List<Vector2>();
        private readonly List<Vector2> _d = new List<Vector2>();

        /// <summary>테 색 — 실행 중 바꿀 때 (예: 기록 칩 작은 카드의 종류 색). 바뀔 때만 다시 그린다.</summary>
        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor == value)
                    return;
                borderColor = value;
                SetVerticesDirty();
            }
        }

        /// <summary>코드에서 모양을 정할 때 (빌더) — 값을 넣고 다시 그린다.</summary>
        public void Set(ShapeKind newShape, float newRadius, float newBorderWidth, Color newBorderColor, float newFeather = 1f, float newDash = 0f, float newGap = 0f)
        {
            shape = newShape;
            radius = newRadius;
            borderWidth = newBorderWidth;
            borderColor = newBorderColor;
            feather = newFeather;
            dash = newDash;
            gap = newGap;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float half = Mathf.Max(feather, 0.01f) / 2f;
            bool bordered = borderWidth > 0f;
            bool dashed = bordered && dash > 0f;
            Color32 fill = color;
            Color32 edge = bordered && !dashed ? (Color32)borderColor : fill;

            // 바깥 번짐
            Contour(rect, -half, dashed, _a);
            Contour(rect, half, dashed, _b);
            if (!bordered)
            {
                Strip(vh, _a, Transparent(fill), _b, fill);
                Fan(vh, rect.center, _b, fill);
                return;
            }

            // 테 안쪽 가장자리 (안쪽 번짐 0.5 의 끝) — 실선이면 면이 여기서 시작, 점선이면 칸이 여기까지
            float innerEdge = borderWidth + 0.5f;
            Contour(rect, Mathf.Max(borderWidth - 0.5f, half), dashed, _c);
            Contour(rect, innerEdge, dashed, _d);
            if (!dashed)
            {
                Strip(vh, _a, Transparent(edge), _b, edge);
                Strip(vh, _b, edge, _c, edge);
                Strip(vh, _c, edge, _d, fill);
                Fan(vh, rect.center, _d, fill);
                return;
            }

            // 점선 — 면은 틈에 보이도록 바깥 끝까지, 바깥 번짐 · 띠 · 안쪽 번짐을 칸마다
            Strip(vh, _a, Transparent(fill), _b, fill);
            Fan(vh, rect.center, _b, fill);
            Color32 ink = borderColor;
            // 둘레가 주기로 나눠 떨어지게 맞춘다 — 윤곽이 시작하는 3시 자리에서 마지막 틈이 잘리지 않게
            float total = 0f;
            for (int i = 0; i < _b.Count; i++)
                total += MidLength(i);
            float period = total / Mathf.Max(1, Mathf.RoundToInt(total / (dash + Mathf.Max(gap, 0f))));
            float along = 0f;
            for (int i = 0; i < _b.Count; i++)
            {
                int next = (i + 1) % _b.Count;
                float length = MidLength(i);
                bool on = (along + length / 2f) % period < dash;
                along += length;
                if (!on)
                    continue;
                Quad(vh, _a[i], _a[next], Transparent(ink), _b[next], _b[i], ink);
                Quad(vh, _b[i], _b[next], ink, _c[next], _c[i], ink);
                Quad(vh, _c[i], _c[next], ink, _d[next], _d[i], Transparent(ink));
            }
        }

        // 바깥에서 inset 만큼 안쪽 윤곽 (음수면 바깥). 안쪽 띠끼리 꼭짓점이 맞도록 inset 과 무관하게 같은 개수 · 같은 자리 순서로 낸다.
        private void Contour(Rect rect, float inset, bool fine, List<Vector2> points)
        {
            points.Clear();
            if (shape == ShapeKind.Ellipse)
            {
                float a = Mathf.Max(rect.width / 2f - inset, 0f);
                float b = Mathf.Max(rect.height / 2f - inset, 0f);
                int count = EllipseSegments(rect, fine);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    points.Add(rect.center + new Vector2(Mathf.Cos(angle) * a, Mathf.Sin(angle) * b));
                }
                return;
            }

            float outerRadius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) / 2f);
            float xMin = Mathf.Min(rect.xMin + inset, rect.center.x), xMax = Mathf.Max(rect.xMax - inset, rect.center.x);
            float yMin = Mathf.Min(rect.yMin + inset, rect.center.y), yMax = Mathf.Max(rect.yMax - inset, rect.center.y);
            float r = Mathf.Clamp(outerRadius - inset, 0f, Mathf.Min(xMax - xMin, yMax - yMin) / 2f);
            int arc = Mathf.Clamp(Mathf.CeilToInt(outerRadius / 2f), 4, 16);
            // 곧은 변을 나누는 수 — 점선일 때만 (바깥 윤곽 길이 기준, 4px 마다)
            int across = fine ? Mathf.Max(1, Mathf.CeilToInt((rect.width - outerRadius * 2f) / 4f)) : 1;
            int down = fine ? Mathf.Max(1, Mathf.CeilToInt((rect.height - outerRadius * 2f) / 4f)) : 1;

            var corners = new[]
            {
                new Vector2(xMax - r, yMax - r), new Vector2(xMin + r, yMax - r),
                new Vector2(xMin + r, yMin + r), new Vector2(xMax - r, yMin + r),
            };
            for (int k = 0; k < 4; k++)
            {
                for (int s = 0; s <= arc; s++)
                {
                    float angle = (k * 90f + s * 90f / arc) * Mathf.Deg2Rad;
                    points.Add(corners[k] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
                // 다음 모서리 시작까지의 변 (위 · 왼쪽 · 아래 · 오른쪽)
                Vector2 from = points[points.Count - 1];
                Vector2 to = corners[(k + 1) % 4] + new Vector2(Mathf.Cos((k + 1) * Mathf.PI / 2f), Mathf.Sin((k + 1) * Mathf.PI / 2f)) * r;
                int pieces = k % 2 == 0 ? across : down;
                for (int j = 1; j < pieces; j++)
                    points.Add(Vector2.Lerp(from, to, (float)j / pieces));
            }
        }

        // 테 가운데 선에서 i 번째 꼭짓점부터 다음 꼭짓점까지의 길이 (점선 칸을 재는 선)
        private float MidLength(int i)
        {
            int next = (i + 1) % _b.Count;
            return Vector2.Distance((_b[i] + _c[i]) / 2f, (_b[next] + _c[next]) / 2f);
        }

        private int EllipseSegments(Rect rect, bool fine)
        {
            float a = rect.width / 2f, b = rect.height / 2f;
            float perimeter = Mathf.PI * (3f * (a + b) - Mathf.Sqrt((3f * a + b) * (a + 3f * b)));
            return Mathf.Clamp(Mathf.CeilToInt(perimeter / (fine ? 3f : 6f)), 48, 512);
        }

        private static Color32 Transparent(Color32 c) => new Color32(c.r, c.g, c.b, 0);

        private static void Strip(VertexHelper vh, List<Vector2> outer, Color32 outerColor, List<Vector2> inner, Color32 innerColor)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < outer.Count; i++)
            {
                vh.AddVert(outer[i], outerColor, Vector4.zero);
                vh.AddVert(inner[i], innerColor, Vector4.zero);
            }
            for (int i = 0; i < outer.Count; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % outer.Count * 2;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(b, b + 1, a + 1);
            }
        }

        private static void Fan(VertexHelper vh, Vector2 center, List<Vector2> rim, Color32 c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, c, Vector4.zero);
            for (int i = 0; i < rim.Count; i++)
                vh.AddVert(rim[i], c, Vector4.zero);
            for (int i = 0; i < rim.Count; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % rim.Count);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color32 abColor, Vector2 c, Vector2 d, Color32 cdColor)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, abColor, Vector4.zero);
            vh.AddVert(b, abColor, Vector4.zero);
            vh.AddVert(c, cdColor, Vector4.zero);
            vh.AddVert(d, cdColor, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
