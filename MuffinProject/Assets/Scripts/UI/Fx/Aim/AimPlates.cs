using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>미리 그린 판 한 장 — 텍스처로 올릴 바이트와, 원래 판 안에서의 자리.</summary>
    public sealed class AimPlate
    {
        /// <summary>원래 판 너비 = 텍스처 너비 (시안 px).</summary>
        public int Width;

        /// <summary>원래 판 높이 (시안 px) — 위아래 투명한 줄을 잘라 내기 전.</summary>
        public int Height;

        /// <summary>잘라 낸 위쪽 줄 수.</summary>
        public int Top;

        /// <summary>텍스처 높이 (남은 줄 수).</summary>
        public int Rows;

        /// <summary>RGBA32, 아래에서 위로, 알파를 곱하지 않은 색.</summary>
        public byte[] Rgba;
    }

    /// <summary>
    /// 시안 AIMC.build() 를 옮긴 것 — 총알 컷부터 쓰는 판들을 시안과 같은 씨앗 · 순서로 그린다.
    /// 망원 설원(B.tele: 하늘 · 산 · 숲 · 눈밭), 옆으로 번진 설원(B.streak), 앞 소나무(B.pineFg), 앞 가지 둘(B.sprigs[0 · 1]), 필름 알갱이(B.grain).
    /// 시안이 그리기만 하고 이 연출에서 안 쓰는 판(구름 · 전경 · 마무리 설원)은 그리지 않되, 같은 수열을 쓰는 곳은 수만 뽑아 넘긴다.
    /// 엔진 기능을 쓰지 않아 작업 스레드에서 부른다.
    /// </summary>
    public static class AimPlates
    {
        private const int FieldWidth = 1700;
        private const int FieldHeight = 900;
        private const int TileSize = 256;
        private const float Tau = Mathf.PI * 2f;
        private const float Pi = Mathf.PI;

        // B.tele = fieldPlates(23, {hz:330, mt:120, rows:4, ts:1.5, treeDrop:110, gap:150, clumps:26, dens:.8})
        private const int FieldSeed = 23;
        private const float Horizon = 330f;
        private const float MountainHeight = 120f;
        private const int TreeRows = 4;
        private const float TreeScale = 1.5f;
        private const float TreeDrop = 110f;
        private const float Gap = 150f;
        private const int Clumps = 26;
        private const float Density = 0.8f;

        private const int CloudCount = 13;
        private const int StarCount = 300;
        private const int MoundCount = 120;
        private const int StreakSeed = 41;
        private const int PineSeed = 8;
        private const int SprigSeed = 5;
        private const int GrainSeed = 77;

        /// <summary>망원 설원 네 판 (하늘 · 산 · 숲 · 눈밭, 시안 B.tele).</summary>
        public sealed class Field
        {
            public AimPlate Sky;
            public AimPlate Mountains;
            public AimPlate Trees;
            public AimPlate Ground;
        }

        /// <summary>나머지 판 — 옆으로 번진 설원 · 앞 소나무 · 앞 가지 둘 · 필름 알갱이 셋.</summary>
        public sealed class Extras
        {
            public AimPlate Streak;
            public AimPlate PineFront;
            public AimPlate SprigA;
            public AimPlate SprigB;
            public byte[][] Grain;
        }

        /// <summary>시안 B.tele 를 그린다 (시간이 가장 오래 걸린다).</summary>
        public static Field BuildField()
        {
            float[] noise = NoiseTile(TileSize, 3, 5);
            float[] noise2 = NoiseTile(TileSize, 9, 3);
            var rng = new AimRandom(FieldSeed);
            var field = new Field();

            var sky = new AimRaster(FieldWidth, FieldHeight);
            DrawSky(sky, rng, noise);
            field.Sky = ToPlate(sky);
            sky = null;

            SkipClouds(rng);

            var mountains = new AimRaster(FieldWidth, FieldHeight);
            DrawMountains(mountains, rng, noise);
            field.Mountains = ToPlate(mountains);
            mountains = null;

            var trees = new AimRaster(FieldWidth, FieldHeight);
            DrawTrees(trees, rng, noise);
            field.Trees = ToPlate(trees);
            trees = null;

            var ground = new AimRaster(FieldWidth, FieldHeight);
            DrawGround(ground, rng, noise, noise2);
            field.Ground = ToPlate(ground);
            return field;
        }

        /// <summary>시안 B.streak · B.pineFg · B.sprigs[0 · 1] · B.grain 을 그린다.</summary>
        public static Extras BuildExtras()
        {
            float[] noise = NoiseTile(TileSize, 3, 5);
            var extras = new Extras();
            extras.Streak = ToPlate(DrawStreak(noise));

            var pine = new AimRaster(420, 760);
            Pine(pine, 210f, 750f, 720f, new AimRandom(PineSeed), 0.9f);
            extras.PineFront = ToPlate(AimRaster.Blurred(pine, 7f));

            // B.sprigs = [0,1,2].map(i => blurred(가지 넷, 6 + i)) — 셋째 판은 이 연출에서 안 쓴다
            var rng = new AimRandom(SprigSeed);
            extras.SprigA = ToPlate(AimRaster.Blurred(DrawSprigs(rng), 6f));
            extras.SprigB = ToPlate(AimRaster.Blurred(DrawSprigs(rng), 7f));

            extras.Grain = new byte[3][];
            for (int s = 1; s <= 3; s++)
                extras.Grain[s - 1] = Grain(s * GrainSeed);
            return extras;
        }

        /// <summary>
        /// 시안 B.grain 한 장 (256², 회색 잡음) — overlay 로 13% 덮는 알갱이를, 2배 곱하기(머티리얼)로 같게 보이도록 미리 바꿔 둔다:
        /// 어두운 곳에서 overlay 결과 = 바탕 × (0.87 + 0.26 × 잡음) = 바탕 × 2 × 이 값.
        /// </summary>
        public static byte[] Grain(int seed)
        {
            const float overlayAlpha = 0.13f;
            var rng = new AimRandom(seed);
            var bytes = new byte[TileSize * TileSize * 4];
            for (int i = 0; i < TileSize * TileSize; i++)
            {
                float v = Mathf.Round(rng.Next() * 255f) / 255f;
                byte g = (byte)Mathf.Round((0.5f - overlayAlpha * 0.5f + overlayAlpha * v) * 255f);
                bytes[i * 4] = g;
                bytes[i * 4 + 1] = g;
                bytes[i * 4 + 2] = g;
                bytes[i * 4 + 3] = 255;
            }
            return bytes;
        }

        #region 설원 (fieldPlates)

        private static void DrawSky(AimRaster x, AimRandom rng, float[] noise)
        {
            x.FillRectLinearVertical(0, 0, FieldWidth, FieldHeight, 0f, Horizon + 40f,
                new[] { 0f, 0.5f, 0.85f, 1f }, new[] { Hex(0x0f1631), Hex(0x1f2850), Hex(0x3a4678), Hex(0x55619a) });
            for (int i = 0; i < StarCount; i++)
            {
                float alpha = 0.12f + rng.Next() * 0.6f;
                float r = rng.Below(0.08) ? 2.2f : 1f + rng.Next();
                float px = rng.Next() * FieldWidth;
                float py = rng.Next() * Horizon * 0.95f;
                x.FillRect(px, py, r, r, Rgba(214, 224, 255, alpha));
            }
            x.ApplyTexture(noise, TileSize, 0.5f, AimRaster.Blend.SoftLight, 3f);
        }

        // 구름 판은 쓰지 않지만 시안이 같은 수열로 그리므로 같은 수만큼 뽑는다
        private static void SkipClouds(AimRandom rng)
        {
            for (int i = 0; i < CloudCount; i++)
            {
                rng.Skip(3);
                int n = 5 + (int)(rng.NextDouble() * 5);
                rng.Skip(3 * n + n + n);
            }
        }

        private static void DrawMountains(AimRaster x, AimRandom rng, float[] noise)
        {
            var pts = new Vector2[35];
            for (int layer = 0; layer < 2; layer++)
            {
                float baseY = Horizon + 6f;
                float amp = MountainHeight * (layer == 1 ? 0.62f : 1f);
                for (int i = 0; i <= 34; i++)
                {
                    float px = i / 34f * FieldWidth;
                    float y = Mathf.Abs(Mathf.Sin(i * 0.9f + FieldSeed + layer * 2.2f)) * 0.6f + Mathf.Abs(Mathf.Sin(i * 0.37f + layer)) * 0.5f + rng.Next() * 0.12f;
                    pts[i] = new Vector2(px, baseY - amp * (0.25f + y * 0.62f));
                }

                x.BeginPath();
                x.MoveTo(0f, baseY);
                foreach (Vector2 p in pts)
                    x.LineTo(p.x, p.y);
                x.LineTo(FieldWidth, baseY);
                x.ClosePath();
                x.Fill(layer == 1 ? Hex(0x243060) : Hex(0x2f3b6c));

                // 달빛 받는 비탈(봉우리 왼쪽) + 눈 골
                x.Clip();
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    Vector2 a = pts[i - 1];
                    Vector2 b = pts[i];
                    Vector2 c = pts[i + 1];
                    if (!(b.y < a.y && b.y <= c.y))
                        continue;
                    x.BeginPath();
                    x.MoveTo(b.x + 2f, b.y - 2f);
                    x.LineTo(a.x - 12f, a.y + 8f);
                    x.LineTo(a.x + 8f, a.y + 46f);
                    x.LineTo(Mathf.Lerp(a.x, b.x, 0.55f), Mathf.Lerp(a.y, b.y, 0.5f) + 26f);
                    x.LineTo(b.x - 10f, b.y + 66f);
                    x.LineTo(b.x + 6f, b.y + 30f);
                    x.ClosePath();
                    x.Fill(layer == 1 ? Hex(0x6f7cb8) : Hex(0x8f9bd4));
                    x.BeginPath();
                    x.MoveTo(b.x + 4f, b.y + 6f);
                    x.LineTo(c.x - 6f, c.y + 30f);
                    x.LineTo(b.x + 22f, b.y + 70f);
                    x.ClosePath();
                    x.Fill(layer == 1 ? Hex(0x4a568c) : Hex(0x56629c));
                }
                x.ResetClip();

                x.BeginPath();
                x.MoveTo(pts[0].x, pts[0].y);
                foreach (Vector2 p in pts)
                    x.LineTo(p.x, p.y);
                x.Stroke(Rgba(9, 15, 32, 0.75f), 2f);
            }
            x.FillRectLinearVertical(0, (int)Horizon - 60, FieldWidth, 72, Horizon - 60f, Horizon + 10f,
                new[] { 0f, 1f }, new[] { Rgba(70, 84, 140, 0f), Rgba(78, 92, 150, 0.7f) });
            x.ApplyTexture(noise, TileSize, 0.45f, AimRaster.Blend.SoftLight, 2.4f);
        }

        private static void DrawTrees(AimRaster x, AimRandom rng, float[] noise)
        {
            var row = new AimRaster(FieldWidth, FieldHeight);
            for (int r = 0; r < TreeRows; r++)
            {
                float k = r / (float)(TreeRows - 1);
                int n = AimRaster.JsRound(Mathf.Lerp(50f, 14f, k) * Density);
                float hh = Mathf.Lerp(54f, 240f, k) * TreeScale;
                float yy = Horizon + Mathf.Lerp(2f, TreeDrop, k);
                row.Clear();
                for (int i = 0; i < n; i++)
                {
                    float px = (i + rng.Next()) / n * FieldWidth;
                    if (Mathf.Abs(px - FieldWidth / 2f) < Gap * (1.25f - k * 0.5f))
                        continue;
                    float py = yy + rng.Next() * 14f;
                    float h = hh * (0.7f + rng.Next() * 0.6f);
                    Pine(row, px, py, h, rng, Mathf.Lerp(0.45f, 1f, k));
                }
                // 먼 줄일수록 안개 색을 덮는다
                row.FillSourceAtop(Rgba(58, 70, 124, Mathf.Lerp(0.55f, 0f, k)));
                x.DrawOver(r == 0 ? AimRaster.Blurred(row, 1.4f) : row, 0, 0);
            }
            x.ApplyTexture(noise, TileSize, 0.3f, AimRaster.Blend.SoftLight, 1.8f);
        }

        private static void DrawGround(AimRaster x, AimRandom rng, float[] noise, float[] noise2)
        {
            x.FillRectLinearVertical(0, (int)Horizon, FieldWidth, FieldHeight - (int)Horizon, Horizon, FieldHeight,
                new[] { 0f, 0.3f, 1f }, new[] { Hex(0x525e96), Hex(0x6672ac), Hex(0x7b87c2) });

            // 눈 둔덕: 밝은 윗면 + 그 아래 딱 떨어지는 그늘 + 아랫선 먹선
            for (int i = 0; i < MoundCount; i++)
            {
                float d = Mathf.Pow(rng.Next(), 1.4f);
                float y = Horizon + 10f + d * (FieldHeight - Horizon);
                float sc = 0.22f + d * 1.5f;
                float px = rng.Next() * FieldWidth;
                float rx = (70f + rng.Next() * 190f) * sc;
                float ry = rx * (0.09f + rng.Next() * 0.06f);
                Blob(x, rng, px + rx * 0.06f, y + ry * 0.75f, rx * 1.04f, ry * 0.9f, 16, 0.07f);
                x.Fill(Hex(0x3b4577));
                Color top = rng.Below(0.5) ? Hex(0xa4ade0) : Hex(0x929dd6);
                Blob(x, rng, px, y, rx, ry * 0.8f, 16, 0.08f);
                x.Fill(top);
                if (rng.Below(0.6))
                {
                    Blob(x, rng, px - rx * 0.2f, y - ry * 0.22f, rx * 0.5f, ry * 0.34f, 10, 0.1f);
                    x.Fill(Hex(0xb9c3ee));
                }
                x.BeginPath();
                x.Ellipse(px, y, rx, ry * 0.8f, 0f, 0.15f, Pi - 0.4f);
                x.Stroke(Rgba(9, 15, 32, 0.4f), Mathf.Max(1f, 1.6f * sc), false);
            }

            // 눈 덮인 덤불 · 바위
            for (int i = 0; i < Clumps; i++)
            {
                float d = Mathf.Pow(rng.Next(), 1.3f);
                float y = Horizon + 14f + d * (FieldHeight - Horizon - 20f);
                float sc = 0.25f + d * 1.5f;
                float px = rng.Next() * FieldWidth;
                if (Mathf.Abs(px - FieldWidth / 2f) < Gap * 0.8f && d < 0.55f)
                    continue;
                x.BeginPath();
                x.Ellipse(px + 12f * sc, y + 6f * sc, 64f * sc, 9f * sc, 0f, 0f, Tau);
                x.Fill(Rgba(22, 30, 62, 0.6f));
                for (int j = 0; j < 5; j++)
                {
                    float cx = px + (rng.Next() - 0.5f) * 72f * sc;
                    float cy = y - rng.Next() * 12f * sc;
                    float rx = (16f + rng.Next() * 20f) * sc;
                    float ry = (10f + rng.Next() * 10f) * sc;
                    Blob(x, rng, cx, cy, rx, ry, 9, 0.3f);
                    x.Fill(Hex(0x0c1228));
                    x.Stroke(Hex(0x060a18), Mathf.Max(1f, 1.5f * sc));
                }
                for (int j = 0; j < 4; j++)
                {
                    float cx = px + (rng.Next() - 0.6f) * 58f * sc;
                    float cy = y - (12f + rng.Next() * 10f) * sc;
                    float rx = (11f + rng.Next() * 15f) * sc;
                    float ry = (3.5f + rng.Next() * 3.5f) * sc;
                    Blob(x, rng, cx, cy, rx, ry, 8, 0.2f);
                    x.Fill(Hex(0xa4ade0));
                }
            }

            x.FillRectLinearVertical(0, (int)Horizon, FieldWidth, 50, Horizon, Horizon + 50f,
                new[] { 0f, 1f }, new[] { Rgba(40, 50, 95, 0.75f), Rgba(40, 50, 95, 0f) });
            x.ApplyTexture(noise, TileSize, 0.55f, AimRaster.Blend.SoftLight, 2.6f);
            x.ApplyTexture(noise2, TileSize, 0.22f, AimRaster.Blend.Overlay, 5f);
        }

        private static void Blob(AimRaster x, AimRandom rng, float cx, float cy, float rx, float ry, int n, float jitter)
        {
            x.BeginPath();
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Tau;
                float r = 1f - jitter + rng.Next() * jitter * 2f;
                float px = cx + Mathf.Cos(a) * rx * r;
                float py = cy + Mathf.Sin(a) * ry * r;
                if (i == 0)
                    x.MoveTo(px, py);
                else
                    x.LineTo(px, py);
            }
            x.ClosePath();
        }

        #endregion

        #region 재료 (pine · sprig)

        // 시안 pine — 층마다 아래가 톱니처럼 처지는 가지 + 달빛 받는 왼쪽 위에 얹힌 눈
        private static void Pine(AimRaster x, float px, float py, float h, AimRandom rng, float lit)
        {
            const int tiers = 7;
            float w0 = h * 0.27f;
            x.Save();
            x.Translate(px, py);
            x.FillRect(-h * 0.02f, -h * 0.12f, h * 0.04f, h * 0.12f, Hex(0x0c1228));
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < tiers; i++)
            {
                float k = i / (float)tiers;
                float y = -h * 0.07f - k * h * 0.8f;
                float w = w0 * (1f - k * 0.8f) * (0.9f + rng.Next() * 0.2f);
                float th = h * 0.2f * (1f - k * 0.3f);
                int n = 4 + ((tiers - i) >> 1);
                var top = new Vector2(0f, y - th);
                left.Clear();
                right.Clear();
                for (int j = 0; j <= n; j++)
                {
                    float u = j / (float)n;
                    float dx = w * u;
                    float dy = y - th * (1f - u) * 0.95f + (j % 2 == 1 ? th * 0.2f : 0f) + rng.Next() * th * 0.08f + (j == n ? th * 0.1f : 0f);
                    right.Add(new Vector2(dx, dy));
                    left.Add(new Vector2(-dx, dy));
                }
                x.BeginPath();
                x.MoveTo(top.x, top.y);
                foreach (Vector2 p in right)
                    x.LineTo(p.x, p.y);
                x.LineTo(0f, y + th * 0.06f);
                for (int j = left.Count - 1; j >= 0; j--)
                    x.LineTo(left[j].x, left[j].y);
                x.ClosePath();
                x.Fill(i % 2 == 1 ? Hex(0x101733) : Hex(0x0c1228));
                x.Stroke(Hex(0x060a18), Mathf.Max(0.8f, h * 0.006f));

                x.BeginPath();
                x.MoveTo(top.x, top.y);
                x.LineTo(-w * 0.34f, y - th * 0.52f);
                x.LineTo(-w * 0.62f, y - th * 0.2f);
                x.LineTo(-w * 0.4f, y - th * 0.3f);
                x.LineTo(-w * 0.12f, y - th * 0.42f);
                x.LineTo(w * 0.18f, y - th * 0.56f);
                x.LineTo(w * 0.34f, y - th * 0.4f);
                x.LineTo(w * 0.1f, y - th * 0.7f);
                x.ClosePath();
                x.Fill(Rgba(127, 139, 196, 0.9f * lit));

                x.BeginPath();
                x.MoveTo(top.x, top.y);
                x.LineTo(-w * 0.26f, y - th * 0.6f);
                x.LineTo(-w * 0.05f, y - th * 0.66f);
                x.LineTo(w * 0.08f, y - th * 0.76f);
                x.ClosePath();
                x.Fill(Rgba(190, 199, 240, 0.85f * lit));
            }
            x.Restore();
        }

        // 시안 sprigs 한 판 — 잎 달린 잔가지 넷 (실루엣)
        private static AimRaster DrawSprigs(AimRandom rng)
        {
            var x = new AimRaster(520, 360);
            for (int j = 0; j < 4; j++)
            {
                float px = 30f + rng.Next() * 60f;
                float py = 330f - j * 26f - rng.Next() * 30f;
                float length = 300f + rng.Next() * 150f;
                float angle = -0.5f - rng.Next() * 0.5f - j * 0.08f;
                Sprig(x, px, py, length, angle, rng);
            }
            return x;
        }

        private static void Sprig(AimRaster x, float px, float py, float length, float angle, AimRandom rng)
        {
            Color ink = Hex(0x070b1a);
            x.Save();
            x.Translate(px, py);
            x.Rotate(angle);
            x.BeginPath();
            x.MoveTo(0f, 0f);
            x.QuadraticCurveTo(length * 0.5f, -length * 0.12f, length, 0f);
            x.Stroke(ink, length * 0.035f);
            for (int i = 1; i < 11; i++)
            {
                float k = i / 11f;
                float bx = length * k;
                float by = -length * 0.12f * Mathf.Sin(k * Pi) * 0.9f;
                float l = length * (0.3f - k * 0.16f);
                for (int s = -1; s <= 1; s += 2)
                {
                    x.Save();
                    x.Translate(bx, by);
                    x.Rotate(s * (0.75f + rng.Next() * 0.35f));
                    x.BeginPath();
                    x.Ellipse(l * 0.5f, 0f, l * 0.55f, l * 0.13f, 0f, 0f, Tau);
                    x.Fill(ink);
                    x.Restore();
                }
            }
            x.Restore();
        }

        #endregion

        #region 옆으로 번진 설원 (B.streak)

        private static AimRaster DrawStreak(float[] noise)
        {
            const int w = 1280;
            const int h = 720;
            var x = new AimRaster(w, h);
            x.FillRectLinearVertical(0, 0, w, h, 0f, h, new[] { 0f, 0.42f, 0.6f, 1f },
                new[] { Hex(0x0f1631), Hex(0x27305a), Hex(0x56629c), Hex(0x7f8bc4) });
            var rng = new AimRandom(StreakSeed);
            for (int i = 0; i < 150; i++)
            {
                float y = rng.Next() * h;
                bool dark = y < 400f ? rng.Below(0.75) : rng.Below(0.35);
                Color color = dark ? Rgba(9, 15, 32, 0.12f + rng.Next() * 0.34f) : Rgba(190, 199, 240, 0.06f + rng.Next() * 0.2f);
                float width = 200f + rng.Next() * 900f;
                float px = rng.Next() * w - width / 2f;
                float height = 2f + rng.Next() * (dark ? 30f : 12f);
                for (int o = -1; o <= 1; o++)
                    x.FillRect(px + o * w, y, width, height, color);
            }
            x.ApplyTexture(noise, TileSize, 0.4f, AimRaster.Blend.SoftLight, 3f);
            AimRaster blurred = AimRaster.Blurred(x, 2f);

            // 왼쪽 끝을 오른쪽 끝과 겹쳐 이어지게 (옆으로 끝없이 흘려도 이음매가 안 보이게)
            var result = new AimRaster(w, h);
            result.DrawOver(blurred, 0, 0);
            result.EraseRectLinearHorizontal(0, 0, 90, h, 0f, w, new[] { 0f, 0.06f }, new[] { Rgba(0, 0, 0, 1f), Rgba(0, 0, 0, 0f) });
            result.DrawBehind(blurred, -1190, 0);
            return result;
        }

        #endregion

        #region 공용

        /// <summary>시안 noiseTile — 값 잡음 여러 겹(붓 얼룩 느낌), 0~1.</summary>
        public static float[] NoiseTile(int n, int seed, int octaves)
        {
            var rng = new AimRandom(seed);
            var acc = new float[n * n];
            float amp = 1f;
            float total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int g = 4 << o;
                var grid = new float[g * g];
                for (int i = 0; i < grid.Length; i++)
                    grid[i] = rng.Next();
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x / (float)n * g;
                        float fy = y / (float)n * g;
                        int x0 = (int)fx;
                        int y0 = (int)fy;
                        float tx = fx - x0;
                        float ty = fy - y0;
                        float sx = tx * tx * (3f - 2f * tx);
                        float sy = ty * ty * (3f - 2f * ty);
                        int x1 = (x0 + 1) % g;
                        int y1 = (y0 + 1) % g;
                        float top = Mathf.Lerp(grid[y0 * g + x0], grid[y0 * g + x1], sx);
                        float bottom = Mathf.Lerp(grid[y1 * g + x0], grid[y1 * g + x1], sx);
                        acc[y * n + x] += amp * Mathf.Lerp(top, bottom, sy);
                    }
                }
                total += amp;
                amp *= 0.55f;
            }
            for (int i = 0; i < acc.Length; i++)
                acc[i] = Mathf.Round(acc[i] / total * 255f) / 255f;
            return acc;
        }

        private static AimPlate ToPlate(AimRaster raster)
        {
            byte[] bytes = raster.ToTextureBytes(out int top, out int rows);
            return new AimPlate { Width = raster.Width, Height = raster.Height, Top = top, Rows = rows, Rgba = bytes };
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
        }

        private static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        #endregion
    }
}
