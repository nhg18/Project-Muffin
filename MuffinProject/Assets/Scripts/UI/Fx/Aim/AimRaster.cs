using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 AIMC 가 미리 그려 두는 판(설원 · 줄 번진 배경 · 앞 가지)을 시안과 같은 방법으로 그리는 작은 픽셀 판 —
    /// 경로 채우기 · 선 · 그라데이션 · 흐림(줄였다 늘리기) · 무늬 덮기(soft-light · overlay).
    /// 텍스처 같은 엔진 기능을 쓰지 않아 작업 스레드에서 돈다. 픽셀은 위에서 아래로, 색은 알파를 곱한 0~255 (premultiplied).
    /// 좌표는 시안 canvas 그대로 (아래로 +, 회전은 시계 방향 +).
    /// </summary>
    public sealed class AimRaster
    {
        /// <summary>무늬 덮기의 섞는 방식.</summary>
        public enum Blend { SoftLight, Overlay }

        // 곡선을 꺾은선으로 나눌 때: 마디 수 = 휜 각도 × √반지름 × 이 값
        private const float ArcDetail = 1.5f;
        private const int MinArcSegments = 6;
        private const int MaxArcSegments = 128;
        private const float CurveStep = 4f;
        private const float MinCoverage = 0.0005f;

        /// <summary>너비 (px).</summary>
        public readonly int Width;

        /// <summary>높이 (px).</summary>
        public readonly int Height;

        /// <summary>RGBA, 알파를 곱한 값, 위에서 아래로.</summary>
        public readonly byte[] Pixels;

        private readonly List<List<Vector2>> _paths = new List<List<Vector2>>();
        private readonly List<bool> _closed = new List<bool>();
        private readonly List<List<Vector2>> _strokePolygons = new List<List<Vector2>>();
        private readonly Stack<AimAffine> _stack = new Stack<AimAffine>();
        private AimAffine _matrix = AimAffine.Identity;
        private List<Vector2> _current;
        private Vector2 _last;
        private Vector2 _start;
        private float[] _accumulation = new float[0];
        private float[] _clip;

        /// <summary>투명한 판을 만든다.</summary>
        public AimRaster(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
        }

        /// <summary>판을 투명하게 비운다.</summary>
        public void Clear()
        {
            Array.Clear(Pixels, 0, Pixels.Length);
        }

        #region 변환

        /// <summary>canvas save — 변환만 저장한다.</summary>
        public void Save()
        {
            _stack.Push(_matrix);
        }

        /// <summary>canvas restore.</summary>
        public void Restore()
        {
            if (_stack.Count > 0)
                _matrix = _stack.Pop();
        }

        /// <summary>canvas translate.</summary>
        public void Translate(float x, float y)
        {
            _matrix.Translate(x, y);
        }

        /// <summary>canvas rotate (라디안).</summary>
        public void Rotate(float radians)
        {
            _matrix.Rotate(radians);
        }

        #endregion

        #region 경로

        /// <summary>canvas beginPath.</summary>
        public void BeginPath()
        {
            _paths.Clear();
            _closed.Clear();
            _current = null;
        }

        /// <summary>canvas moveTo.</summary>
        public void MoveTo(float x, float y)
        {
            _current = new List<Vector2>();
            _paths.Add(_current);
            _closed.Add(false);
            _start = new Vector2(x, y);
            _last = _start;
            _current.Add(_matrix.Apply(x, y));
        }

        /// <summary>canvas lineTo.</summary>
        public void LineTo(float x, float y)
        {
            if (_current == null)
            {
                MoveTo(x, y);
                return;
            }
            _last = new Vector2(x, y);
            _current.Add(_matrix.Apply(x, y));
        }

        /// <summary>canvas quadraticCurveTo.</summary>
        public void QuadraticCurveTo(float cx, float cy, float x, float y)
        {
            if (_current == null)
                MoveTo(cx, cy);
            Vector2 p0 = _last;
            var c = new Vector2(cx, cy);
            var p1 = new Vector2(x, y);
            float length = (Vector2.Distance(p0, c) + Vector2.Distance(c, p1)) * _matrix.LineScale;
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / CurveStep), 4, 32);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float r = 1f - t;
                Vector2 p = r * r * p0 + 2f * r * t * c + t * t * p1;
                _current.Add(_matrix.Apply(p));
            }
            _last = p1;
        }

        /// <summary>canvas ellipse(cx, cy, rx, ry, rotation, start, end) — 시계 방향.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, float rotation, float start, float end)
        {
            float radius = Mathf.Max(rx, ry) * _matrix.LineScale;
            float sweep = Mathf.Abs(end - start);
            int segments = Mathf.Clamp(Mathf.CeilToInt(sweep * Mathf.Sqrt(Mathf.Max(radius, 1f)) * ArcDetail), MinArcSegments, MaxArcSegments);
            float cr = Mathf.Cos(rotation);
            float sr = Mathf.Sin(rotation);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)segments);
                float ex = rx * Mathf.Cos(a);
                float ey = ry * Mathf.Sin(a);
                float x = cx + ex * cr - ey * sr;
                float y = cy + ex * sr + ey * cr;
                if (i == 0 && _current == null)
                    MoveTo(x, y);
                else
                    LineTo(x, y);
            }
        }

        /// <summary>canvas rect.</summary>
        public void Rect(float x, float y, float w, float h)
        {
            MoveTo(x, y);
            LineTo(x + w, y);
            LineTo(x + w, y + h);
            LineTo(x, y + h);
            ClosePath();
        }

        /// <summary>canvas closePath — 다음 lineTo 는 새 조각을 시작점에서 연다.</summary>
        public void ClosePath()
        {
            if (_current == null)
                return;
            _closed[_closed.Count - 1] = true;
            _current = null;
            _last = _start;
        }

        #endregion

        #region 칠하기

        /// <summary>canvas fill — 지금 경로를 색으로 채운다 (모든 조각을 닫아서). 경로는 남는다.</summary>
        public void Fill(Color color)
        {
            if (!TryBounds(_paths, 0f, out int x0, out int y0, out int x1, out int y1))
                return;
            int bw = x1 - x0 + 2;
            int bh = y1 - y0;
            PrepareAccumulation(bw * bh);
            foreach (List<Vector2> path in _paths)
                AccumulatePolygon(path, x0, y0, bw, bh, false);
            ResolveOver(x0, y0, x1, y1, bw, color);
        }

        /// <summary>canvas stroke — 둥근 이음(시안 lineJoin round). roundCaps 가 아니면 열린 선의 두 끝은 자른다.</summary>
        public void Stroke(Color color, float width, bool roundCaps = true)
        {
            float half = width * _matrix.LineScale * 0.5f;
            _strokePolygons.Clear();
            for (int p = 0; p < _paths.Count; p++)
                AddStrokePolygons(_paths[p], _closed[p], half, roundCaps);
            if (!TryBounds(_strokePolygons, 0f, out int x0, out int y0, out int x1, out int y1))
                return;
            int bw = x1 - x0 + 2;
            int bh = y1 - y0;
            PrepareAccumulation(bw * bh);
            foreach (List<Vector2> polygon in _strokePolygons)
                AccumulatePolygon(polygon, x0, y0, bw, bh, true);
            ResolveOver(x0, y0, x1, y1, bw, color);
        }

        /// <summary>canvas fillRect (변환 적용).</summary>
        public void FillRect(float x, float y, float w, float h, Color color)
        {
            BeginPath();
            Rect(x, y, w, h);
            Fill(color);
            BeginPath();
        }

        /// <summary>canvas clip — 지금 경로 안만 칠해지게 한다. <see cref="ResetClip"/> 로 푼다.</summary>
        public void Clip()
        {
            _clip = new float[Width * Height];
            if (!TryBounds(_paths, 0f, out int x0, out int y0, out int x1, out int y1))
                return;
            int bw = x1 - x0 + 2;
            int bh = y1 - y0;
            PrepareAccumulation(bw * bh);
            foreach (List<Vector2> path in _paths)
                AccumulatePolygon(path, x0, y0, bw, bh, false);
            for (int row = 0; row < bh; row++)
            {
                int line = row * bw;
                float sum = 0f;
                for (int col = 0; col < bw; col++)
                {
                    sum += _accumulation[line + col];
                    _accumulation[line + col] = 0f;
                    if (col < x1 - x0)
                        _clip[(y0 + row) * Width + x0 + col] = Mathf.Min(1f, Mathf.Abs(sum));
                }
            }
        }

        /// <summary>clip 을 푼다 (시안 restore).</summary>
        public void ResetClip()
        {
            _clip = null;
        }

        /// <summary>세로 그라데이션으로 정수 사각형을 덮는다 — 그라데이션은 gy0 → gy1, 그 밖은 끝 색.</summary>
        public void FillRectLinearVertical(int x, int y, int w, int h, float gy0, float gy1, float[] offsets, Color[] colors)
        {
            int xa = Mathf.Max(0, x);
            int xb = Mathf.Min(Width, x + w);
            int ya = Mathf.Max(0, y);
            int yb = Mathf.Min(Height, y + h);
            for (int py = ya; py < yb; py++)
            {
                GradientAt(offsets, colors, ((py + 0.5f) - gy0) / (gy1 - gy0), out float r, out float g, out float b, out float a);
                int i = (py * Width + xa) * 4;
                for (int px = xa; px < xb; px++, i += 4)
                    BlendPremultiplied(i, r, g, b, a);
            }
        }

        /// <summary>가로 그라데이션 알파로 지운다 (canvas destination-out).</summary>
        public void EraseRectLinearHorizontal(int x, int y, int w, int h, float gx0, float gx1, float[] offsets, Color[] colors)
        {
            int xa = Mathf.Max(0, x);
            int xb = Mathf.Min(Width, x + w);
            int ya = Mathf.Max(0, y);
            int yb = Mathf.Min(Height, y + h);
            for (int px = xa; px < xb; px++)
            {
                GradientAt(offsets, colors, ((px + 0.5f) - gx0) / (gx1 - gx0), out _, out _, out _, out float a);
                float keep = 1f - a;
                for (int py = ya; py < yb; py++)
                {
                    int i = (py * Width + px) * 4;
                    Pixels[i] = ToByte(Pixels[i] * keep);
                    Pixels[i + 1] = ToByte(Pixels[i + 1] * keep);
                    Pixels[i + 2] = ToByte(Pixels[i + 2] * keep);
                    Pixels[i + 3] = ToByte(Pixels[i + 3] * keep);
                }
            }
        }

        /// <summary>이미 칠한 곳에만 색을 덮는다 (canvas source-atop + fillRect 판 전체).</summary>
        public void FillSourceAtop(Color color)
        {
            float keep = 1f - color.a;
            float r = color.r * color.a;
            float g = color.g * color.a;
            float b = color.b * color.a;
            for (int i = 0; i < Pixels.Length; i += 4)
            {
                byte alpha = Pixels[i + 3];
                if (alpha == 0)
                    continue;
                Pixels[i] = ToByte(r * alpha + Pixels[i] * keep);
                Pixels[i + 1] = ToByte(g * alpha + Pixels[i + 1] * keep);
                Pixels[i + 2] = ToByte(b * alpha + Pixels[i + 2] * keep);
            }
        }

        /// <summary>다른 판을 (dx, dy) 에 위로 얹는다 (canvas drawImage, source-over).</summary>
        public void DrawOver(AimRaster source, int dx, int dy)
        {
            Composite(source, dx, dy, false);
        }

        /// <summary>다른 판을 (dx, dy) 에 뒤로 깐다 (canvas destination-over).</summary>
        public void DrawBehind(AimRaster source, int dx, int dy)
        {
            Composite(source, dx, dy, true);
        }

        /// <summary>
        /// 시안 texture() — 회색 무늬(tile, n×n, 0~1)를 scale 배로 깔아, 칠해진 곳에만 mode 로 alpha 만큼 섞는다.
        /// 붓 얼룩 · 종이 결 같은 얼룩을 낸다.
        /// </summary>
        public void ApplyTexture(float[] tile, int n, float alpha, Blend mode, float scale)
        {
            for (int py = 0; py < Height; py++)
            {
                float ty = (py + 0.5f) / scale - 0.5f;
                for (int px = 0; px < Width; px++)
                {
                    int i = (py * Width + px) * 4;
                    float ab = Pixels[i + 3] / 255f;
                    if (ab <= 0f)
                        continue;
                    float cs = SampleTile(tile, n, (px + 0.5f) / scale - 0.5f, ty);
                    float sa = ab * alpha;
                    for (int c = 0; c < 3; c++)
                    {
                        float cb = Pixels[i + c] / 255f / ab;
                        float blended = mode == Blend.SoftLight ? SoftLight(cb, cs) : Overlay(cb, cs);
                        float co = sa * (1f - ab) * cs + ab * (1f - sa) * cb + sa * ab * blended;
                        Pixels[i + c] = ToByte(co * 255f);
                    }
                    Pixels[i + 3] = ToByte((sa + ab * (1f - sa)) * 255f);
                }
            }
        }

        #endregion

        #region 흐림

        /// <summary>시안 blurred — 줄였다가 다시 늘려서 흐린다 (px 가 클수록 많이).</summary>
        public static AimRaster Blurred(AimRaster source, float px)
        {
            if (px <= 0f)
                return source;
            float f = Mathf.Max(1.5f, px * 1.6f);
            int w = Mathf.Max(2, JsRound(source.Width / f));
            int h = Mathf.Max(2, JsRound(source.Height / f));
            AimRaster small = ResizeArea(source, w, h);
            if (px > 3f)
                small = ResizeArea(small, Mathf.Max(2, w >> 1), Mathf.Max(2, h >> 1));
            return ResizeBilinear(small, source.Width, source.Height);
        }

        /// <summary>시안 Math.round (0.5 는 올림).</summary>
        public static int JsRound(float v)
        {
            return Mathf.FloorToInt(v + 0.5f);
        }

        // 줄이기 — 칸마다 덮는 원본 픽셀을 넓이만큼 평균 (canvas imageSmoothingQuality high 근사)
        private static AimRaster ResizeArea(AimRaster source, int w, int h)
        {
            int sw = source.Width;
            int sh = source.Height;
            var rows = new float[w * sh * 4];
            float sx = sw / (float)w;
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float from = x * sx;
                    float to = from + sx;
                    int o = (y * w + x) * 4;
                    for (int s = (int)from; s < Mathf.Min(sw, Mathf.CeilToInt(to)); s++)
                    {
                        float weight = (Mathf.Min(to, s + 1f) - Mathf.Max(from, s)) / sx;
                        int i = (y * sw + s) * 4;
                        rows[o] += source.Pixels[i] * weight;
                        rows[o + 1] += source.Pixels[i + 1] * weight;
                        rows[o + 2] += source.Pixels[i + 2] * weight;
                        rows[o + 3] += source.Pixels[i + 3] * weight;
                    }
                }
            }

            var result = new AimRaster(w, h);
            float sy = sh / (float)h;
            for (int y = 0; y < h; y++)
            {
                float from = y * sy;
                float to = from + sy;
                for (int x = 0; x < w; x++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    float a = 0f;
                    for (int s = (int)from; s < Mathf.Min(sh, Mathf.CeilToInt(to)); s++)
                    {
                        float weight = (Mathf.Min(to, s + 1f) - Mathf.Max(from, s)) / sy;
                        int i = (s * w + x) * 4;
                        r += rows[i] * weight;
                        g += rows[i + 1] * weight;
                        b += rows[i + 2] * weight;
                        a += rows[i + 3] * weight;
                    }
                    int o = (y * w + x) * 4;
                    result.Pixels[o] = ToByte(r);
                    result.Pixels[o + 1] = ToByte(g);
                    result.Pixels[o + 2] = ToByte(b);
                    result.Pixels[o + 3] = ToByte(a);
                }
            }
            return result;
        }

        // 늘리기 — 쌍선형, 픽셀 가운데 맞춤, 가장자리는 끝 픽셀
        private static AimRaster ResizeBilinear(AimRaster source, int w, int h)
        {
            var result = new AimRaster(w, h);
            int sw = source.Width;
            int sh = source.Height;
            float kx = sw / (float)w;
            float ky = sh / (float)h;
            for (int y = 0; y < h; y++)
            {
                float fy = Mathf.Clamp((y + 0.5f) * ky - 0.5f, 0f, sh - 1);
                int y0 = (int)fy;
                int y1 = Mathf.Min(sh - 1, y0 + 1);
                float ty = fy - y0;
                for (int x = 0; x < w; x++)
                {
                    float fx = Mathf.Clamp((x + 0.5f) * kx - 0.5f, 0f, sw - 1);
                    int x0 = (int)fx;
                    int x1 = Mathf.Min(sw - 1, x0 + 1);
                    float tx = fx - x0;
                    int o = (y * w + x) * 4;
                    for (int c = 0; c < 4; c++)
                    {
                        float top = Mathf.Lerp(source.Pixels[(y0 * sw + x0) * 4 + c], source.Pixels[(y0 * sw + x1) * 4 + c], tx);
                        float bottom = Mathf.Lerp(source.Pixels[(y1 * sw + x0) * 4 + c], source.Pixels[(y1 * sw + x1) * 4 + c], tx);
                        result.Pixels[o + c] = ToByte(Mathf.Lerp(top, bottom, ty));
                    }
                }
            }
            return result;
        }

        #endregion

        #region 텍스처로

        /// <summary>
        /// Unity 텍스처(RGBA32, 아래에서 위로, 알파를 곱하지 않은 색)용 바이트로 바꾼다. 위아래로 투명한 줄은 잘라 낸다 (top = 잘라 낸 위쪽 줄 수).
        /// 투명한 픽셀에는 옆 픽셀의 색을 번져 둔다 — 늘려 그릴 때 가장자리가 검게 번지지 않게.
        /// </summary>
        public byte[] ToTextureBytes(out int top, out int rows)
        {
            top = 0;
            int bottom = Height - 1;
            while (top < Height && RowEmpty(top))
                top++;
            while (bottom > top && RowEmpty(bottom))
                bottom--;
            rows = Mathf.Max(1, bottom - top + 1);

            var straight = new byte[Width * rows * 4];
            for (int y = 0; y < rows; y++)
            {
                int source = (top + y) * Width * 4;
                int target = (rows - 1 - y) * Width * 4;
                for (int x = 0; x < Width * 4; x += 4)
                {
                    byte a = Pixels[source + x + 3];
                    straight[target + x + 3] = a;
                    if (a == 0)
                        continue;
                    float k = 255f / a;
                    straight[target + x] = ToByte(Pixels[source + x] * k);
                    straight[target + x + 1] = ToByte(Pixels[source + x + 1] * k);
                    straight[target + x + 2] = ToByte(Pixels[source + x + 2] * k);
                }
            }
            Bleed(straight, Width, rows);
            return straight;
        }

        private bool RowEmpty(int y)
        {
            int i = y * Width * 4 + 3;
            for (int x = 0; x < Width; x++, i += 4)
            {
                if (Pixels[i] != 0)
                    return false;
            }
            return true;
        }

        private static void Bleed(byte[] rgba, int w, int h)
        {
            var source = (byte[])rgba.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    if (source[i + 3] != 0)
                        continue;
                    int n = -1;
                    if (x > 0 && source[i - 4 + 3] != 0)
                        n = i - 4;
                    else if (x < w - 1 && source[i + 4 + 3] != 0)
                        n = i + 4;
                    else if (y > 0 && source[i - w * 4 + 3] != 0)
                        n = i - w * 4;
                    else if (y < h - 1 && source[i + w * 4 + 3] != 0)
                        n = i + w * 4;
                    if (n < 0)
                        continue;
                    rgba[i] = source[n];
                    rgba[i + 1] = source[n + 1];
                    rgba[i + 2] = source[n + 2];
                }
            }
        }

        #endregion

        #region 내부

        private void PrepareAccumulation(int size)
        {
            if (_accumulation.Length < size)
                _accumulation = new float[size];
        }

        private bool TryBounds(List<List<Vector2>> polygons, float pad, out int x0, out int y0, out int x1, out int y1)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (List<Vector2> polygon in polygons)
            {
                foreach (Vector2 p in polygon)
                {
                    minX = Mathf.Min(minX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x);
                    maxY = Mathf.Max(maxY, p.y);
                }
            }
            x0 = Mathf.Max(0, Mathf.FloorToInt(minX - pad));
            y0 = Mathf.Max(0, Mathf.FloorToInt(minY - pad));
            x1 = Mathf.Min(Width, Mathf.CeilToInt(maxX + pad));
            y1 = Mathf.Min(Height, Mathf.CeilToInt(maxY + pad));
            return x1 > x0 && y1 > y0;
        }

        // 선 한 줄 = 마디마다 두께 사각형 + 꺾이는 곳 · 끝마다 동그라미 (모두 같은 방향으로 감아 겹쳐도 한 겹)
        private void AddStrokePolygons(List<Vector2> path, bool closed, float half, bool roundCaps)
        {
            int n = path.Count;
            if (n < 2 || half <= 0f)
                return;
            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = path[i];
                Vector2 b = path[(i + 1) % n];
                Vector2 d = b - a;
                float length = d.magnitude;
                if (length < 1e-4f)
                    continue;
                Vector2 normal = new Vector2(-d.y, d.x) / length * half;
                _strokePolygons.Add(new List<Vector2> { a + normal, b + normal, b - normal, a - normal });
            }
            int dots = Mathf.Clamp(Mathf.CeilToInt(half * 3f), 6, 20);
            for (int i = 0; i < n; i++)
            {
                if (!closed && !roundCaps && (i == 0 || i == n - 1))
                    continue;
                var circle = new List<Vector2>(dots);
                for (int k = 0; k < dots; k++)
                {
                    float angle = k * Mathf.PI * 2f / dots;
                    circle.Add(path[i] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * half);
                }
                _strokePolygons.Add(circle);
            }
        }

        private void AccumulatePolygon(List<Vector2> polygon, int x0, int y0, int bw, int bh, bool forcePositive)
        {
            int n = polygon.Count;
            if (n < 2)
                return;
            bool reverse = false;
            if (forcePositive)
            {
                float area = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = polygon[i];
                    Vector2 b = polygon[(i + 1) % n];
                    area += a.x * b.y - b.x * a.y;
                }
                reverse = area < 0f;
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % n];
                if (reverse)
                    AccumulateLine(b.x - x0, b.y - y0, a.x - x0, a.y - y0, bw, bh);
                else
                    AccumulateLine(a.x - x0, a.y - y0, b.x - x0, b.y - y0, bw, bh);
            }
        }

        // 부호 있는 넓이를 칸에 쌓는다 — 줄마다 왼쪽부터 더하면 그 픽셀이 덮인 정도가 된다 (font-rs 방식, 경계가 매끈하다)
        private void AccumulateLine(float ax, float ay, float bx, float by, int bw, int bh)
        {
            if (Mathf.Abs(ay - by) < 1e-7f)
                return;
            float dir = 1f;
            if (ay > by)
            {
                dir = -1f;
                float tx = ax;
                ax = bx;
                bx = tx;
                float ty = ay;
                ay = by;
                by = ty;
            }
            if (by <= 0f || ay >= bh)
                return;

            float dxdy = (bx - ax) / (by - ay);
            float x = ax;
            float top = ay;
            if (top < 0f)
            {
                x -= top * dxdy;
                top = 0f;
            }
            int rowStart = (int)top;
            int rowEnd = Mathf.Min(bh, Mathf.CeilToInt(by));
            float maxX = bw - 2;
            for (int y = rowStart; y < rowEnd; y++)
            {
                float dy = Mathf.Min(y + 1f, by) - Mathf.Max(y, top);
                float next = x + dxdy * dy;
                float d = dy * dir;
                float xa = Mathf.Clamp(Mathf.Min(x, next), 0f, maxX);
                float xb = Mathf.Clamp(Mathf.Max(x, next), 0f, maxX);
                int line = y * bw;
                float xaFloor = Mathf.Floor(xa);
                int xai = (int)xaFloor;
                float xbCeil = Mathf.Ceil(xb);
                int xbi = (int)xbCeil;
                if (xbi <= xai + 1)
                {
                    float middle = 0.5f * (xa + xb) - xaFloor;
                    _accumulation[line + xai] += d - d * middle;
                    _accumulation[line + xai + 1] += d * middle;
                }
                else
                {
                    float s = 1f / (xb - xa);
                    float xaFraction = xa - xaFloor;
                    float a0 = 0.5f * s * (1f - xaFraction) * (1f - xaFraction);
                    float xbFraction = xb - xbCeil + 1f;
                    float am = 0.5f * s * xbFraction * xbFraction;
                    _accumulation[line + xai] += d * a0;
                    if (xbi == xai + 2)
                    {
                        _accumulation[line + xai + 1] += d * (1f - a0 - am);
                    }
                    else
                    {
                        float a1 = s * (1.5f - xaFraction);
                        _accumulation[line + xai + 1] += d * (a1 - a0);
                        for (int xi = xai + 2; xi < xbi - 1; xi++)
                            _accumulation[line + xi] += d * s;
                        float a2 = a1 + (xbi - xai - 3) * s;
                        _accumulation[line + xbi - 1] += d * (1f - a2 - am);
                    }
                    _accumulation[line + xbi] += d * am;
                }
                x = next;
            }
        }

        private void ResolveOver(int x0, int y0, int x1, int y1, int bw, Color color)
        {
            float r = color.r * color.a;
            float g = color.g * color.a;
            float b = color.b * color.a;
            int w = x1 - x0;
            for (int row = 0; row < y1 - y0; row++)
            {
                int line = row * bw;
                int py = y0 + row;
                float sum = 0f;
                for (int col = 0; col < bw; col++)
                {
                    sum += _accumulation[line + col];
                    _accumulation[line + col] = 0f;
                    if (col >= w)
                        continue;
                    float coverage = Mathf.Min(1f, Mathf.Abs(sum));
                    if (_clip != null)
                        coverage *= _clip[py * Width + x0 + col];
                    if (coverage < MinCoverage)
                        continue;
                    BlendPremultiplied((py * Width + x0 + col) * 4, r * coverage, g * coverage, b * coverage, color.a * coverage);
                }
            }
        }

        // 알파를 곱한 색(0~1)을 위에 얹는다 (source-over)
        private void BlendPremultiplied(int i, float r, float g, float b, float a)
        {
            float keep = 1f - a;
            Pixels[i] = ToByte(r * 255f + Pixels[i] * keep);
            Pixels[i + 1] = ToByte(g * 255f + Pixels[i + 1] * keep);
            Pixels[i + 2] = ToByte(b * 255f + Pixels[i + 2] * keep);
            Pixels[i + 3] = ToByte(a * 255f + Pixels[i + 3] * keep);
        }

        private void Composite(AimRaster source, int dx, int dy, bool behind)
        {
            int xa = Mathf.Max(0, dx);
            int xb = Mathf.Min(Width, dx + source.Width);
            int ya = Mathf.Max(0, dy);
            int yb = Mathf.Min(Height, dy + source.Height);
            for (int y = ya; y < yb; y++)
            {
                for (int x = xa; x < xb; x++)
                {
                    int s = ((y - dy) * source.Width + (x - dx)) * 4;
                    int t = (y * Width + x) * 4;
                    if (behind)
                    {
                        float keep = 1f - Pixels[t + 3] / 255f;
                        for (int c = 0; c < 4; c++)
                            Pixels[t + c] = ToByte(Pixels[t + c] + source.Pixels[s + c] * keep);
                    }
                    else
                    {
                        float keep = 1f - source.Pixels[s + 3] / 255f;
                        for (int c = 0; c < 4; c++)
                            Pixels[t + c] = ToByte(source.Pixels[s + c] + Pixels[t + c] * keep);
                    }
                }
            }
        }

        // 그라데이션 색 — 알파를 곱한 값으로 섞는다 (canvas 와 같게), 결과도 알파를 곱한 값
        private static void GradientAt(float[] offsets, Color[] colors, float t, out float r, out float g, out float b, out float a)
        {
            int last = offsets.Length - 1;
            Color c0 = colors[0];
            Color c1 = colors[0];
            float k = 0f;
            if (t <= offsets[0])
            {
                c1 = c0;
            }
            else if (t >= offsets[last])
            {
                c0 = colors[last];
                c1 = c0;
            }
            else
            {
                for (int i = 0; i < last; i++)
                {
                    if (t > offsets[i + 1])
                        continue;
                    c0 = colors[i];
                    c1 = colors[i + 1];
                    float span = offsets[i + 1] - offsets[i];
                    k = span <= 0f ? 1f : (t - offsets[i]) / span;
                    break;
                }
            }
            a = Mathf.Lerp(c0.a, c1.a, k);
            r = Mathf.Lerp(c0.r * c0.a, c1.r * c1.a, k);
            g = Mathf.Lerp(c0.g * c0.a, c1.g * c1.a, k);
            b = Mathf.Lerp(c0.b * c0.a, c1.b * c1.a, k);
        }

        private static float SampleTile(float[] tile, int n, float x, float y)
        {
            float fx = Mathf.Floor(x);
            float fy = Mathf.Floor(y);
            float tx = x - fx;
            float ty = y - fy;
            int x0 = Wrap((int)fx, n);
            int y0 = Wrap((int)fy, n);
            int x1 = (x0 + 1) % n;
            int y1 = (y0 + 1) % n;
            float top = Mathf.Lerp(tile[y0 * n + x0], tile[y0 * n + x1], tx);
            float bottom = Mathf.Lerp(tile[y1 * n + x0], tile[y1 * n + x1], tx);
            return Mathf.Lerp(top, bottom, ty);
        }

        private static int Wrap(int v, int n)
        {
            int r = v % n;
            return r < 0 ? r + n : r;
        }

        // W3C soft-light (cb 바탕, cs 덮는 색)
        private static float SoftLight(float cb, float cs)
        {
            if (cs <= 0.5f)
                return cb - (1f - 2f * cs) * cb * (1f - cb);
            float d = cb <= 0.25f ? ((16f * cb - 12f) * cb + 4f) * cb : Mathf.Sqrt(cb);
            return cb + (2f * cs - 1f) * (d - cb);
        }

        // W3C overlay
        private static float Overlay(float cb, float cs)
        {
            return cb <= 0.5f ? 2f * cb * cs : 1f - 2f * (1f - cb) * (1f - cs);
        }

        private static byte ToByte(float v)
        {
            if (v <= 0f)
                return 0;
            if (v >= 255f)
                return 255;
            return (byte)(v + 0.5f);
        }

        #endregion
    }
}
