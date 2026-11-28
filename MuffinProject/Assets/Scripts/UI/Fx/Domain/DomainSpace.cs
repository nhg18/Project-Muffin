using UnityEngine;

namespace Chapchu.UI.Fx.Domain
{
    /// <summary>
    /// 「영역전개」 영역 안의 우주 — 시안 SPECIAL["영역전개"] 의 DRAW.B(e) 와 같다.
    /// 멀리서 은하 쪽으로 빠르게 날아 들어간 뒤(1.3초, 별이 빛줄기로 늘어난다) 천천히 돌며 내려다본다. 성운 · 별은 강아지 뒤에, 은하(세 팔 소용돌이 · 밝은 중심 · 빛 내림)는 강아지 앞에 그린다.
    /// 모두 빛이라 더하기 머티리얼로 그린다. 길이 단위는 화면 짧은 변(= 1), 별 크기만 시안 px. 별 · 은하 · 성운 자리는 재생마다 무작위.
    /// </summary>
    public class DomainSpace : MonoBehaviour
    {
        private const float Tau = Mathf.PI * 2f;
        // 시안 P3: 초점 거리 · 이보다 가까운 점은 그리지 않는다 (30px)
        private const float Focal = 1.15f, NearPx = 30f;
        // 시안 DRAW.B 카메라: 날아 들어가는 시간 · 처음 거리 · 다가가는 거리 · 그 뒤 다가가는 빠르기
        private const float FlySeconds = 1.3f, CameraFar = 10f, CameraApproach = 7.2f, CameraDrift = 0.3f;
        // 카메라: 내려다보는 각(처음 · 더해지는 양 · 흔들림) · 방위(처음 · 빠르기)
        private const float TiltStart = 0.22f, TiltGain = 0.78f, TiltSway = 0.05f, YawStart = -0.6f, YawSpeed = 0.32f;
        // 은하: 팔 수 · 반지름 · 팔이 감기는 정도 · 도는 빠르기 · 날아 들어가는 동안 더 감기는 양
        private const int Arms = 3;
        private const float GalaxyRadius = 2.6f, ArmCore = 0.12f, ArmTwist = 1.9f, SpinSpeed = 0.22f, SpinFly = 1.2f;
        // 중심: 반지름 · 빛 내림 수 · 반 폭(라디안)
        private const float CoreRadius = 1.1f, RayHalfWidth = 0.035f;
        private const int Rays = 14, CoreSegments = 40;
        // 성운 그림: 한 칸 크기 · 한 칸에 겹치는 빛 구름 수 (시안 mkNeb 256px · 70개 — 절반 크기로 만든다)
        private const int NebulaTile = 128, NebulaBlobs = 70, GlowSize = 64;

        // 시안 mkNeb(c1, c2) 네 벌
        private static readonly Color[,] NebulaColors =
        {
            { new Color32(150, 130, 255, 255), new Color32(90, 140, 255, 255) },
            { new Color32(120, 160, 255, 255), new Color32(230, 220, 255, 255) },
            { new Color32(190, 140, 255, 255), new Color32(255, 255, 255, 255) },
            { new Color32(80, 100, 220, 255), new Color32(170, 120, 255, 255) },
        };

        // 시안 GLW — 은하 팔 사이사이의 빛 번짐 세 색
        private static readonly Color32[] GlowColors = { new Color32(200, 190, 255, 255), new Color32(150, 190, 255, 255), new Color32(255, 240, 230, 255) };

        // 시안 중심 빛의 radial-gradient (자리, 색)
        private static readonly float[] CoreStops = { 0f, 0.12f, 0.4f, 1f };
        private static readonly Color[] CoreColors =
        {
            new Color(1f, 252f / 255f, 240f / 255f, 0.95f), new Color(1f, 235f / 255f, 215f / 255f, 0.6f),
            new Color(190f / 255f, 170f / 255f, 1f, 0.18f), new Color(120f / 255f, 110f / 255f, 1f, 0f),
        };

        [Header("그리는 판 (모두 더하기 머티리얼)")]
        [Tooltip("강아지 뒤 — 성운.")]
        [SerializeField] private UIQuadBatch nebulaBatch;
        [Tooltip("강아지 뒤 — 별.")]
        [SerializeField] private UIQuadBatch starBatch;
        [Tooltip("강아지 앞 — 은하 팔의 빛 번짐.")]
        [SerializeField] private UIQuadBatch glowBatch;
        [Tooltip("강아지 앞 — 은하의 별 · 중심 · 빛 내림.")]
        [SerializeField] private UIQuadBatch galaxyBatch;

        [Header("수 (시안 STAR3 · GAL3 · NEB)")]
        [SerializeField] private int starCount = 1700;
        [SerializeField] private int galaxyCount = 3400;
        [SerializeField] private int nebulaCount = 22;
        [Tooltip("은하의 별 몇 개마다 빛 번짐 하나 (시안 7).")]
        [SerializeField] private int glowEvery = 7;

        [Header("색")]
        [Tooltip("별: 흰 별 · 푸른 별 · 노란 별 (시안 STAR3.c).")]
        [SerializeField] private Color32[] starColors = { new Color32(255, 255, 255, 255), new Color32(0xCD, 0xD8, 0xFF, 255), new Color32(0xFF, 0xE9, 0xD6, 255) };
        [Tooltip("은하 중심의 별 두 색 (시안 GAL3.c).")]
        [SerializeField] private Color32[] coreStarColors = { new Color32(0xFF, 0xF6, 0xE8, 255), new Color32(255, 255, 255, 255) };
        [Tooltip("은하 팔의 별 세 색.")]
        [SerializeField] private Color32[] armStarColors = { new Color32(0xC9, 0xD6, 0xFF, 255), new Color32(0xE2, 0xD6, 0xFF, 255), new Color32(255, 255, 255, 255) };
        [Tooltip("빛 내림: 중심 색 → 끝 색 (끝은 투명).")]
        [SerializeField] private Color rayInner = new Color(1f, 250f / 255f, 240f / 255f, 0.22f);
        [SerializeField] private Color rayOuter = new Color(200f / 255f, 190f / 255f, 1f, 0f);

        private struct Star
        {
            public Vector3 Position;
            public float Size, Twinkle;
            public Color32 Color;
        }

        private struct Particle
        {
            public bool Glow;
            public float Radius, Angle, Height, Size, Alpha;
            public Color32 Color;
        }

        private struct Nebula
        {
            public int Tile;
            public Vector3 Position;
            public float Size, Alpha;
        }

        private Star[] _stars;
        private Particle[] _galaxy;
        private Nebula[] _nebulae;
        private Texture2D _nebulaTexture;
        private Texture2D _glowTexture;

        // 한 프레임 동안 쓰는 카메라 값 (시안 P3 의 cyw · syw · ct · st)
        private float _yawCos, _yawSin, _tiltCos, _tiltSin;

        private void Awake()
        {
            if (nebulaBatch == null || starBatch == null || glowBatch == null || galaxyBatch == null)
                Debug.LogError($"[{nameof(DomainSpace)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnDestroy()
        {
            Destroy(_nebulaTexture);
            Destroy(_glowTexture);
        }

        /// <summary>별 · 은하 · 성운을 새로 뽑고 성운 그림을 만든다. 재생 전에 한 번 부른다.</summary>
        public void Build()
        {
            _stars = new Star[starCount];
            for (int i = 0; i < _stars.Length; i++)
            {
                // 은하를 둘러싼 거대한 구 안 (위아래로 눌린)
                float u = Random.value * 2f - 1f, angle = Random.value * Tau, distance = Random.Range(2.2f, 12f), flat = Mathf.Sqrt(1f - u * u);
                _stars[i] = new Star
                {
                    Position = new Vector3(Mathf.Cos(angle) * flat * distance, u * distance * 0.7f, Mathf.Sin(angle) * flat * distance),
                    Size = Random.value < 0.06f ? Random.Range(2.4f, 4f) : Random.Range(0.6f, 1.8f),
                    Twinkle = Random.value * 6.3f,
                    Color = Random.value < 0.15f ? starColors[1] : Random.value < 0.1f ? starColors[2] : starColors[0],
                };
            }

            _galaxy = new Particle[galaxyCount];
            for (int i = 0; i < _galaxy.Length; i++)
            {
                float u = Mathf.Pow(Random.value, 0.7f), radius = GalaxyRadius * u;
                bool core = u < 0.12f;
                _galaxy[i] = new Particle
                {
                    Glow = i % glowEvery == 0,
                    Radius = radius,
                    Angle = i % Arms * Tau / Arms + Mathf.Log(1f + radius / ArmCore) * ArmTwist + Random.Range(-0.5f, 0.5f) * (1.25f - u),
                    Height = Random.Range(-1f, 1f) * (core ? 0.08f : 0.025f) * (1.1f - u),
                    Size = Random.value < 0.04f ? Random.Range(2.6f, 4.4f) : Random.Range(0.8f, 2.2f),
                    Alpha = core ? 0.9f : 0.35f + 0.5f * (1f - u),
                    Color = core
                        ? Random.value < 0.5f ? coreStarColors[0] : coreStarColors[1]
                        : Random.value < 0.45f ? armStarColors[0] : Random.value < 0.5f ? armStarColors[1] : armStarColors[2],
                };
            }

            _nebulae = new Nebula[nebulaCount];
            for (int i = 0; i < _nebulae.Length; i++)
            {
                _nebulae[i] = new Nebula
                {
                    Tile = i % 4,
                    Position = new Vector3(Random.Range(-1f, 1f) * 4.5f, Random.Range(-1f, 1f) * 1.6f, Random.Range(-1f, 1f) * 4.5f),
                    Size = Random.Range(1.6f, 3.6f),
                    Alpha = Random.Range(0.35f, 0.7f),
                };
            }

            if (_nebulaTexture == null)
            {
                _nebulaTexture = BuildNebulaTexture();
                _glowTexture = BuildGlowTexture();
                nebulaBatch.SetTexture(_nebulaTexture);
                glowBatch.SetTexture(_glowTexture);
            }
        }

        /// <summary>그린 것을 모두 지운다.</summary>
        public void Clear()
        {
            foreach (UIQuadBatch batch in new[] { nebulaBatch, starBatch, glowBatch, galaxyBatch })
            {
                batch.Clear();
                batch.Apply();
            }
        }

        /// <summary>영역에 들어온 지 seconds 초의 모습을 그린다. screen = 화면 크기, unit = 시안 1px 의 크기.</summary>
        public void Draw(float seconds, Vector2 screen, float unit)
        {
            if (_stars == null) return;

            float side = Mathf.Min(screen.x, screen.y);
            float fly = Mathf.Min(1f, seconds / FlySeconds);
            float arrive = 1f - Mathf.Pow(1f - fly, 3f);
            float distance = CameraFar - CameraApproach * arrive - CameraDrift * Mathf.Max(0f, seconds - FlySeconds);
            float tilt = TiltStart + TiltGain * arrive + TiltSway * Mathf.Sin(seconds * 0.5f);
            float yaw = YawStart + seconds * YawSpeed;
            _yawCos = Mathf.Cos(yaw);
            _yawSin = Mathf.Sin(yaw);
            _tiltCos = Mathf.Cos(tilt);
            _tiltSin = Mathf.Sin(tilt);
            float near = NearPx * unit / side;

            DrawNebulae(arrive, distance, near, side, unit);
            DrawStars(seconds, fly < 1f, distance, near, side, screen, unit);
            DrawGalaxy(seconds, arrive, distance, near, side, unit);
            DrawCore(seconds, arrive, yaw, distance, near, side);

            nebulaBatch.Apply();
            starBatch.Apply();
            glowBatch.Apply();
            galaxyBatch.Apply();
        }

        // 성운: 깊이마다 겹친 빛 구름 — 가까운 것은 크고 빠르게, 먼 것은 작고 느리게 지나간다
        private void DrawNebulae(float arrive, float distance, float near, float side, float unit)
        {
            nebulaBatch.Clear();
            foreach (Nebula nebula in _nebulae)
            {
                if (!Project(nebula.Position, distance, near, out Vector3 seen)) continue;

                float size = nebula.Size * seen.z * side;
                if (size < 4f * unit) continue;

                float depth = seen.z > 0f ? Focal / seen.z : 0f;
                float fog = Mathf.Min(1f, depth / 1.2f) * Mathf.Max(0f, 1f - depth / 16f);
                // 옆 칸의 색이 번져 들어오지 않게 한 픽셀 안쪽만 쓴다
                const float inset = 1f / (NebulaTile * 2);
                var uv = new Rect(nebula.Tile % 2 * 0.5f + inset, nebula.Tile / 2 * 0.5f + inset, 0.5f - inset * 2f, 0.5f - inset * 2f);
                nebulaBatch.Quad(new Vector2(seen.x, seen.y) * side, new Vector2(size, size), White(nebula.Alpha * fog * (0.5f + 0.5f * arrive)), uv);
            }
        }

        // 별: 날아 들어가는 동안은 한 프레임 전 자리까지 이어 빛줄기로
        private void DrawStars(float seconds, bool warp, float distance, float near, float side, Vector2 screen, float unit)
        {
            starBatch.Clear();
            Vector2 edge = screen * 0.5f + Vector2.one * (40f * unit);
            float before = Mathf.Max(0f, (seconds - 1f / 60f) / FlySeconds);
            float beforeDistance = CameraFar - CameraApproach * (1f - Mathf.Pow(1f - before, 3f));
            foreach (Star star in _stars)
            {
                if (!Project(star.Position, distance, near, out Vector3 seen)) continue;

                Vector2 at = new Vector2(seen.x, seen.y) * side;
                if (Mathf.Abs(at.x) > edge.x || Mathf.Abs(at.y) > edge.y) continue;

                float size = Mathf.Clamp(star.Size * seen.z * 1.6f, 0.4f, 4f) * unit;
                float alpha = Mathf.Min(1f, 0.25f + seen.z * 1.4f) * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(seconds * 2.4f + star.Twinkle)));
                Color32 color = Fade(star.Color, alpha);
                if (warp && Project(star.Position, beforeDistance, near, out Vector3 trail))
                    starBatch.Line(at, new Vector2(trail.x, trail.y) * side, size, color);
                else
                    starBatch.Quad(at, new Vector2(size, size), color);
            }
        }

        // 은하: 세 팔 소용돌이 — 안쪽일수록 빨리 돈다
        private void DrawGalaxy(float seconds, float arrive, float distance, float near, float side, float unit)
        {
            glowBatch.Clear();
            galaxyBatch.Clear();
            float spin = seconds * SpinSpeed + SpinFly * (1f - arrive);
            int glow = 0;
            foreach (Particle particle in _galaxy)
            {
                float angle = particle.Angle + spin * (1.25f - particle.Radius / GalaxyRadius);
                var position = new Vector3(Mathf.Cos(angle) * particle.Radius, particle.Height, Mathf.Sin(angle) * particle.Radius);
                if (!Project(position, distance, near, out Vector3 seen)) continue;

                Vector2 at = new Vector2(seen.x, seen.y) * side;
                if (particle.Glow)
                {
                    float spread = 0.22f * seen.z * (1.4f - particle.Radius / GalaxyRadius) * side;
                    glowBatch.Quad(at, new Vector2(spread, spread), Fade(GlowColors[glow++ % GlowColors.Length], 0.16f * arrive));
                }

                float size = Mathf.Max(0.8f, particle.Size * seen.z * 2.6f) * unit;
                galaxyBatch.Quad(at, new Vector2(size, size), Fade(particle.Color, Mathf.Min(1f, particle.Alpha * (0.55f + seen.z * 1.1f))));
            }
        }

        // 밝은 중심 + 사방으로 뻗는 빛 내림 — 내려다보는 각만큼 눌린다
        private void DrawCore(float seconds, float arrive, float yaw, float distance, float near, float side)
        {
            if (!Project(Vector3.zero, distance, near, out Vector3 seen)) return;

            Vector2 center = new Vector2(seen.x, seen.y) * side;
            float radius = CoreRadius * seen.z * side;
            float flat = Mathf.Max(0.22f, Mathf.Abs(_tiltSin));
            // 시안 rotate(-yaw × 0.15) 는 화면 y 가 아래라 시계 방향 — 여기서는 부호가 반대
            float turn = yaw * 0.15f;

            Color32 inner = Fade(rayInner, arrive), outer = Fade(rayOuter, arrive);
            for (int i = 0; i < Rays; i++)
            {
                float angle = -(i * Tau / Rays + seconds * 0.08f);
                float length = radius * (1.6f + 0.5f * Mathf.Sin(i * 1.7f + seconds));
                galaxyBatch.Triangle(center, inner,
                    center + Turn(Oval(angle - RayHalfWidth, length, flat), turn), outer,
                    center + Turn(Oval(angle + RayHalfWidth, length, flat), turn), outer);
            }

            for (int stop = 1; stop < CoreStops.Length; stop++)
            {
                Color32 from = Fade(CoreColors[stop - 1], 0.9f * arrive), to = Fade(CoreColors[stop], 0.9f * arrive);
                float inside = radius * CoreStops[stop - 1], outside = radius * CoreStops[stop];
                for (int i = 0; i < CoreSegments; i++)
                {
                    float a = i * Tau / CoreSegments, b = (i + 1) * Tau / CoreSegments;
                    galaxyBatch.Quad(
                        center + Turn(Oval(a, inside, flat), turn), from, center + Turn(Oval(a, outside, flat), turn), to,
                        center + Turn(Oval(b, outside, flat), turn), to, center + Turn(Oval(b, inside, flat), turn), from);
                }
            }
        }

        // 시안 P3 — 은하 기준 자리가 화면 어디에 얼마나 크게 보이는지. seen = (화면 가운데 기준 x, y 위 +, 크기 배율). 카메라보다 뒤 · 너무 가까우면 false
        private bool Project(Vector3 position, float distance, float near, out Vector3 seen)
        {
            float x = position.x * _yawCos - position.z * _yawSin;
            float z = position.x * _yawSin + position.z * _yawCos;
            float y = position.y * _tiltCos - z * _tiltSin;
            float depth = position.y * _tiltSin + z * _tiltCos + distance;
            if (depth < near)
            {
                seen = default;
                return false;
            }

            float scale = Focal / depth;
            seen = new Vector3(x * scale, -y * scale, scale);
            return true;
        }

        private static Vector2 Oval(float angle, float radius, float flat) => new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * flat);

        private static Vector2 Turn(Vector2 point, float angle)
        {
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
        }

        private static Color32 White(float alpha) => new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));

        private static Color32 Fade(Color32 color, float alpha)
        {
            color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.a / 255f * alpha) * 255f);
            return color;
        }

        // 시안 mkNeb — 가운데로 몰린 빛 구름 여럿을 겹쳐 얼룩진 성운 한 칸을 만든다. 네 벌을 한 장(2 × 2)에 담는다
        private static Texture2D BuildNebulaTexture()
        {
            int size = NebulaTile * 2;
            var pixels = new Color[size * size];
            for (int tile = 0; tile < 4; tile++)
            {
                int left = tile % 2 * NebulaTile, bottom = tile / 2 * NebulaTile;
                float half = NebulaTile * 0.5f, scale = NebulaTile / 256f;
                for (int i = 0; i < NebulaBlobs; i++)
                {
                    float spread = 70f * (1f - i / 90f) * scale;
                    float x = half + Random.Range(-spread, spread), y = half + Random.Range(-spread, spread);
                    float radius = Random.Range(18f, 70f) * scale, strength = Random.Range(0.05f, 0.16f);
                    Color tint = NebulaColors[tile, i % 3 != 0 ? 0 : 1];
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(x - radius)), x1 = Mathf.Min(NebulaTile - 1, Mathf.CeilToInt(x + radius));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(y - radius)), y1 = Mathf.Min(NebulaTile - 1, Mathf.CeilToInt(y + radius));
                    for (int py = y0; py <= y1; py++)
                    {
                        for (int px = x0; px <= x1; px++)
                        {
                            float fall = 1f - Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y)) / radius;
                            if (fall <= 0f) continue;

                            // 캔버스 그라데이션(색 → 투명한 검정)을 그 위에 덮어 그린 것 — 여기서는 알파를 미리 곱한 색으로 쌓는다
                            float alpha = fall * strength;
                            int index = (bottom + py) * size + left + px;
                            Color under = pixels[index];
                            pixels[index] = new Color(
                                tint.r * fall * alpha + under.r * (1f - alpha), tint.g * fall * alpha + under.g * (1f - alpha),
                                tint.b * fall * alpha + under.b * (1f - alpha), alpha + under.a * (1f - alpha));
                        }
                    }
                }
            }

            // 미리 곱한 색 → 보통 색 (머티리얼이 알파를 곱해 더한다)
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.a > 0.0001f)
                    pixels[i] = new Color(pixel.r / pixel.a, pixel.g / pixel.a, pixel.b / pixel.a, pixel.a);
            }

            return NewTexture(size, pixels, "Domain Nebula");
        }

        // 시안 mkGlow — 가운데가 밝고 가장자리로 사라지는 둥근 빛
        private static Texture2D BuildGlowTexture()
        {
            var pixels = new Color[GlowSize * GlowSize];
            float half = (GlowSize - 1) * 0.5f;
            for (int y = 0; y < GlowSize; y++)
            {
                for (int x = 0; x < GlowSize; x++)
                {
                    float fall = Mathf.Max(0f, 1f - Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half);
                    pixels[y * GlowSize + x] = new Color(fall, fall, fall, fall);
                }
            }

            return NewTexture(GlowSize, pixels, "Domain Glow");
        }

        private static Texture2D NewTexture(int size, Color[] pixels, string textureName)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
