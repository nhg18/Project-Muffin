using System;
using System.Collections;
using System.Threading.Tasks;
using Chapchu.UI.Fx.Comic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 띠 컷인의 카드 그림 — 네모 틀 없이 캐릭터만 띄운다 (아티팩트 「찹츄 카드 이펙트 v2」 .ci-art.ci-cut).
    /// 그림을 칸에 맞춰(contain) 놓고 흰 외곽선 → 아래로 어긋난 진한 그림자 → 빛(glow 색) 순으로 두른 뒤, 칸 가장자리를 흐리게 지운다.
    /// 시안은 흰 바탕을 지워 캐릭터만 남기지만 게임의 카드 그림은 바탕이 이미 투명해서 그 단계는 없다.
    /// 그림 한 장을 실행 때 만든다 (<see cref="Build"/> — 일꾼 스레드). 다 만들 때까지는 아무것도 보이지 않는다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class BandArtCutOut : MonoBehaviour
    {
        // 시안 drop-shadow 의 흐림 값은 가우스 표준편차의 두 배 (box-shadow 와 같다)
        private const float BlurToSigma = 0.5f;
        // 큰 그림은 한 번에 줄이면 계단이 생긴다 — 이 배율씩 나눠 줄인다
        private const int MaxStepShrink = 2;

        [SerializeField] private RawImage picture;
        [Tooltip("만들 그림의 세로 (px) — 화면에 보이는 크기쯤이면 된다.")]
        [SerializeField] private int resolution = 640;
        [Tooltip("흰 외곽선 두께 (시안 px, drop-shadow ±3px).")]
        [SerializeField] private float outline = 3f;
        [Tooltip("그림자가 아래로 어긋난 거리 (시안 px, 0.6vmin — 720 화면 기준).")]
        [SerializeField] private float shadowDrop = 4.32f;
        [Tooltip("그림자 색 (알파 = 진하기, 시안 rgba(43, 40, 86, .55)).")]
        [SerializeField] private Color shadowColor = new Color(43f / 255f, 40f / 255f, 86f / 255f, 0.55f);
        [Tooltip("빛이 번지는 흐림 (시안 px, drop-shadow 2.4vmin — 720 화면 기준).")]
        [SerializeField] private float glowBlur = 17.28f;
        [Tooltip("칸 가장자리를 흐리게 지우는 폭 — 칸 비율 (x 왼쪽 · y 오른쪽 · z 위 · w 아래, 시안 9% · 9% · 8% · 14%).")]
        [SerializeField] private Vector4 edgeFade = new Vector4(0.09f, 0.09f, 0.08f, 0.14f);

        private Texture2D _built;
        private bool _building;

        /// <summary>그림을 다 만들었거나 만들지 못했다 (못 했으면 비어 보인다).</summary>
        public bool IsReady => !_building;

        private void Awake()
        {
            if (picture == null)
                Debug.LogError($"[{nameof(BandArtCutOut)}] {name} 의 {nameof(picture)} 가 비었다", this);
        }

        private void OnDestroy() => Release();

        /// <summary>그림을 만들기 시작한다. pixelScale = 시안 px 하나의 이 칸 단위 크기 (FxLayer.PixelScale).</summary>
        public void Build(Sprite art, Color glow, float pixelScale)
        {
            Release();
            picture.enabled = false;
            if (art == null || !isActiveAndEnabled)
                return;
            StartCoroutine(BuildRoutine(art, glow, pixelScale));
        }

        private IEnumerator BuildRoutine(Sprite art, Color glow, float pixelScale)
        {
            _building = true;
            Rect box = ((RectTransform)transform).rect;
            int height = Mathf.Max(8, resolution);
            int width = Mathf.Max(8, Mathf.RoundToInt(height * box.width / box.height));
            // 그림은 칸 높이에 맞춘다 (칸이 그림보다 넓다 — 시안 object-fit: contain)
            float artAspect = art.rect.width / art.rect.height;
            int artHeight = Mathf.Min(height, Mathf.RoundToInt(width / artAspect));
            int artWidth = Mathf.RoundToInt(artHeight * artAspect);
            Color32[] pixels = Read(art, artWidth, artHeight);

            float texelsPerPx = pixelScale * height / box.height;
            var settings = new Settings
            {
                Width = width,
                Height = height,
                ArtWidth = artWidth,
                ArtHeight = artHeight,
                Outline = Mathf.Max(1, Mathf.RoundToInt(outline * texelsPerPx)),
                ShadowDrop = Mathf.RoundToInt(shadowDrop * texelsPerPx),
                ShadowColor = shadowColor,
                GlowSigma = glowBlur * BlurToSigma * texelsPerPx,
                GlowColor = glow,
                EdgeFade = edgeFade,
            };
            Task<Color32[]> job = Task.Run(() => Compose(pixels, settings));
            while (!job.IsCompleted)
                yield return null;
            _building = false;
            if (job.IsFaulted)
            {
                Debug.LogError($"[{nameof(BandArtCutOut)}] {art.name} 그림을 만들지 못했다: {job.Exception}", this);
                yield break;
            }

            _built = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"{art.name} 띠 컷인",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _built.SetPixels32(job.Result);
            _built.Apply(false, true);
            picture.texture = _built;
            picture.uvRect = new Rect(0f, 0f, 1f, 1f);
            picture.enabled = true;
        }

        // 그림을 width × height 로 줄여 읽는다 (아래 줄부터 — Unity 텍스처 순서). 압축된 그림도 RenderTexture 를 거쳐 읽는다
        private static Color32[] Read(Sprite art, int width, int height)
        {
            Texture texture = art.texture;
            Rect source = art.rect;
            var scale = new Vector2(source.width / texture.width, source.height / texture.height);
            var offset = new Vector2(source.x / texture.width, source.y / texture.height);
            RenderTexture previous = RenderTexture.active;

            // 반씩 줄여 가다가 마지막에 꼭 width × height 로 (그림이 더 작으면 키운다)
            int stepWidth = Mathf.RoundToInt(source.width), stepHeight = Mathf.RoundToInt(source.height);
            RenderTexture current = RenderTexture.GetTemporary(stepWidth, stepHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, current, scale, offset);
            while (stepWidth != width || stepHeight != height)
            {
                bool halve = stepHeight / MaxStepShrink > height && stepWidth / MaxStepShrink > width;
                stepWidth = halve ? stepWidth / MaxStepShrink : width;
                stepHeight = halve ? stepHeight / MaxStepShrink : height;
                RenderTexture next = RenderTexture.GetTemporary(stepWidth, stepHeight, 0, RenderTextureFormat.ARGB32);
                current.filterMode = FilterMode.Bilinear;
                Graphics.Blit(current, next);
                RenderTexture.ReleaseTemporary(current);
                current = next;
            }

            RenderTexture.active = current;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            read.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(current);
            Color32[] pixels = read.GetPixels32();
            Destroy(read);
            return pixels;
        }

        private void Release()
        {
            StopAllCoroutines();
            _building = false;
            if (_built == null)
                return;
            if (picture != null && picture.texture == _built)
                picture.texture = null;
            Destroy(_built);
            _built = null;
        }

        private struct Settings
        {
            public int Width;
            public int Height;
            public int ArtWidth;
            public int ArtHeight;
            public int Outline;
            public int ShadowDrop;
            public Color ShadowColor;
            public float GlowSigma;
            public Color GlowColor;
            public Vector4 EdgeFade;
        }

        // 일꾼 스레드에서 돈다 — Unity API 를 쓰지 않는다. 판은 위 줄부터, 미리 곱한 알파. 줄끼리 따로인 단계는 여러 스레드로 나눈다
        private static Color32[] Compose(Color32[] artBottomUp, Settings s)
        {
            int w = s.Width, h = s.Height, n = w * h;
            var image = new float[n * 4];

            // 1. 칸 가운데 아래에 그림 (시안 object-fit: contain — 그림이 칸 높이에 맞으면 위아래로 꽉 찬다)
            int left = (w - s.ArtWidth) / 2, top = (h - s.ArtHeight) / 2;
            for (int y = 0; y < s.ArtHeight; y++)
            {
                for (int x = 0; x < s.ArtWidth; x++)
                {
                    Color32 c = artBottomUp[(s.ArtHeight - 1 - y) * s.ArtWidth + x];
                    int i = ((y + top) * w + x + left) * 4;
                    float a = c.a / 255f;
                    image[i] = c.r / 255f * a;
                    image[i + 1] = c.g / 255f * a;
                    image[i + 2] = c.b / 255f * a;
                    image[i + 3] = a;
                }
            }

            // 2. 흰 외곽선 — 시안 drop-shadow(3px 0 0 #fff) (-3px 0) (0 3px) (0 -3px) 를 차례로: 앞 결과의 모양을 어긋나게 깐다
            var shade = new float[n * 4];
            DropShadow(image, shade, w, h, s.Outline, 0, Color.white);
            DropShadow(image, shade, w, h, -s.Outline, 0, Color.white);
            DropShadow(image, shade, w, h, 0, s.Outline, Color.white);
            DropShadow(image, shade, w, h, 0, -s.Outline, Color.white);

            // 3. 아래로 어긋난 진한 그림자 (시안 drop-shadow(0 .6vmin 0 rgba(43, 40, 86, .55)))
            DropShadow(image, shade, w, h, 0, s.ShadowDrop, s.ShadowColor);

            // 4. 빛 (시안 drop-shadow(0 0 2.4vmin var(--glow))) — 모양을 흐려 glow 색으로 깐다
            Color glow = s.GlowColor;
            Parallel.For(0, h, y =>
            {
                for (int i = y * w, end = i + w; i < end; i++)
                {
                    float a = image[i * 4 + 3] * glow.a;
                    shade[i * 4] = glow.r * a;
                    shade[i * 4 + 1] = glow.g * a;
                    shade[i * 4 + 2] = glow.b * a;
                    shade[i * 4 + 3] = a;
                }
            });
            ComicSticker.Blur(shade, w, h, s.GlowSigma);
            OverAll(image, shade, w, h);

            // 5. 칸 가장자리를 흐리게 (시안 mask-image 두 줄 — 가로 9% · 91%, 세로 8% · 86%)
            var pixels = new Color32[n];
            Parallel.For(0, h, y =>
            {
                float fy = Fade(y + 0.5f, h, s.EdgeFade.z, s.EdgeFade.w);
                for (int x = 0; x < w; x++)
                {
                    float fade = Fade(x + 0.5f, w, s.EdgeFade.x, s.EdgeFade.y) * fy;
                    int i = (y * w + x) * 4;
                    float a = image[i + 3] * fade;
                    Color32 c = default;
                    if (a > 0f)
                    {
                        // 미리 곱한 색을 알파로 나눈다 — 흐리기는 알파에만 곱했으니 색은 그대로
                        float raw = image[i + 3];
                        c.r = ToByte(image[i] / raw);
                        c.g = ToByte(image[i + 1] / raw);
                        c.b = ToByte(image[i + 2] / raw);
                        c.a = ToByte(a);
                    }
                    pixels[(h - 1 - y) * w + x] = c;
                }
            });
            return pixels;
        }

        // 지금 모양(알파)을 (dx, dy) 만큼 어긋나게 color 로 칠해 아래에 깐다 — shade 는 빈 판으로 빌려 쓴다
        private static void DropShadow(float[] image, float[] shade, int w, int h, int dx, int dy, Color color)
        {
            Array.Clear(shade, 0, shade.Length);
            Parallel.For(0, h, y =>
            {
                int sy = y - dy;
                if (sy < 0 || sy >= h)
                    return;
                for (int x = 0; x < w; x++)
                {
                    int sx = x - dx;
                    if (sx < 0 || sx >= w)
                        continue;
                    float a = image[(sy * w + sx) * 4 + 3] * color.a;
                    int i = (y * w + x) * 4;
                    shade[i] = color.r * a;
                    shade[i + 1] = color.g * a;
                    shade[i + 2] = color.b * a;
                    shade[i + 3] = a;
                }
            });
            OverAll(image, shade, w, h);
        }

        // image 전체를 under 위에 얹는다 — 줄마다 나눠서
        private static void OverAll(float[] image, float[] under, int w, int h)
        {
            Parallel.For(0, h, y =>
            {
                for (int i = y * w * 4, end = i + w * 4; i < end; i += 4)
                    Over(image, under, i);
            });
        }

        // image 를 under 위에 얹는다 (미리 곱한 알파, 결과는 image 에)
        private static void Over(float[] image, float[] under, int i)
        {
            float keep = 1f - image[i + 3];
            image[i] += under[i] * keep;
            image[i + 1] += under[i + 1] * keep;
            image[i + 2] += under[i + 2] * keep;
            image[i + 3] += under[i + 3] * keep;
        }

        // 칸 끝에서 0 → start · end 비율 안쪽에서 1 (CSS linear-gradient 마스크)
        private static float Fade(float position, int length, float start, float end)
        {
            float t = position / length;
            if (start > 0f && t < start)
                return t / start;
            if (end > 0f && t > 1f - end)
                return (1f - t) / end;
            return 1f;
        }

        private static byte ToByte(float v)
        {
            v = v * 255f + 0.5f;
            return (byte)(v <= 0f ? 0f : v >= 255f ? 255f : v);
        }

#if UNITY_EDITOR
        private void Reset() => picture = GetComponent<RawImage>();
#endif
    }
}
