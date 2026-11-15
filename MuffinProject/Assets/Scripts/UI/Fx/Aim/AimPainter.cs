using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 매 프레임 지우고 다시 그리는 붓 — 시안 canvas 의 그리기(선 · 곡선 · 둥근 빛 · 면 · 그라데이션)를 메시로 옮긴다.
    /// 좌표는 시안 px · 아래로 + (이 그림 칸의 왼쪽 위가 0). 회전은 시계 방향 +. 섞는 방식은 머티리얼로 고른다 —
    /// 더하기(시안 globalCompositeOperation "lighter")는 FxAdditive.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다
    public class AimPainter : MaskableGraphic
    {
        private const float Tau = Mathf.PI * 2f;
        private const float MiterLimit = 4f;

        [Tooltip("둥근 끝 · 둥근 빛의 매끄러움 — 화면 몇 단위마다 한 마디를 둘지.")]
        [SerializeField] private float segmentLength = 6f;

        private readonly List<UIVertex> _vertices = new List<UIVertex>();
        private readonly List<int> _indices = new List<int>();
        private readonly Stack<State> _stack = new Stack<State>();
        private readonly List<Vector2> _clean = new List<Vector2>();
        private readonly List<Color> _cleanColors = new List<Color>();
        private AimAffine _matrix = AimAffine.Identity;
        private float _alpha = 1f;
        private Vector2 _origin;

        private struct State
        {
            public AimAffine Matrix;
            public float Alpha;
        }

        /// <summary>시안 globalAlpha — 이후 그리는 모든 것에 곱한다.</summary>
        public float Alpha
        {
            get => _alpha;
            set => _alpha = value;
        }

        /// <summary>그린 것을 모두 지우고 변환 · 알파를 처음으로.</summary>
        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
            _stack.Clear();
            _matrix = AimAffine.Identity;
            _alpha = 1f;
            Rect rect = rectTransform.rect;
            _origin = new Vector2(rect.xMin, rect.yMax);
            SetVerticesDirty();
        }

        #region 변환

        /// <summary>canvas save — 변환 · 알파.</summary>
        public void Save()
        {
            _stack.Push(new State { Matrix = _matrix, Alpha = _alpha });
        }

        /// <summary>canvas restore.</summary>
        public void Restore()
        {
            if (_stack.Count == 0)
                return;
            State state = _stack.Pop();
            _matrix = state.Matrix;
            _alpha = state.Alpha;
        }

        /// <summary>canvas translate.</summary>
        public void Translate(float x, float y)
        {
            _matrix.Translate(x, y);
        }

        /// <summary>canvas scale.</summary>
        public void Scale(float x, float y)
        {
            _matrix.Scale(x, y);
        }

        /// <summary>canvas rotate (라디안, 시계 방향 +).</summary>
        public void Rotate(float radians)
        {
            _matrix.Rotate(radians);
        }

        #endregion

        #region 선

        /// <summary>곧은 선 하나. roundCap 이면 두 끝이 둥글다.</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color, bool roundCap)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            Color32 c = ToVertexColor(color);
            if (length < 1e-4f)
            {
                if (roundCap)
                    Ellipse(a, width * 0.5f, width * 0.5f, 0f, color);
                return;
            }
            Vector2 dir = d / length;
            Vector2 normal = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            int i0 = AddVertex(a + normal, c);
            int i1 = AddVertex(b + normal, c);
            int i2 = AddVertex(b - normal, c);
            int i3 = AddVertex(a - normal, c);
            AddQuad(i0, i1, i2, i3);
            if (!roundCap)
                return;
            AddCap(a, -dir, width * 0.5f, c);
            AddCap(b, dir, width * 0.5f, c);
        }

        /// <summary>꺾은선 (뾰족 이음). closed 면 끝과 처음을 잇는다.</summary>
        public void Stroke(List<Vector2> points, float width, Color color, bool roundCap, bool closed = false)
        {
            StrokeCore(points, null, color, width, roundCap, closed);
        }

        /// <summary>점마다 색이 다른 꺾은선 — 선을 따라 가는 그라데이션.</summary>
        public void Stroke(List<Vector2> points, List<Color> colors, float width, bool roundCap)
        {
            StrokeCore(points, colors, Color.clear, width, roundCap, false);
        }

        #endregion

        #region 면

        /// <summary>속이 찬 타원 (rotation 라디안).</summary>
        public void Ellipse(Vector2 center, float rx, float ry, float rotation, Color color)
        {
            if (rx <= 0f || ry <= 0f)
                return;
            Color32 c = ToVertexColor(color);
            int n = SegmentsFor(Mathf.Max(rx, ry), Tau, 12);
            int middle = AddVertex(center, c);
            int first = -1;
            int previous = -1;
            float cr = Mathf.Cos(rotation);
            float sr = Mathf.Sin(rotation);
            for (int i = 0; i < n; i++)
            {
                float a = i * Tau / n;
                float ex = Mathf.Cos(a) * rx;
                float ey = Mathf.Sin(a) * ry;
                int index = AddVertex(center + new Vector2(ex * cr - ey * sr, ex * sr + ey * cr), c);
                if (previous >= 0)
                    AddTriangle(middle, previous, index);
                else
                    first = index;
                previous = index;
            }
            AddTriangle(middle, previous, first);
        }

        /// <summary>시안 glow — 가운데 color(알파 포함)에서 반지름 r 끝에서 투명해지는 둥근 빛.</summary>
        public void Glow(Vector2 center, float radius, Color color)
        {
            if (radius <= 0f || color.a <= 0f)
                return;
            Color32 inner = ToVertexColor(color);
            Color32 outer = ToVertexColor(new Color(color.r, color.g, color.b, 0f));
            int n = SegmentsFor(radius, Tau, 16);
            int middle = AddVertex(center, inner);
            int first = -1;
            int previous = -1;
            for (int i = 0; i < n; i++)
            {
                float a = i * Tau / n;
                int index = AddVertex(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, outer);
                if (previous >= 0)
                    AddTriangle(middle, previous, index);
                else
                    first = index;
                previous = index;
            }
            AddTriangle(middle, previous, first);
        }

        /// <summary>볼록한(또는 가운데에서 다 보이는) 다각형을 무게중심 부채꼴로 채운다.</summary>
        public void Polygon(List<Vector2> points, Color color)
        {
            int n = points.Count;
            if (n < 3)
                return;
            Color32 c = ToVertexColor(color);
            Vector2 center = Vector2.zero;
            foreach (Vector2 p in points)
                center += p;
            int middle = AddVertex(center / n, c);
            int first = AddVertex(points[0], c);
            int previous = first;
            for (int i = 1; i < n; i++)
            {
                int index = AddVertex(points[i], c);
                AddTriangle(middle, previous, index);
                previous = index;
            }
            AddTriangle(middle, previous, first);
        }

        /// <summary>네 꼭짓점 · 네 색 사각형 (a → b → c → d 둘레 순서).</summary>
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            AddQuad(AddVertex(a, ToVertexColor(ca)), AddVertex(b, ToVertexColor(cb)), AddVertex(c, ToVertexColor(cc)), AddVertex(d, ToVertexColor(cd)));
        }

        /// <summary>삼각형 하나.</summary>
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            Color32 c32 = ToVertexColor(color);
            AddTriangle(AddVertex(a, c32), AddVertex(b, c32), AddVertex(c, c32));
        }

        /// <summary>canvas fillRect.</summary>
        public void Rect(float x, float y, float w, float h, Color color)
        {
            Quad(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h), color, color, color, color);
        }

        /// <summary>
        /// 시안 createRadialGradient(가운데, r0 → r1) 로 큰 사각형을 덮은 것 — r0 안은 inner, r1 밖은 outer.
        /// cover 는 덮을 반지름 (사각형 모서리까지).
        /// </summary>
        public void Radial(Vector2 center, float r0, float r1, Color inner, Color outer, float cover)
        {
            if (inner.a > 0f)
                Ring(center, 0f, r0, inner, inner);
            Ring(center, r0, r1, inner, outer);
            if (cover > r1 && outer.a > 0f)
                Ring(center, r1, cover, outer, outer);
        }

        /// <summary>
        /// 시안 createRadialGradient(c0, r0, c1, r1) 로 원(c1, r1)을 채운 것 — 빛이 한쪽으로 치우친 공.
        /// offsets · colors 는 그라데이션 멈춤.
        /// </summary>
        public void RadialDisc(Vector2 c0, float r0, Vector2 c1, float r1, float[] offsets, Color[] colors)
        {
            int n = SegmentsFor(r1, Tau, 24);
            Ellipse(c0, r0, r0, 0f, colors[0]);
            const int stepsPerStop = 3;
            float previousT = 0f;
            Color previousColor = colors[0];
            for (int s = 1; s < offsets.Length; s++)
            {
                for (int k = 1; k <= stepsPerStop; k++)
                {
                    float t = Mathf.Lerp(offsets[s - 1], offsets[s], k / (float)stepsPerStop);
                    Color color = Color.Lerp(colors[s - 1], colors[s], k / (float)stepsPerStop);
                    Band(Vector2.Lerp(c0, c1, previousT), Mathf.Lerp(r0, r1, previousT), Vector2.Lerp(c0, c1, t), Mathf.Lerp(r0, r1, t), previousColor, color, n);
                    previousT = t;
                    previousColor = color;
                }
            }
        }

        #endregion

        #region 점 모으기 (곡선 → 꺾은선)

        /// <summary>2차 베지어를 segments 마디로 나눠 points 에 붙인다 (첫 점 포함).</summary>
        public static void AddQuadratic(List<Vector2> points, Vector2 p0, Vector2 control, Vector2 p1, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float r = 1f - t;
                points.Add(r * r * p0 + 2f * r * t * control + t * t * p1);
            }
        }

        /// <summary>3차 베지어를 segments 마디로 나눠 points 에 붙인다 (첫 점 빼고).</summary>
        public static void AddCubic(List<Vector2> points, Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1, int segments)
        {
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float r = 1f - t;
                points.Add(r * r * r * p0 + 3f * r * r * t * c0 + 3f * r * t * t * c1 + t * t * t * p1);
            }
        }

        /// <summary>canvas ellipse(cx, cy, rx, ry, 0, start, end) 의 둘레 점들 (시계 방향).</summary>
        public static void AddArc(List<Vector2> points, Vector2 center, float rx, float ry, float start, float end, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)segments);
                points.Add(center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
            }
        }

        #endregion

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_vertices.Count > 0)
                vh.AddUIVertexStream(_vertices, _indices);
        }

        #region 내부

        private void StrokeCore(List<Vector2> points, List<Color> colors, Color color, float width, bool roundCap, bool closed)
        {
            // 같은 자리 점은 버린다 (방향을 못 정한다)
            _clean.Clear();
            _cleanColors.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                if (_clean.Count > 0 && (points[i] - _clean[_clean.Count - 1]).sqrMagnitude < 1e-8f)
                    continue;
                _clean.Add(points[i]);
                _cleanColors.Add(colors != null ? colors[i] : color);
            }
            if (closed && _clean.Count > 2 && (_clean[0] - _clean[_clean.Count - 1]).sqrMagnitude < 1e-8f)
            {
                _clean.RemoveAt(_clean.Count - 1);
                _cleanColors.RemoveAt(_cleanColors.Count - 1);
            }
            int n = _clean.Count;
            if (n < 2)
                return;

            float half = width * 0.5f;
            int start = _vertices.Count;
            for (int i = 0; i < n; i++)
            {
                bool hasPrevious = i > 0 || closed;
                bool hasNext = i < n - 1 || closed;
                Vector2 p = _clean[i];
                Vector2 dirNext = hasNext ? (_clean[(i + 1) % n] - p).normalized : Vector2.zero;
                Vector2 dirPrevious = hasPrevious ? (p - _clean[(i - 1 + n) % n]).normalized : dirNext;
                if (!hasNext)
                    dirNext = dirPrevious;
                Vector2 normalPrevious = new Vector2(-dirPrevious.y, dirPrevious.x);
                Vector2 normalNext = new Vector2(-dirNext.y, dirNext.x);
                Vector2 miter = normalPrevious + normalNext;
                if (miter.sqrMagnitude < 1e-6f)
                    miter = normalNext;
                miter.Normalize();
                float length = half / Mathf.Max(Vector2.Dot(miter, normalNext), 1f / MiterLimit);
                Color32 c = ToVertexColor(_cleanColors[i]);
                AddVertex(p + miter * length, c);
                AddVertex(p - miter * length, c);
            }
            int last = closed ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % n) * 2;
                AddQuad(a, b, b + 1, a + 1);
            }
            if (!roundCap || closed)
                return;
            AddCap(_clean[0], (_clean[0] - _clean[1]).normalized, half, ToVertexColor(_cleanColors[0]));
            AddCap(_clean[n - 1], (_clean[n - 1] - _clean[n - 2]).normalized, half, ToVertexColor(_cleanColors[n - 1]));
        }

        // p 에서 dir 쪽으로 반원
        private void AddCap(Vector2 p, Vector2 dir, float radius, Color32 c)
        {
            int n = Mathf.Max(2, SegmentsFor(radius, Mathf.PI, 2));
            Vector2 normal = new Vector2(-dir.y, dir.x);
            int middle = AddVertex(p, c);
            int previous = -1;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * i / n;
                int index = AddVertex(p + (normal * Mathf.Cos(a) + dir * Mathf.Sin(a)) * radius, c);
                if (previous >= 0)
                    AddTriangle(middle, previous, index);
                previous = index;
            }
        }

        // 가운데가 같은 두 원 사이 띠
        private void Ring(Vector2 center, float r0, float r1, Color c0, Color c1)
        {
            Band(center, r0, center, r1, c0, c1, SegmentsFor(r1, Tau, 24));
        }

        // 원 (c0, r0) 과 원 (c1, r1) 사이 띠 — 같은 각도끼리 잇는다
        private void Band(Vector2 c0, float r0, Vector2 c1, float r1, Color color0, Color color1, int n)
        {
            Color32 a32 = ToVertexColor(color0);
            Color32 b32 = ToVertexColor(color1);
            int start = _vertices.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * Tau / n;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                AddVertex(c0 + dir * r0, a32);
                AddVertex(c1 + dir * r1, b32);
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % n) * 2;
                AddQuad(a, b, b + 1, a + 1);
            }
        }

        private int SegmentsFor(float radius, float sweep, int minimum)
        {
            float screen = radius * _matrix.LineScale;
            int n = Mathf.CeilToInt(sweep * screen / Mathf.Max(1f, segmentLength));
            return Mathf.Clamp(n, minimum, 96);
        }

        private Color32 ToVertexColor(Color color)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * _alpha));
        }

        private int AddVertex(Vector2 p, Color32 color)
        {
            Vector2 q = _matrix.Apply(p);
            UIVertex v = UIVertex.simpleVert;
            v.position = new Vector3(_origin.x + q.x, _origin.y - q.y, 0f);
            v.color = color;
            _vertices.Add(v);
            return _vertices.Count - 1;
        }

        private void AddTriangle(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        private void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        #endregion
    }
}
