using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 스티커 — 카드 그림 둘레에 흰 테 + 먹 테를 두르고 흐린 그림자를 깐 것 (시안 sticker(src, white, ink) + stk(src, x, y, h, {ax, ay, rot, sx, sy, flip, shadow})).
    /// 자리 = 이 RectTransform 의 피벗 = 그림 속 <see cref="anchor"/> 점. 기울기(rot) = 이 RectTransform, 찌그러짐(sx · sy · flip) = 이 RectTransform 의 localScale.
    /// 테 · 그림자는 그림에서 실행 때 한 번 만든다 (<see cref="StartBuild"/> — 컷인이 시작 전에 부르고 <see cref="IsReady"/> 를 기다린다). 에디터에서는 테 없이 그림만 보인다.
    /// 그림(Sprite)은 압축돼 있어도 된다 — 읽을 때 RenderTexture 를 거친다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ComicSticker : MaskableGraphic
    {
        // 시안 sticker(): 테 바깥으로 6px 더 여백
        private const int ExtraPad = 6;
        // 시안 stk(): 흐린 그림자는 가우스 흐림 — 표준편차의 3 배까지 번진다
        private const float BlurReach = 3f;
        // 가우스 흐림을 상자 흐림 세 번으로 근사한다
        private const int BlurPasses = 3;
        private const byte InsideAlpha = 128;
        private const double Far = 1e20;

        [Tooltip("스티커로 만들 그림 (시안 SPA 그림).")]
        [SerializeField] private Sprite art;
        [Tooltip("그림 전체 높이가 화면에서 차지할 높이 (설계 px, 시안 h).")]
        [SerializeField] private float height = 560f;
        [Tooltip("이 오브젝트 자리에 올 그림 속 점 (그림 px, 왼쪽 위 0, 아래로 + — 시안 ax · ay).")]
        [SerializeField] private Vector2 anchor = new Vector2(300f, 562f);
        [Tooltip("흰 테 두께 (그림 px, 시안 white 11).")]
        [SerializeField] private int white = 11;
        [Tooltip("먹 테 두께 — 흰 테 바깥 (그림 px, 시안 ink 4).")]
        [SerializeField] private int ink = 4;
        [Tooltip("흐린 그림자를 깐다 (시안 shadow).")]
        [SerializeField] private bool shadow = true;
        [Tooltip("그림자 진하기 (시안 0.28).")]
        [SerializeField] private float shadowAlpha = 0.28f;
        [Tooltip("그림자 흐림 — 가우스 표준편차 (그림 px, 시안 blur(6px)).")]
        [SerializeField] private float shadowBlur = 6f;
        [Tooltip("그림자 어긋남 (그림 px, 오른쪽 · 아래 +, 시안 14 · 18).")]
        [SerializeField] private Vector2Int shadowOffset = new Vector2Int(14, 18);

        private Texture2D _built;
        // 만든 그림의 왼쪽 위가 원래 그림의 왼쪽 위에서 얼마나 바깥인지 (그림 px)
        private Vector2Int _builtOrigin;
        private Task<Color32[]> _job;
        private Vector2Int _jobSize;
        private bool _building;
        private bool _failed;

        /// <summary>테 · 그림자를 다 만들었거나, 만들 그림이 없거나, 만들지 못했으면(그림만 보인다) true.</summary>
        public bool IsReady => art == null || _built != null || _failed;

        /// <summary>스티커로 만들 그림. 바꾸면 테를 다시 만들어야 한다 (<see cref="StartBuild"/>).</summary>
        public Sprite Art
        {
            get => art;
            set
            {
                if (art == value)
                    return;
                art = value;
                _failed = false;
                ReleaseBuilt();
                SetAllDirty();
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (_built != null)
                    return _built;
                return art != null ? art.texture : s_WhiteTexture;
            }
        }

        /// <summary>테 · 그림자 만들기를 시작한다 (실행 중에만, 이미 했으면 아무것도 안 한다). 끝나면 <see cref="IsReady"/>.</summary>
        public void StartBuild()
        {
            if (!Application.isPlaying || IsReady || _building || !isActiveAndEnabled)
                return;
            StartCoroutine(BuildRoutine());
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (art == null)
                return;
            Rect source = art.rect; // textureRect 는 투명 둘레를 잘라 내 기준점이 어긋난다
            float k = height / source.height;
            Color32 c = color;
            if (_built != null)
            {
                Quad(vh, -_builtOrigin.x, -_builtOrigin.y, _built.width, _built.height, k, c, new Vector4(0f, 0f, 1f, 1f));
                return;
            }
            Texture texture = art.texture;
            var uv = new Vector4(source.xMin / texture.width, source.yMin / texture.height, source.xMax / texture.width, source.yMax / texture.height);
            Quad(vh, 0f, 0f, source.width, source.height, k, c, uv);
        }

        protected override void OnDestroy()
        {
            ReleaseBuilt();
            base.OnDestroy();
        }

        // 그림 px 칸 (x, y, w, h — 왼쪽 위 기준, 아래로 +) 을 이 오브젝트 좌표로
        private void Quad(VertexHelper vh, float x, float y, float w, float h, float k, Color32 c, Vector4 uv)
        {
            float x0 = (x - anchor.x) * k, x1 = (x + w - anchor.x) * k;
            float y0 = -(y - anchor.y) * k, y1 = -(y + h - anchor.y) * k;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y1), c, new Vector4(uv.x, uv.y, 0f, 0f));
            vh.AddVert(new Vector3(x0, y0), c, new Vector4(uv.x, uv.w, 0f, 0f));
            vh.AddVert(new Vector3(x1, y0), c, new Vector4(uv.z, uv.w, 0f, 0f));
            vh.AddVert(new Vector3(x1, y1), c, new Vector4(uv.z, uv.y, 0f, 0f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private IEnumerator BuildRoutine()
        {
            _building = true;
            Color32[] pixels = ReadArt(out int artWidth, out int artHeight);
            var settings = new BuildSettings
            {
                Width = artWidth,
                Height = artHeight,
                White = Mathf.Max(0, white),
                Ink = Mathf.Max(0, ink),
                Shadow = shadow,
                ShadowAlpha = shadowAlpha,
                ShadowBlur = Mathf.Max(0f, shadowBlur),
                ShadowOffset = shadowOffset,
                InkColor = ComicKit.Ink,
            };
            Vector2Int origin = settings.Origin;
            _jobSize = settings.Size;
            _job = Task.Run(() => Build(pixels, settings));
            while (!_job.IsCompleted)
                yield return null;
            _building = false;
            if (_job.IsFaulted)
            {
                Debug.LogError($"[{nameof(ComicSticker)}] {name} 스티커를 만들지 못했다: {_job.Exception}");
                _job = null;
                _failed = true;
                yield break;
            }
            var texture = new Texture2D(_jobSize.x, _jobSize.y, TextureFormat.RGBA32, false)
            {
                name = $"{art.name} 스티커",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(_job.Result);
            texture.Apply(false, true);
            _job = null;
            _built = texture;
            _builtOrigin = origin;
            SetAllDirty();
        }

        // 그림을 읽는다 (아래 줄부터 — Unity 텍스처 순서)
        private Color32[] ReadArt(out int width, out int rows)
        {
            Rect source = art.rect; // textureRect 는 투명 둘레를 잘라 내 기준점이 어긋난다
            width = Mathf.RoundToInt(source.width);
            rows = Mathf.RoundToInt(source.height);
            Texture texture = art.texture;
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(width, rows, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(source.xMin, source.yMin, width, rows), 0, 0, false);
            read.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] pixels = read.GetPixels32();
            Destroy(read);
            return pixels;
        }

        private void ReleaseBuilt()
        {
            if (_built == null)
                return;
            Destroy(_built);
            _built = null;
        }

        private struct BuildSettings
        {
            public int Width;
            public int Height;
            public int White;
            public int Ink;
            public bool Shadow;
            public float ShadowAlpha;
            public float ShadowBlur;
            public Vector2Int ShadowOffset;
            public Color InkColor;

            // 테를 두른 스티커 여백 (시안 p = white + ink + 6)
            public int Pad => White + Ink + ExtraPad;

            public int BlurPad => Shadow ? Mathf.CeilToInt(ShadowBlur * BlurReach) : 0;

            // 결과 그림의 왼쪽 위가 원래 그림 왼쪽 위에서 얼마나 바깥인지
            public Vector2Int Origin
            {
                get
                {
                    if (!Shadow)
                        return new Vector2Int(Pad, Pad);
                    return new Vector2Int(Pad + Mathf.Max(0, BlurPad - ShadowOffset.x), Pad + Mathf.Max(0, BlurPad - ShadowOffset.y));
                }
            }

            public Vector2Int Size
            {
                get
                {
                    int w = Width + Pad * 2, h = Height + Pad * 2;
                    if (!Shadow)
                        return new Vector2Int(w, h);
                    Vector2Int origin = Origin;
                    int right = Mathf.Max(Pad, Pad + ShadowOffset.x + BlurPad);
                    int bottom = Mathf.Max(Pad, Pad + ShadowOffset.y + BlurPad);
                    return new Vector2Int(origin.x + Width + right, origin.y + Height + bottom);
                }
            }
        }

        // 일꾼 스레드에서 돈다 — Unity API 를 쓰지 않는다. 배열은 모두 위 줄부터
        private static Color32[] Build(Color32[] artBottomUp, BuildSettings s)
        {
            int p = s.Pad, sw = s.Width + p * 2, sh = s.Height + p * 2, n = sw * sh;

            // 1. 테를 두를 판 (시안 sticker 의 캔버스) 에 그림을 놓고, 그림 안쪽까지의 거리
            var art = new float[n * 4];
            var inside = new double[n];
            for (int i = 0; i < n; i++)
                inside[i] = Far;
            for (int y = 0; y < s.Height; y++)
            {
                for (int x = 0; x < s.Width; x++)
                {
                    Color32 c = artBottomUp[(s.Height - 1 - y) * s.Width + x];
                    int i = (y + p) * sw + x + p;
                    float a = c.a / 255f;
                    art[i * 4] = c.r / 255f * a;
                    art[i * 4 + 1] = c.g / 255f * a;
                    art[i * 4 + 2] = c.b / 255f * a;
                    art[i * 4 + 3] = a;
                    if (c.a >= InsideAlpha)
                        inside[i] = 0d;
                }
            }
            DistanceTransform(inside, sw, sh);

            // 2. 먹 테(white + ink) 위에 흰 테(white), 그 위에 그림 — 미리 곱한 알파
            var sticker = new float[n * 4];
            float inkR = s.InkColor.r, inkG = s.InkColor.g, inkB = s.InkColor.b;
            for (int i = 0; i < n; i++)
            {
                float d = (float)Math.Sqrt(inside[i]);
                float inkA = Coverage(s.White + s.Ink, d);
                float whiteA = Coverage(s.White, d);
                // 먹 → 흰
                float r = inkR * inkA, g = inkG * inkA, b = inkB * inkA, a = inkA;
                r = whiteA + r * (1f - whiteA);
                g = whiteA + g * (1f - whiteA);
                b = whiteA + b * (1f - whiteA);
                a = whiteA + a * (1f - whiteA);
                // → 그림
                float artA = art[i * 4 + 3];
                sticker[i * 4] = art[i * 4] + r * (1f - artA);
                sticker[i * 4 + 1] = art[i * 4 + 1] + g * (1f - artA);
                sticker[i * 4 + 2] = art[i * 4 + 2] + b * (1f - artA);
                sticker[i * 4 + 3] = artA + a * (1f - artA);
            }

            // 3. 결과 판 — 흐린 그림자를 깔고 그 위에 스티커
            Vector2Int size = s.Size, origin = s.Origin;
            int ow = size.x, oh = size.y;
            var result = new float[ow * oh * 4];
            int sx0 = origin.x - p, sy0 = origin.y - p;
            if (s.Shadow)
            {
                int bp = s.BlurPad, bw = sw + bp * 2, bh = sh + bp * 2;
                var blurred = new float[bw * bh * 4];
                for (int y = 0; y < sh; y++)
                    Array.Copy(sticker, y * sw * 4, blurred, ((y + bp) * bw + bp) * 4, sw * 4);
                Blur(blurred, bw, bh, s.ShadowBlur);
                int bx0 = sx0 + s.ShadowOffset.x - bp, by0 = sy0 + s.ShadowOffset.y - bp;
                for (int y = 0; y < bh; y++)
                {
                    int ry = y + by0;
                    if (ry < 0 || ry >= oh)
                        continue;
                    for (int x = 0; x < bw; x++)
                    {
                        int rx = x + bx0;
                        if (rx < 0 || rx >= ow)
                            continue;
                        int from = (y * bw + x) * 4, to = (ry * ow + rx) * 4;
                        for (int ch = 0; ch < 4; ch++)
                            result[to + ch] = blurred[from + ch] * s.ShadowAlpha;
                    }
                }
            }
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    int from = (y * sw + x) * 4, to = ((y + sy0) * ow + x + sx0) * 4;
                    float a = sticker[from + 3];
                    for (int ch = 0; ch < 4; ch++)
                        result[to + ch] = sticker[from + ch] + result[to + ch] * (1f - a);
                }
            }

            // 4. Unity 텍스처로 — 아래 줄부터, 곱하지 않은 알파
            var pixels = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                for (int x = 0; x < ow; x++)
                {
                    int i = (y * ow + x) * 4;
                    float a = result[i + 3];
                    Color32 c = default;
                    if (a > 0f)
                    {
                        c.r = ToByte(result[i] / a);
                        c.g = ToByte(result[i + 1] / a);
                        c.b = ToByte(result[i + 2] / a);
                        c.a = ToByte(a);
                    }
                    pixels[(oh - 1 - y) * ow + x] = c;
                }
            }
            return pixels;
        }

        // 그림 안쪽에서 d 떨어진 칸이 두께 radius 테에 덮이는 정도 (가장자리 1px 을 부드럽게)
        private static float Coverage(float radius, float d)
        {
            if (radius <= 0f)
                return 0f;
            float v = radius + 1f - d;
            return v <= 0f ? 0f : v >= 1f ? 1f : v;
        }

        private static byte ToByte(float v)
        {
            v = v * 255f + 0.5f;
            return (byte)(v <= 0f ? 0f : v >= 255f ? 255f : v);
        }

        // 제곱 거리 변환 (Felzenszwalb) — 값 0 인 칸까지의 제곱 거리로 바꾼다
        private static void DistanceTransform(double[] grid, int width, int height)
        {
            int size = Math.Max(width, height);
            var f = new double[size];
            var d = new double[size];
            var v = new int[size];
            var z = new double[size + 1];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    f[y] = grid[y * width + x];
                DistanceTransform1D(f, height, d, v, z);
                for (int y = 0; y < height; y++)
                    grid[y * width + x] = d[y];
            }
            for (int y = 0; y < height; y++)
            {
                Array.Copy(grid, y * width, f, 0, width);
                DistanceTransform1D(f, width, d, v, z);
                Array.Copy(d, 0, grid, y * width, width);
            }
        }

        private static void DistanceTransform1D(double[] f, int n, double[] d, int[] v, double[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2d * q - 2d * v[k]);
                // z[0] = −∞ 라 k 는 0 아래로 내려가지 않는다
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2d * q - 2d * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                    k++;
                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }

        // 가우스 흐림 근사 — 상자 흐림 세 번 (가로 · 세로), 미리 곱한 RGBA
        private static void Blur(float[] data, int width, int height, float sigma)
        {
            if (sigma <= 0f)
                return;
            int[] boxes = BoxSizes(sigma, BlurPasses);
            var temp = new float[data.Length];
            foreach (int box in boxes)
            {
                int radius = box / 2;
                BoxBlur(data, temp, width, height, radius, true);
                BoxBlur(temp, data, width, height, radius, false);
            }
        }

        private static int[] BoxSizes(float sigma, int count)
        {
            double ideal = Math.Sqrt(12d * sigma * sigma / count + 1d);
            int lower = (int)Math.Floor(ideal);
            if (lower % 2 == 0)
                lower--;
            int upper = lower + 2;
            double m = (12d * sigma * sigma - count * lower * lower - 4d * count * lower - 3d * count) / (-4d * lower - 4d);
            int lowerCount = (int)Math.Round(m);
            var sizes = new int[count];
            for (int i = 0; i < count; i++)
                sizes[i] = i < lowerCount ? lower : upper;
            return sizes;
        }

        private static void BoxBlur(float[] from, float[] to, int width, int height, int radius, bool horizontal)
        {
            int lines = horizontal ? height : width, length = horizontal ? width : height;
            int stride = horizontal ? 4 : width * 4;
            float scale = 1f / (radius * 2 + 1);
            var sum = new float[4];
            for (int line = 0; line < lines; line++)
            {
                int start = horizontal ? line * width * 4 : line * 4;
                sum[0] = sum[1] = sum[2] = sum[3] = 0f;
                for (int i = 0; i <= radius && i < length; i++)
                {
                    for (int ch = 0; ch < 4; ch++)
                        sum[ch] += from[start + i * stride + ch];
                }
                for (int i = 0; i < length; i++)
                {
                    for (int ch = 0; ch < 4; ch++)
                        to[start + i * stride + ch] = sum[ch] * scale;
                    int add = i + radius + 1, remove = i - radius;
                    for (int ch = 0; ch < 4; ch++)
                    {
                        if (add < length)
                            sum[ch] += from[start + add * stride + ch];
                        if (remove >= 0)
                            sum[ch] -= from[start + remove * stride + ch];
                    }
                }
            }
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif
    }
}
