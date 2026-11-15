using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 캔버스 2D 처럼 경로를 그려 UI 메시로 내는 펜. 시안 CI2 의 그리기 코드를 줄 단위로 옮길 수 있게 이름 · 순서를 캔버스와 맞췄다
    /// (MoveTo · LineTo · BezierCurveTo · Arc · Ellipse · ClosePath · Fill · Stroke · Save · Restore · Translate · Rotate · Scale · Alpha).
    /// 좌표는 시안처럼 아래로 + (y down), 각도는 라디안 · 시계 방향 +. 원점 = 그리는 Graphic 의 피벗. 메시로 낼 때 y 를 뒤집는다.
    /// 경로 점은 넣는 순간의 변환을 따르고, 선 두께는 Stroke 하는 순간의 배율을 따른다 (캔버스와 같다). 선 이음 · 끝은 늘 둥글다 (시안 lineJoin · lineCap round).
    /// 없는 것: 글자(TMP 를 쓴다) · 그림 잘라 붙이기(<see cref="FillImage"/> 만) · 구멍 난 경로(하위 경로를 따로 채운다) · 합성 모드(더하기는 FxAdditive 머티리얼을 쓴 다른 Graphic).
    /// </summary>
    public sealed class ComicPen
    {
        // 곡선 · 호를 이 길이(설계 px)마다 나눈다
        private const float CurveStep = 3f;
        private const int MaxCurveSegments = 256;
        // 이음 각의 반 cos 이 이보다 작으면(많이 꺾이면) 뾰족이 대신 둥근 이음
        private const float MiterLimitCos = 0.6f;
        private const float Epsilon = 1e-4f;

        private readonly List<Subpath> _paths = new List<Subpath>();
        private readonly Stack<State> _stack = new Stack<State>();
        private readonly List<Vector2> _clean = new List<Vector2>();
        private readonly List<int> _indices = new List<int>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Vector2> _pieceA = new List<Vector2>();
        private readonly List<Vector2> _pieceB = new List<Vector2>();
        private readonly List<Vector2> _pieceC = new List<Vector2>();
        private readonly List<Vector2> _left = new List<Vector2>();
        private readonly List<Vector2> _right = new List<Vector2>();
        private int _pathCount;
        private Affine _matrix = Affine.Identity;
        private VertexHelper _vh;
        private Color _tint = Color.white;

        /// <summary>지금 그리는 것의 투명도 배율 (시안 globalAlpha). <see cref="Save"/> · <see cref="Restore"/> 에 같이 들어간다.</summary>
        public float Alpha { get; set; } = 1f;

        /// <summary>지금 변환의 배율 (선 두께 · 곡선 나누기에 쓴다).</summary>
        public float CurrentScale => _matrix.Scale;

        private struct State
        {
            public Affine Matrix;
            public float Alpha;
        }

        private sealed class Subpath
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public bool Closed;
        }

        // 캔버스 변환 (a, b, c, d, e, f): x' = a·x + c·y + e, y' = b·x + d·y + f
        private struct Affine
        {
            public float A, B, C, D, E, F;

            public static Affine Identity => new Affine { A = 1f, D = 1f };

            public float Scale => Mathf.Sqrt(Mathf.Abs(A * D - B * C));

            public Vector2 Apply(float x, float y) => new Vector2(A * x + C * y + E, B * x + D * y + F);

            // m 을 먼저, 그다음 이것 — 캔버스 transform() 은 지금 변환 뒤에 곱한다
            public Affine Then(Affine m)
            {
                return new Affine
                {
                    A = A * m.A + C * m.B,
                    B = B * m.A + D * m.B,
                    C = A * m.C + C * m.D,
                    D = B * m.C + D * m.D,
                    E = A * m.E + C * m.F + E,
                    F = B * m.E + D * m.F + F,
                };
            }

            public bool TryInvert(out Affine inverse)
            {
                float det = A * D - B * C;
                inverse = Identity;
                if (Mathf.Abs(det) < 1e-12f)
                    return false;
                float k = 1f / det;
                inverse = new Affine
                {
                    A = D * k,
                    B = -B * k,
                    C = -C * k,
                    D = A * k,
                    E = (C * F - D * E) * k,
                    F = (B * E - A * F) * k,
                };
                return true;
            }
        }

        /// <summary>그리기 시작 — Graphic 의 OnPopulateMesh 가 부른다. tint 는 모든 색에 곱한다 (Graphic.color).</summary>
        public void Begin(VertexHelper vh, Color tint)
        {
            _vh = vh;
            _tint = tint;
            _matrix = Affine.Identity;
            Alpha = 1f;
            _stack.Clear();
            BeginPath();
        }

        /// <summary>그리기 끝.</summary>
        public void End()
        {
            _vh = null;
        }

        #region 변환

        /// <summary>변환 · 투명도를 쌓아 둔다 (시안 save).</summary>
        public void Save()
        {
            _stack.Push(new State { Matrix = _matrix, Alpha = Alpha });
        }

        /// <summary>쌓아 둔 변환 · 투명도로 돌아간다 (시안 restore).</summary>
        public void Restore()
        {
            if (_stack.Count == 0)
                return;
            State state = _stack.Pop();
            _matrix = state.Matrix;
            Alpha = state.Alpha;
        }

        public void Translate(float x, float y)
        {
            Transform(1f, 0f, 0f, 1f, x, y);
        }

        /// <summary>돌린다 (라디안, 시계 방향 +).</summary>
        public void Rotate(float radians)
        {
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            Transform(cos, sin, -sin, cos, 0f, 0f);
        }

        public void Scale(float x, float y)
        {
            Transform(x, 0f, 0f, y, 0f, 0f);
        }

        /// <summary>시안 transform(a, b, c, d, e, f) — 지금 변환 뒤에 곱한다 (기울이기: Transform(1, 0, k, 1, 0, 0)).</summary>
        public void Transform(float a, float b, float c, float d, float e, float f)
        {
            _matrix = _matrix.Then(new Affine { A = a, B = b, C = c, D = d, E = e, F = f });
        }

        /// <summary>변환을 처음으로 (시안 setTransform(1, 0, 0, 1, 0, 0)).</summary>
        public void ResetTransform()
        {
            _matrix = Affine.Identity;
        }

        #endregion

        #region 경로

        /// <summary>경로를 비운다 (시안 beginPath).</summary>
        public void BeginPath()
        {
            _pathCount = 0;
        }

        public void MoveTo(float x, float y)
        {
            Subpath path = NewSubpath();
            path.Points.Add(_matrix.Apply(x, y));
        }

        public void LineTo(float x, float y)
        {
            Subpath path = OpenSubpath();
            if (path == null)
            {
                MoveTo(x, y);
                return;
            }
            path.Points.Add(_matrix.Apply(x, y));
        }

        public void QuadraticCurveTo(float cx, float cy, float x, float y)
        {
            Subpath path = OpenSubpath();
            if (path == null)
            {
                MoveTo(cx, cy);
                path = OpenSubpath();
            }
            Vector2 p0 = path.Points[path.Points.Count - 1];
            Vector2 p1 = _matrix.Apply(cx, cy), p2 = _matrix.Apply(x, y);
            int n = Segments(Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2));
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n, u = 1f - t;
                path.Points.Add(u * u * p0 + 2f * u * t * p1 + t * t * p2);
            }
        }

        public void BezierCurveTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            Subpath path = OpenSubpath();
            if (path == null)
            {
                MoveTo(c1x, c1y);
                path = OpenSubpath();
            }
            Vector2 p0 = path.Points[path.Points.Count - 1];
            Vector2 p1 = _matrix.Apply(c1x, c1y), p2 = _matrix.Apply(c2x, c2y), p3 = _matrix.Apply(x, y);
            int n = Segments(Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3));
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n, u = 1f - t;
                path.Points.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        /// <summary>원호 (시안 arc) — 지금 경로가 있으면 호의 시작점까지 선으로 잇는다.</summary>
        public void Arc(float x, float y, float radius, float startAngle, float endAngle, bool counterClockwise = false)
        {
            Ellipse(x, y, radius, radius, 0f, startAngle, endAngle, counterClockwise);
        }

        /// <summary>타원호 (시안 ellipse) — 지금 경로가 있으면 호의 시작점까지 선으로 잇는다.</summary>
        public void Ellipse(float x, float y, float radiusX, float radiusY, float rotation, float startAngle, float endAngle, bool counterClockwise = false)
        {
            float sweep = Sweep(startAngle, endAngle, counterClockwise);
            float cosR = Mathf.Cos(rotation), sinR = Mathf.Sin(rotation);
            float length = Mathf.Abs(sweep) * Mathf.Max(radiusX, radiusY) * _matrix.Scale;
            int n = Mathf.Clamp(Mathf.CeilToInt(length / CurveStep), 4, MaxCurveSegments);
            for (int i = 0; i <= n; i++)
            {
                float a = startAngle + sweep * i / n;
                float ex = Mathf.Cos(a) * radiusX, ey = Mathf.Sin(a) * radiusY;
                float px = x + cosR * ex - sinR * ey, py = y + sinR * ex + cosR * ey;
                if (i == 0)
                    LineTo(px, py);
                else
                    OpenSubpath().Points.Add(_matrix.Apply(px, py));
            }
        }

        /// <summary>하위 경로를 닫는다 (시안 closePath). 다음 선은 닫은 경로의 첫 점에서 시작한다.</summary>
        public void ClosePath()
        {
            Subpath path = OpenSubpath();
            if (path == null || path.Points.Count == 0)
                return;
            path.Closed = true;
            Vector2 first = path.Points[0];
            NewSubpath().Points.Add(first);
        }

        /// <summary>사각형 하위 경로 (시안 rect).</summary>
        public void Rect(float x, float y, float width, float height)
        {
            MoveTo(x, y);
            LineTo(x + width, y);
            LineTo(x + width, y + height);
            LineTo(x, y + height);
            ClosePath();
        }

        /// <summary>둥근 사각형 — 시안 rr 처럼 경로를 새로 시작한다.</summary>
        public void RoundRect(float x, float y, float width, float height, float radius)
        {
            BeginPath();
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(Mathf.Abs(width), Mathf.Abs(height)) / 2f);
            float half = Mathf.PI / 2f;
            MoveTo(x + r, y);
            Arc(x + width - r, y + r, r, -half, 0f);
            Arc(x + width - r, y + height - r, r, 0f, half);
            Arc(x + r, y + height - r, r, half, Mathf.PI);
            Arc(x + r, y + r, r, Mathf.PI, Mathf.PI + half);
            ClosePath();
        }

        #endregion

        #region 칠하기

        /// <summary>경로 안을 한 색으로 칠한다 (시안 fill). 하위 경로마다 따로 칠한다.</summary>
        public void Fill(Color color)
        {
            Color32 c = Paint(color);
            for (int i = 0; i < _pathCount; i++)
            {
                if (!Triangulate(_paths[i].Points))
                    continue;
                int start = _vh.currentVertCount;
                foreach (Vector2 p in _clean)
                    AddVert(p, c);
                for (int t = 0; t < _triangles.Count; t += 3)
                    _vh.AddTriangle(start + _triangles[t], start + _triangles[t + 1], start + _triangles[t + 2]);
            }
        }

        /// <summary>
        /// 경로 안을 선형 그라데이션으로 칠한다 (시안 createLinearGradient + fill). (x0, y0) → (x1, y1) 은 지금 변환의 좌표.
        /// stops 는 0 ~ 1 오름차순, colors 와 개수가 같다. 양 끝 밖은 끝 색 그대로다.
        /// </summary>
        public void FillLinear(float x0, float y0, float x1, float y1, float[] stops, Color[] colors)
        {
            Vector2 p0 = _matrix.Apply(x0, y0), p1 = _matrix.Apply(x1, y1);
            Vector2 axis = p1 - p0;
            float length2 = Mathf.Max(axis.sqrMagnitude, Epsilon);
            for (int i = 0; i < _pathCount; i++)
            {
                if (!Triangulate(_paths[i].Points))
                    continue;
                // _clean · _triangles 를 따로 들고 — 조각을 나누는 동안 덮어쓰지 않게
                for (int t = 0; t < _triangles.Count; t += 3)
                {
                    _pieceA.Clear();
                    _pieceA.Add(_clean[_triangles[t]]);
                    _pieceA.Add(_clean[_triangles[t + 1]]);
                    _pieceA.Add(_clean[_triangles[t + 2]]);
                    foreach (float stop in stops)
                    {
                        SplitByLine(_pieceA, p0, axis, length2, stop, _pieceB, _pieceC);
                        EmitGradientPiece(_pieceB, p0, axis, length2, stops, colors);
                        _pieceA.Clear();
                        _pieceA.AddRange(_pieceC);
                    }
                    EmitGradientPiece(_pieceA, p0, axis, length2, stops, colors);
                }
            }
        }

        /// <summary>두 색 선형 그라데이션 (시안 createLinearGradient 에 색 둘).</summary>
        public void FillLinear(float x0, float y0, float x1, float y1, Color from, Color to)
        {
            FillLinear(x0, y0, x1, y1, new[] { 0f, 1f }, new[] { from, to });
        }

        /// <summary>둥근 빛 — (x, y) 에서 inner, 반지름 radius 에서 outer 로 (시안 createRadialGradient(x, y, 0, x, y, r)). 경로와 상관없이 원을 칠한다.</summary>
        public void FillRadial(float x, float y, float radius, Color inner, Color outer)
        {
            Vector2 center = _matrix.Apply(x, y);
            float r = radius * _matrix.Scale;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 2f * r / (CurveStep * 2f)), 16, 96);
            int start = _vh.currentVertCount;
            AddVert(center, Paint(inner));
            Color32 rim = Paint(outer);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                AddVert(_matrix.Apply(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius), rim);
            }
            for (int i = 0; i < n; i++)
                _vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % n);
        }

        /// <summary>
        /// 경로 안에 그림을 깐다 — 시안 clip(경로) + drawImage(im, x, y, w, h). (x, y, w, h) 는 지금 변환의 좌표,
        /// uv 는 그 칸에 들어갈 그림 영역 (Graphic 의 mainTexture 기준, 0 ~ 1). 색은 흰색 × 투명도.
        /// </summary>
        public void FillImage(float x, float y, float width, float height, Rect uv)
        {
            if (!_matrix.TryInvert(out Affine inverse))
                return;
            Color32 c = Paint(Color.white);
            for (int i = 0; i < _pathCount; i++)
            {
                if (!Triangulate(_paths[i].Points))
                    continue;
                int start = _vh.currentVertCount;
                foreach (Vector2 p in _clean)
                {
                    Vector2 local = inverse.Apply(p.x, p.y);
                    float u = uv.xMin + (local.x - x) / width * uv.width;
                    // 그림 위쪽(작은 y) = 텍스처 위쪽(큰 v)
                    float v = uv.yMax - (local.y - y) / height * uv.height;
                    _vh.AddVert(new Vector3(p.x, -p.y), c, new Vector4(u, v, 0f, 0f));
                }
                for (int t = 0; t < _triangles.Count; t += 3)
                    _vh.AddTriangle(start + _triangles[t], start + _triangles[t + 1], start + _triangles[t + 2]);
            }
        }

        /// <summary>경로를 선으로 긋는다 (시안 stroke). 두께는 지금 변환의 배율을 따른다. 이음 · 끝은 둥글다.</summary>
        public void Stroke(Color color, float lineWidth)
        {
            float half = lineWidth * _matrix.Scale / 2f;
            if (half <= 0f)
                return;
            Color32 c = Paint(color);
            for (int i = 0; i < _pathCount; i++)
                StrokeSubpath(_paths[i], half, c);
        }

        #endregion

        #region 안쪽

        private Subpath NewSubpath()
        {
            if (_pathCount == _paths.Count)
                _paths.Add(new Subpath());
            Subpath path = _paths[_pathCount++];
            path.Points.Clear();
            path.Closed = false;
            return path;
        }

        private Subpath OpenSubpath()
        {
            if (_pathCount == 0)
                return null;
            Subpath path = _paths[_pathCount - 1];
            return path.Points.Count == 0 ? null : path;
        }

        private int Segments(float length)
        {
            return Mathf.Clamp(Mathf.CeilToInt(length / CurveStep), 2, MaxCurveSegments);
        }

        private static float Sweep(float start, float end, bool counterClockwise)
        {
            float sweep = end - start;
            if (!counterClockwise)
            {
                if (sweep >= ComicKit.Tau)
                    return ComicKit.Tau;
                sweep %= ComicKit.Tau;
                return sweep < 0f ? sweep + ComicKit.Tau : sweep;
            }
            if (-sweep >= ComicKit.Tau)
                return -ComicKit.Tau;
            sweep %= ComicKit.Tau;
            return sweep > 0f ? sweep - ComicKit.Tau : sweep;
        }

        private Color32 Paint(Color color)
        {
            Color c = color * _tint;
            c.a *= Alpha;
            return c;
        }

        private void AddVert(Vector2 p, Color32 color)
        {
            _vh.AddVert(new Vector3(p.x, -p.y), color, Vector4.zero);
        }

        // 점을 정리해 _clean 에 담고, 삼각형 꼭짓점 번호를 _triangles 에 담는다. 칠할 수 없으면 false
        private bool Triangulate(List<Vector2> points)
        {
            _clean.Clear();
            _triangles.Clear();
            foreach (Vector2 p in points)
            {
                if (_clean.Count == 0 || (p - _clean[_clean.Count - 1]).sqrMagnitude > Epsilon)
                    _clean.Add(p);
            }
            while (_clean.Count > 1 && (_clean[0] - _clean[_clean.Count - 1]).sqrMagnitude <= Epsilon)
                _clean.RemoveAt(_clean.Count - 1);
            int n = _clean.Count;
            if (n < 3)
                return false;

            float area = 0f;
            for (int i = 0; i < n; i++)
                area += Cross(_clean[i], _clean[(i + 1) % n]);
            if (Mathf.Abs(area) < Epsilon)
                return false;
            float sign = Mathf.Sign(area);

            if (IsConvex(sign))
            {
                for (int i = 1; i < n - 1; i++)
                {
                    _triangles.Add(0);
                    _triangles.Add(i);
                    _triangles.Add(i + 1);
                }
                return true;
            }

            // 귀 자르기 — 오목한 경로 (터짐 별 · 땀방울 꼬리 등)
            _indices.Clear();
            for (int i = 0; i < n; i++)
                _indices.Add(i);
            int guard = n * n;
            while (_indices.Count > 3 && guard-- > 0)
            {
                bool cut = false;
                int count = _indices.Count;
                for (int i = 0; i < count; i++)
                {
                    int i0 = _indices[(i + count - 1) % count], i1 = _indices[i], i2 = _indices[(i + 1) % count];
                    Vector2 a = _clean[i0], b = _clean[i1], c = _clean[i2];
                    if (Cross(b - a, c - b) * sign <= 0f)
                        continue;
                    bool blocked = false;
                    for (int j = 0; j < count; j++)
                    {
                        int k = _indices[j];
                        if (k == i0 || k == i1 || k == i2)
                            continue;
                        if (InTriangle(_clean[k], a, b, c, sign))
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (blocked)
                        continue;
                    _triangles.Add(i0);
                    _triangles.Add(i1);
                    _triangles.Add(i2);
                    _indices.RemoveAt(i);
                    cut = true;
                    break;
                }
                // 스스로 꼬인 경로 — 남은 것은 부채꼴로 채운다
                if (!cut)
                    break;
            }
            for (int i = 1; i < _indices.Count - 1; i++)
            {
                _triangles.Add(_indices[0]);
                _triangles.Add(_indices[i]);
                _triangles.Add(_indices[i + 1]);
            }
            return true;
        }

        private bool IsConvex(float sign)
        {
            int n = _clean.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = _clean[i], b = _clean[(i + 1) % n], c = _clean[(i + 2) % n];
                if (Cross(b - a, c - b) * sign < -Epsilon)
                    return false;
            }
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float sign)
        {
            return Cross(b - a, p - a) * sign >= 0f && Cross(c - b, p - b) * sign >= 0f && Cross(a - c, p - c) * sign >= 0f;
        }

        // 볼록 다각형을 t = stop 선으로 나눈다 — below (t ≤ stop) · above (t ≥ stop)
        private static void SplitByLine(List<Vector2> polygon, Vector2 origin, Vector2 axis, float length2, float stop,
            List<Vector2> below, List<Vector2> above)
        {
            below.Clear();
            above.Clear();
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % n];
                float fa = Vector2.Dot(a - origin, axis) / length2 - stop;
                float fb = Vector2.Dot(b - origin, axis) / length2 - stop;
                if (fa <= 0f)
                    below.Add(a);
                if (fa >= 0f)
                    above.Add(a);
                if ((fa < 0f && fb > 0f) || (fa > 0f && fb < 0f))
                {
                    Vector2 cross = Vector2.Lerp(a, b, fa / (fa - fb));
                    below.Add(cross);
                    above.Add(cross);
                }
            }
        }

        private void EmitGradientPiece(List<Vector2> piece, Vector2 origin, Vector2 axis, float length2, float[] stops, Color[] colors)
        {
            if (piece.Count < 3)
                return;
            int start = _vh.currentVertCount;
            foreach (Vector2 p in piece)
                AddVert(p, Paint(Evaluate(Vector2.Dot(p - origin, axis) / length2, stops, colors)));
            for (int i = 1; i < piece.Count - 1; i++)
                _vh.AddTriangle(start, start + i, start + i + 1);
        }

        private static Color Evaluate(float t, float[] stops, Color[] colors)
        {
            if (t <= stops[0])
                return colors[0];
            for (int i = 1; i < stops.Length; i++)
            {
                if (t <= stops[i])
                    return Color.Lerp(colors[i - 1], colors[i], Mathf.InverseLerp(stops[i - 1], stops[i], t));
            }
            return colors[colors.Length - 1];
        }

        private void StrokeSubpath(Subpath path, float half, Color32 color)
        {
            _clean.Clear();
            foreach (Vector2 p in path.Points)
            {
                if (_clean.Count == 0 || (p - _clean[_clean.Count - 1]).sqrMagnitude > Epsilon)
                    _clean.Add(p);
            }
            bool closed = path.Closed;
            if (closed)
            {
                while (_clean.Count > 1 && (_clean[0] - _clean[_clean.Count - 1]).sqrMagnitude <= Epsilon)
                    _clean.RemoveAt(_clean.Count - 1);
                closed = _clean.Count > 2;
            }
            int n = _clean.Count;
            if (n < 2)
                return;

            _left.Clear();
            _right.Clear();
            int first = closed ? 0 : 1, last = closed ? n - 1 : n - 2;
            if (!closed)
            {
                Vector2 normal = Normal(_clean[0], _clean[1]);
                _left.Add(_clean[0] + normal * half);
                _right.Add(_clean[0] - normal * half);
                Cap(_clean[0], normal, half, color, true);
            }

            bool restart = false;
            for (int i = first; i <= last; i++)
            {
                Vector2 prev = _clean[(i - 1 + n) % n], p = _clean[i], next = _clean[(i + 1) % n];
                Vector2 n0 = Normal(prev, p), n1 = Normal(p, next);
                Vector2 mid = n0 + n1;
                float cosHalf = mid.magnitude / 2f;
                if (cosHalf > MiterLimitCos)
                {
                    Vector2 miter = mid.normalized * (half / cosHalf);
                    _left.Add(p + miter);
                    _right.Add(p - miter);
                    continue;
                }

                // 많이 꺾였다 — 띠를 여기서 끊고 둥근 이음
                _left.Add(p + n0 * half);
                _right.Add(p - n0 * half);
                EmitStrip(color, closed && !restart && i == first);
                _left.Clear();
                _right.Clear();
                _left.Add(p + n1 * half);
                _right.Add(p - n1 * half);
                Disc(p, half, color);
                restart = true;
            }

            if (closed)
            {
                // 첫 점으로 돌아와 닫는다 — 첫 점에서 끊겼으면 들어오는 쪽, 아니면 첫 점의 뾰족 이음
                Vector2 prev = _clean[n - 1], p = _clean[0], next = _clean[1];
                Vector2 n0 = Normal(prev, p), n1 = Normal(p, next);
                Vector2 mid = n0 + n1;
                float cosHalf = mid.magnitude / 2f;
                if (cosHalf > MiterLimitCos)
                {
                    Vector2 miter = mid.normalized * (half / cosHalf);
                    _left.Add(p + miter);
                    _right.Add(p - miter);
                }
                else
                {
                    _left.Add(p + n0 * half);
                    _right.Add(p - n0 * half);
                }
            }
            else
            {
                Vector2 normal = Normal(_clean[n - 2], _clean[n - 1]);
                _left.Add(_clean[n - 1] + normal * half);
                _right.Add(_clean[n - 1] - normal * half);
                Cap(_clean[n - 1], normal, half, color, false);
            }
            EmitStrip(color, false);
        }

        // _left · _right 를 띠로 낸다. skip 이면 (닫힌 경로의 첫 점에서 끊긴 앞 조각 — 점 하나뿐) 내지 않는다
        private void EmitStrip(Color32 color, bool skip)
        {
            if (skip || _left.Count < 2)
                return;
            int start = _vh.currentVertCount;
            for (int i = 0; i < _left.Count; i++)
            {
                AddVert(_left[i], color);
                AddVert(_right[i], color);
            }
            for (int i = 0; i < _left.Count - 1; i++)
            {
                int a = start + i * 2;
                _vh.AddTriangle(a, a + 1, a + 2);
                _vh.AddTriangle(a + 1, a + 3, a + 2);
            }
        }

        private static Vector2 Normal(Vector2 from, Vector2 to)
        {
            Vector2 d = (to - from).normalized;
            return new Vector2(-d.y, d.x);
        }

        // 둥근 끝 — 선 방향 반대쪽(시작) 또는 앞쪽(끝)으로 반원
        private void Cap(Vector2 p, Vector2 normal, float half, Color32 color, bool start)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * half / CurveStep), 4, 32);
            float a0 = Mathf.Atan2(normal.y, normal.x);
            float sweep = start ? Mathf.PI : -Mathf.PI;
            int center = _vh.currentVertCount;
            AddVert(p, color);
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + sweep * i / n;
                AddVert(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * half, color);
            }
            for (int i = 0; i < n; i++)
                _vh.AddTriangle(center, center + 1 + i, center + 2 + i);
        }

        private void Disc(Vector2 p, float radius, Color32 color)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 2f * radius / CurveStep), 8, 48);
            int center = _vh.currentVertCount;
            AddVert(p, color);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                AddVert(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color);
            }
            for (int i = 0; i < n; i++)
                _vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % n);
        }

        #endregion
    }
}
