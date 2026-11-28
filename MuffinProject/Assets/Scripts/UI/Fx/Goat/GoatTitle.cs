using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Goat
{
    /// <summary>
    /// 「GOAT」 컷인의 제목 — 금빛 돌 글자 네 개가 하나씩 화면 밖에서 날아와 쿵 박힌다 (시안 goat_cutin.js 의 overlay 글자 부분).
    /// 글자 하나는 같은 글자 모양을 여러 번 겹쳐 그린다: 흐린 그림자 → 아래로 밀어낸 두께 → 둘레의 불빛 → 검은 테두리 → 밝은 모서리 → 금빛 면(세로 그라데이션) → 지나가는 반짝임.
    /// 박히는 순간에는 면이 하얗게 번쩍이고(0.32초) 돌 조각 · 먼지가 튀고, 그 뒤로 별빛이 가끔 반짝인다.
    /// 글자 모양은 글자 그림 한 장(<see cref="sheet"/>)에서 가져온다 — 게임 글꼴(21-font)이 아닌 컷인 전용 글자라 그림으로 둔다 (지옥참마도의 한자와 같다).
    /// 그림에는 줄이 셋이다: 또렷한 글자 · 흐린 글자(그림자) · 테두리만큼 부풀려 흐린 글자(불빛).
    /// 좌표는 시안 화면(1280 × 720) px — 이 오브젝트의 자리가 화면 왼쪽 위다.
    /// </summary>
    public class GoatTitle : MonoBehaviour
    {
        /// <summary>글자 수.</summary>
        public const int Letters = 4;

        // 시안 LQ: 첫 글자가 날아오기 시작하는 시각 · 글자 사이 · 날아오는 시간 (초)
        private const float FirstSlam = 5.7f, SlamGap = 0.44f, SlamSeconds = 0.13f;
        // 시안 글자: 글자 사이 · 넘으면 줄이는 폭 · 가운데 · 글자 밑선 (px)
        private const float LetterGap = 34f, FitWidth = 1080f, CenterX = 640f, Baseline = 272f;
        // 글자 폭 — 시안 글꼴(Noto Serif CJK JP Black, 270px)의 G · O · A · T. 글자 높이(밑선 위)
        private static readonly float[] Advances = { 206.82f, 214.65f, 205.47f, 192.78f };
        private const float InkHeight = 207f;
        // 글자 그림의 크기 (px) 와 세 줄 — 줄의 위 끝 · 한 칸의 폭 · 높이 (그림 px) · 칸 안에서 글자 시작점 왼쪽 · 밑선 위의 여백 (시안 px) · 시안 1px 이 그림 몇 px 인지
        private static readonly Vector2 SheetSize = new Vector2(1392f, 852f);
        private static readonly SheetRow SharpRow = new SheetRow(0f, 348f, 342f, 8f, 216f, 1.5f);
        private static readonly SheetRow ShadowRow = new SheetRow(342f, 255f, 255f, 60f, 270f, 0.75f);
        private static readonly SheetRow HeatRow = new SheetRow(597f, 255f, 255f, 60f, 270f, 0.75f);
        // 날아올 때의 크기 (3.2 → 1) · 박힌 직후 눌림(7%, 0.12초) · 박힐 때 흔들림(15px, 0.3초)
        private const float SlamScale = 3.2f, Squash = 0.07f, SquashSeconds = 0.12f, ShakePixels = 15f, ShakeSeconds = 0.3f;
        // 두께: 가장 아래 · 한 겹 간격 · 이보다 아래는 그늘 색 / 테두리: 굵기의 반 · 겹 수
        private const float Depth = 18f, DepthStep = 2f, DepthShadeBelow = 11f, OutlineRadius = 8f;
        private const int OutlineCopies = 10;
        // 그림자: 두께 맨 아래에서 더 아래로 (글자가 커져도 그대로) / 박힐 때 뒤에서 번지는 불빛: 평소 번짐 · 더 번지는 양 · 가라앉는 시간
        private const float ShadowDrop = 34f, HeatBlur = 14f, HeatBurst = 80f, HeatSeconds = 0.6f;
        // 면: 그라데이션 위 · 아래 (글자 밑선 기준, 위가 −) / 번쩍임 시간
        private const float FaceTop = -205f, FaceBottom = 4f, FlashSeconds = 0.32f;
        // 반짝임: 마지막 글자가 박히고 0.5초 뒤부터 1.5초마다, 0.6초 동안 왼쪽 밖(−220)에서 오른쪽 밖(1500)으로
        private const float ShineDelay = 0.5f, ShinePeriod = 1.5f, ShineSeconds = 0.6f, ShineFrom = -220f, ShineTo = 1500f;
        private const int ShineColumns = 14, ShineRows = 6;
        // 돌 조각: 글자마다 16개, 0.75초 / 먼지 줄 0.3초 / 별빛
        private const int Chips = 16;
        private const float ChipSeconds = 0.75f, ChipGravity = 900f, DustSeconds = 0.3f, SparkleSize = 46f;

        // 면 그라데이션을 나누는 자리 (글자 밑선 기준 px) — 금빛 여섯 색의 자리와 위 · 아래 물들임의 자리
        private static readonly float[] FaceStops = { -205f, -200f, -142.3f, -126.5f, -100.5f, -94.23f, -37.8f, 4f };
        private static readonly float[] GoldAt = { 0f, 0.3f, 0.5f, 0.53f, 0.8f, 1f };

        [Tooltip("글자 그림 — G · O · A · T 가 줄마다 같은 크기 칸에 나란히 든 한 장 (흰색, 알파가 모양). 위부터 또렷한 글자 · 그림자 · 불빛.")]
        [SerializeField] private Sprite sheet;
        [Tooltip("글자 (글자 그림을 입힌다).")]
        [SerializeField] private UIQuadBatch letters;
        [Tooltip("돌 조각 · 먼지 · 별빛.")]
        [SerializeField] private UIQuadBatch bits;
        [Tooltip("글자가 박힐 때 뒤에서 번지는 불빛 (글자마다 하나).")]
        [SerializeField] private Graphic[] bursts = new Graphic[0];

        [Header("색 (시안)")]
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private Color baseColor = new Color32(0x06, 0x04, 0x03, 255);
        [SerializeField] private Color depthDark = new Color32(0x07, 0x05, 0x04, 255);
        [SerializeField] private Color depthLit = new Color32(0x2c, 0x1d, 0x0c, 255);
        [SerializeField] private Color outlineColor = new Color32(0x08, 0x06, 0x05, 255);
        [SerializeField] private Color heatColor = new Color(1f, 110f / 255f, 36f / 255f, 0.9f);
        [SerializeField] private Color edgeColor = new Color(1f, 248f / 255f, 226f / 255f, 0.75f);
        [Tooltip("면의 금빛 — 위에서 아래로 0 · 30 · 50 · 53 · 80 · 100%.")]
        [SerializeField] private Color[] gold =
        {
            new Color32(0xff, 0xf7, 0xdf, 255), new Color32(0xe3, 0xc5, 0x7c, 255), new Color32(0xa1, 0x7f, 0x40, 255),
            new Color32(0xd2, 0xb0, 0x64, 255), new Color32(0x8a, 0x6a, 0x30, 255), new Color32(0x3e, 0x2c, 0x10, 255),
        };
        [Tooltip("면 위쪽에 얹는 달빛 · 아래쪽에 얹는 그늘 (알파 = 얹는 정도).")]
        [SerializeField] private Color faceCool = new Color(190f / 255f, 215f / 255f, 1f, 0.22f);
        [SerializeField] private Color faceWarm = new Color(12f / 255f, 6f / 255f, 0f, 0.42f);
        [SerializeField] private Color flashColor = new Color(1f, 247f / 255f, 220f / 255f, 0.85f);
        [SerializeField] private Color shineColor = new Color(1f, 252f / 255f, 240f / 255f, 0.95f);
        [SerializeField] private Color chipDark = new Color32(0x2a, 0x21, 0x18, 255);
        [SerializeField] private Color chipGold = new Color32(0xcd, 0xaa, 0x62, 255);
        [SerializeField] private Color dustColor = new Color32(0xe9, 0xd9, 0xb0, 255);
        [SerializeField] private Color sparkleColor = new Color32(0xff, 0xfd, 0xf0, 255);

        private readonly Glyph[] _glyphs = new Glyph[Letters];
        // 글자마다 돌 조각 16개: (방향, 빠르기, 처음 x — 글자 폭의 비율, 크기)
        private readonly Vector4[,] _chips = new Vector4[Letters, Chips];
        private float _fit;

        // 글자 그림의 한 줄
        private readonly struct SheetRow
        {
            public readonly float Top, CellWidth, CellHeight, PadLeft, Above, Scale;

            public SheetRow(float top, float cellWidth, float cellHeight, float padLeft, float above, float scale)
            {
                Top = top;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
                PadLeft = padLeft;
                Above = above;
                Scale = scale;
            }
        }

        // 글자 그림의 한 칸 — 놓이는 자리 (시안 px, 글자 가운데 · 밑선 기준, 아래가 +) 와 그 칸의 그림 좌표
        private struct Cell
        {
            public float Left, Right, Top, Bottom;
            public float UvLeft, UvRight, UvTop, UvBottom;
        }

        // 글자 하나 — 가운데 자리 · 폭 · 세 줄의 칸 (면 · 그림자 · 불빛)
        private struct Glyph
        {
            public float Center, Width;
            public Cell Face, Shadow, Heat;
        }

        // 이번 프레임의 글자 하나
        private struct Pose
        {
            public bool Shown, Landed;
            public float Since, Scale, Alpha;
            public Vector2 Origin;
        }

        /// <summary>i 번째 글자가 날아오기 시작하는 시각 (시안 LQ).</summary>
        public static float SlamTime(int index) => FirstSlam + index * SlamGap;

        /// <summary>i 번째 글자가 박히는 시각 — 화면이 흔들리고 소리가 난다.</summary>
        public static float LandTime(int index) => SlamTime(index) + SlamSeconds;

        private void Awake()
        {
            if (sheet == null || letters == null || bits == null || bursts.Length != Letters || gold.Length != GoldAt.Length)
                Debug.LogError($"[{nameof(GoatTitle)}] 참조 · 불빛 수 · 금빛 색 수 중 맞지 않는 것이 있습니다.", this);
        }

        /// <summary>글자 그림에서 글자마다의 칸을 잡고 자리를 정한다. 재생 전에 한 번.</summary>
        public void Prepare()
        {
            letters.SetTexture(sheet.texture);
            float total = LetterGap * (Letters - 1);
            for (int i = 0; i < Letters; i++)
            {
                float width = Advances[i];
                _glyphs[i] = new Glyph { Width = width, Face = CellOf(i, width, SharpRow), Shadow = CellOf(i, width, ShadowRow), Heat = CellOf(i, width, HeatRow) };
                total += width;
            }

            _fit = Mathf.Min(1f, FitWidth / total);
            float x = -total / 2f;
            for (int i = 0; i < Letters; i++)
            {
                _glyphs[i].Center = x + _glyphs[i].Width / 2f;
                x += _glyphs[i].Width + LetterGap;
            }

            // 돌 조각 — 시안 rng(91 + 글자 × 7), 조각마다 방향 → 빠르기 → 자리 → 크기 순으로 뽑는다
            for (int i = 0; i < Letters; i++)
            {
                uint seed = (uint)(91 + i * 7);
                float Next()
                {
                    seed = unchecked(seed * 1664525u + 1013904223u);
                    return (float)(seed / 4294967296.0);
                }

                for (int j = 0; j < Chips; j++)
                {
                    float angle = -Mathf.PI * (0.08f + Next() * 0.84f);
                    float speed = 260f + Next() * 520f;
                    float start = (Next() - 0.5f) * 0.8f;
                    _chips[i, j] = new Vector4(angle, speed, start, 4f + Next() * 9f);
                }
            }
        }

        /// <summary>시각 t(초)의 제목을 그린다.</summary>
        public void Draw(float t)
        {
            letters.Clear();
            bits.Clear();

            // 박힐 때마다 제목 전체가 흔들린다
            float shake = 0f;
            for (int i = 0; i < Letters; i++)
            {
                float since = t - LandTime(i);
                if (since >= 0f)
                    shake += ShakePixels * Mathf.Clamp01(1f - since / ShakeSeconds);
            }
            var origin = new Vector2(CenterX + Mathf.Sin(t * 97f) * shake, Baseline + Mathf.Cos(t * 113f) * shake);

            // 뒤(그림자 · 두께 · 테두리)를 모두 그린 다음 면을 그린다 — 옆 글자의 테두리가 면을 덮지 않게
            for (int i = 0; i < Letters; i++)
                DrawBack(i, PoseOf(i, t, origin));
            for (int i = 0; i < Letters; i++)
                DrawFace(i, PoseOf(i, t, origin), t);
            for (int i = 0; i < Letters; i++)
                DrawBits(i, PoseOf(i, t, origin), t);

            letters.Apply();
            bits.Apply();
        }

        private Pose PoseOf(int index, float t, Vector2 origin)
        {
            float flight = Mathf.InverseLerp(SlamTime(index), LandTime(index), t);
            float since = t - LandTime(index);
            bool landed = since >= 0f;
            float press = Mathf.Clamp01(since / SquashSeconds);
            return new Pose
            {
                Shown = flight > 0f,
                Landed = landed,
                Since = since,
                Scale = landed ? 1f - Squash * (1f - press) * Mathf.Sin(press * Mathf.PI) : SlamScale - (SlamScale - 1f) * flight * flight,
                Alpha = landed ? 1f : Mathf.Clamp01(flight * 1.6f),
                Origin = origin,
            };
        }

        private void DrawBack(int index, Pose pose)
        {
            RectTransform burst = bursts[index].rectTransform;
            float heat = pose.Landed ? Mathf.Clamp01(1f - pose.Since / HeatSeconds) : 0f;
            bursts[index].canvasRenderer.SetAlpha(heat);
            if (!pose.Shown)
                return;

            Glyph g = _glyphs[index];
            // 박힐 때 뒤에서 번지는 불빛 — 글자보다 조금 크게, 가라앉으며 줄어든다
            float spread = HeatBlur + HeatBurst * heat;
            burst.anchoredPosition = ToLocal(pose, g, new Vector2(0f, -InkHeight / 2f));
            burst.sizeDelta = new Vector2(g.Width, InkHeight) * _fit + Vector2.one * spread * 2f;

            // 그림자: 두께의 맨 아래에서 더 아래로 떨어진 흐린 글자
            Stamp(g, g.Shadow, pose, new Vector2(0f, Depth), new Vector2(0f, ShadowDrop), Fade(shadowColor, pose.Alpha));

            // 두께: 아래에서 위로 한 겹씩
            Stamp(g, g.Face, pose, new Vector2(0f, Depth), Vector2.zero, Fade(baseColor, pose.Alpha));
            for (float depth = Depth; depth >= 2f; depth -= DepthStep)
                Stamp(g, g.Face, pose, new Vector2(0f, depth), Vector2.zero, Fade(depth > DepthShadeBelow ? depthDark : depthLit, pose.Alpha));

            // 박힌 뒤로는 테두리 둘레에 불빛이 남는다
            if (pose.Landed)
                Stamp(g, g.Heat, pose, Vector2.zero, Vector2.zero, heatColor);

            for (int i = 0; i < OutlineCopies; i++)
                Stamp(g, g.Face, pose, Around(i, OutlineCopies, OutlineRadius, 0f), Vector2.zero, Fade(outlineColor, pose.Alpha));
        }

        private void DrawFace(int index, Pose pose, float t)
        {
            if (!pose.Shown)
                return;

            Glyph g = _glyphs[index];
            // 밝은 모서리: 면보다 살짝 크게 · 위로 — 면 둘레에 가는 빛 줄로 남는다
            for (int i = 0; i < 4; i++)
                Stamp(g, g.Face, pose, new Vector2(0f, -1.5f) + Around(i, 4, 1.5f, 0.5f), Vector2.zero, Fade(edgeColor, 0.8f * pose.Alpha));

            // 면: 금빛 세로 그라데이션 — 그라데이션이 꺾이는 자리마다 가로로 잘라 색을 준다. 박힌 직후에는 하얗게 번쩍인다
            float flash = pose.Landed && pose.Since < FlashSeconds ? flashColor.a * (1f - pose.Since / FlashSeconds) : 0f;
            float from = g.Face.Top;
            Color32 fromColor = FaceColor(from, flash, pose.Alpha);
            for (int i = 0; i <= FaceStops.Length; i++)
            {
                float to = i < FaceStops.Length ? Mathf.Clamp(FaceStops[i], g.Face.Top, g.Face.Bottom) : g.Face.Bottom;
                if (to <= from)
                    continue;

                Color32 toColor = FaceColor(to, flash, pose.Alpha);
                Band(g, pose, from, to, fromColor, toColor);
                from = to;
                fromColor = toColor;
            }

            DrawShine(g, pose, t);
        }

        // 글자 위를 비스듬히 지나가는 반짝임 — 글자 그림을 잘게 나눠 꼭짓점마다 밝기를 준다
        private void DrawShine(Glyph g, Pose pose, float t)
        {
            float start = SlamTime(Letters - 1) + ShineDelay;
            if (t <= start || !pose.Landed)
                return;

            float phase = (t - start) % ShinePeriod / ShineSeconds;
            if (phase >= 1f)
                return;

            // 시안: (sweep − 150, 밑선 − 200) 에서 (sweep + 150, 밑선 + 60) 으로 가는 그라데이션
            float sweep = Mathf.Lerp(ShineFrom, ShineTo, phase);
            var axisStart = new Vector2(sweep - 150f, pose.Origin.y - 200f);
            var axis = new Vector2(300f, 260f);
            float Light(Vector2 canvas)
            {
                float k = Vector2.Dot(canvas - axisStart, axis) / axis.sqrMagnitude;
                // 0 → 0.42 흐림(0.25) → 0.5 가장 밝음 → 0.58 흐림 → 1 에서 0
                float away = Mathf.Abs(k - 0.5f);
                if (away >= 0.5f)
                    return 0f;
                return away < 0.08f ? Mathf.Lerp(1f, 0.26f, away / 0.08f) : Mathf.Lerp(0.26f, 0f, (away - 0.08f) / 0.42f);
            }

            for (int row = 0; row < ShineRows; row++)
            {
                float v0 = (float)row / ShineRows, v1 = (float)(row + 1) / ShineRows;
                float y0 = Mathf.Lerp(g.Face.Top, g.Face.Bottom, v0), y1 = Mathf.Lerp(g.Face.Top, g.Face.Bottom, v1);
                for (int column = 0; column < ShineColumns; column++)
                {
                    float u0 = (float)column / ShineColumns, u1 = (float)(column + 1) / ShineColumns;
                    float x0 = Mathf.Lerp(g.Face.Left, g.Face.Right, u0), x1 = Mathf.Lerp(g.Face.Left, g.Face.Right, u1);
                    Vector2 bottomLeft = ToCanvas(pose, g, new Vector2(x0, y1)), topLeft = ToCanvas(pose, g, new Vector2(x0, y0));
                    Vector2 topRight = ToCanvas(pose, g, new Vector2(x1, y0)), bottomRight = ToCanvas(pose, g, new Vector2(x1, y1));
                    float a = Light(bottomLeft), b = Light(topLeft), c = Light(topRight), d = Light(bottomRight);
                    if (a <= 0f && b <= 0f && c <= 0f && d <= 0f)
                        continue;

                    letters.Quad(Flip(bottomLeft), Uv(g.Face, u0, v1), Fade(shineColor, a), Flip(topLeft), Uv(g.Face, u0, v0), Fade(shineColor, b),
                        Flip(topRight), Uv(g.Face, u1, v0), Fade(shineColor, c), Flip(bottomRight), Uv(g.Face, u1, v1), Fade(shineColor, d));
                }
            }
        }

        // 박힌 자리에서 튀는 돌 조각 · 옆으로 퍼지는 먼지 줄 · 가끔 반짝이는 별빛
        private void DrawBits(int index, Pose pose, float t)
        {
            if (!pose.Landed)
                return;

            Glyph g = _glyphs[index];
            float since = pose.Since;
            Vector2 Bit(float x, float y) => Flip(pose.Origin + new Vector2(g.Center + x, y) * _fit);

            if (since < ChipSeconds)
            {
                float alpha = Mathf.Clamp01(1f - since / ChipSeconds);
                for (int j = 0; j < Chips; j++)
                {
                    Vector4 chip = _chips[index, j];
                    float x = chip.z * g.Width + Mathf.Cos(chip.x) * chip.y * since;
                    float y = -10f + Mathf.Sin(chip.x) * chip.y * since + ChipGravity * since * since;
                    // 네모의 위쪽 끝이 (y − 크기 / 2), 높이는 크기의 0.7
                    bits.Quad(Bit(x, y - chip.w * 0.15f), new Vector2(chip.w, chip.w * 0.7f) * _fit, Fade(j % 3 != 0 ? chipDark : chipGold, alpha));
                }

                float dust = Mathf.Clamp01(1f - since / DustSeconds);
                if (dust > 0f)
                    bits.Quad(Bit(0f, 8f + 2.5f * dust), new Vector2(g.Width * 1.5f * (1f + since * 3f), 5f * dust) * _fit, Fade(dustColor, 0.5f * dust));
            }

            float twinkle = Mathf.Max(0f, Mathf.Sin((t - SlamTime(index)) * 3.3f + index * 1.9f));
            if (since > 0.3f && twinkle > 0.55f)
            {
                // 네 갈래 별 — 글자 왼쪽 위 · 오른쪽 위에 번갈아
                float size = (twinkle - 0.55f) / 0.45f;
                float radius = SparkleSize * size, waist = radius * 0.12f;
                bool right = index % 2 == 1;
                float x = (right ? 0.34f : -0.36f) * g.Width, y = right ? -178f : -150f;
                Color32 color = Fade(sparkleColor, size);
                Vector2 center = Bit(x, y);
                bits.Quad(Bit(x, y - radius), color, Bit(x + waist, y - waist), color, center, color, Bit(x - waist, y - waist), color);
                bits.Quad(Bit(x + radius, y), color, Bit(x + waist, y + waist), color, center, color, Bit(x + waist, y - waist), color);
                bits.Quad(Bit(x, y + radius), color, Bit(x - waist, y + waist), color, center, color, Bit(x + waist, y + waist), color);
                bits.Quad(Bit(x - radius, y), color, Bit(x - waist, y - waist), color, center, color, Bit(x - waist, y + waist), color);
            }
        }

        // 글자 그림의 한 칸을 통째로 한 장. offset = 글자와 같이 커지는 어긋남, nudge = 화면 px 그대로의 어긋남
        private void Stamp(Glyph g, Cell cell, Pose pose, Vector2 offset, Vector2 nudge, Color32 color)
        {
            letters.Quad(Flip(ToCanvas(pose, g, new Vector2(cell.Left, cell.Bottom) + offset) + nudge), Uv(cell, 0f, 1f), color,
                Flip(ToCanvas(pose, g, new Vector2(cell.Left, cell.Top) + offset) + nudge), Uv(cell, 0f, 0f), color,
                Flip(ToCanvas(pose, g, new Vector2(cell.Right, cell.Top) + offset) + nudge), Uv(cell, 1f, 0f), color,
                Flip(ToCanvas(pose, g, new Vector2(cell.Right, cell.Bottom) + offset) + nudge), Uv(cell, 1f, 1f), color);
        }

        // 글자 그림의 가로 띠 하나 (from 위 · to 아래, 글자 밑선 기준) — 위 · 아래 색을 잇는다
        private void Band(Glyph g, Pose pose, float from, float to, Color32 fromColor, Color32 toColor)
        {
            Cell face = g.Face;
            float v0 = Mathf.InverseLerp(face.Top, face.Bottom, from), v1 = Mathf.InverseLerp(face.Top, face.Bottom, to);
            letters.Quad(Flip(ToCanvas(pose, g, new Vector2(face.Left, to))), Uv(face, 0f, v1), toColor,
                Flip(ToCanvas(pose, g, new Vector2(face.Left, from))), Uv(face, 0f, v0), fromColor,
                Flip(ToCanvas(pose, g, new Vector2(face.Right, from))), Uv(face, 1f, v0), fromColor,
                Flip(ToCanvas(pose, g, new Vector2(face.Right, to))), Uv(face, 1f, v1), toColor);
        }

        // 면의 색 (글자 밑선 기준 높이 y): 금빛 → 위는 달빛 · 아래는 그늘을 얹고 → 번쩍임
        private Color32 FaceColor(float y, float flash, float alpha)
        {
            float k = Mathf.InverseLerp(FaceTop, FaceBottom, y);
            Color color = gold[gold.Length - 1];
            for (int i = 1; i < GoldAt.Length; i++)
            {
                if (k > GoldAt[i])
                    continue;
                color = Color.Lerp(gold[i - 1], gold[i], Mathf.InverseLerp(GoldAt[i - 1], GoldAt[i], k));
                break;
            }

            // 시안: 밑선 − 200 (달빛) → 35% 에서 0 → 밑선 + 10 (그늘)
            float tint = Mathf.InverseLerp(-200f, 10f, y);
            color = tint < 0.35f
                ? Color.Lerp(color, faceCool, faceCool.a * (1f - tint / 0.35f))
                : Color.Lerp(color, faceWarm, faceWarm.a * ((tint - 0.35f) / 0.65f));
            color = Color.Lerp(color, flashColor, flash);
            color.a = alpha;
            return color;
        }

        // 글자 안의 자리(글자 가운데 · 밑선 기준, 아래가 +) → 화면 px (왼쪽 위 기준, 아래가 +)
        private Vector2 ToCanvas(Pose pose, Glyph g, Vector2 point) => pose.Origin + new Vector2(g.Center + pose.Scale * point.x, pose.Scale * point.y) * _fit;

        private Vector2 ToLocal(Pose pose, Glyph g, Vector2 point) => Flip(ToCanvas(pose, g, point));

        // 화면 px (아래가 +) → 이 오브젝트 안의 자리 (위가 +)
        private static Vector2 Flip(Vector2 canvas) => new Vector2(canvas.x, -canvas.y);

        // 글자 그림에서 index 번째 글자의 한 칸 — 줄마다 칸 크기 · 여백 · 배율이 다르다
        private Cell CellOf(int index, float width, SheetRow row)
        {
            Vector4 uv = UIQuadBatch.SpriteUv(sheet);
            float left = -width / 2f - row.PadLeft;
            return new Cell
            {
                Left = left,
                Right = left + row.CellWidth / row.Scale,
                Top = -row.Above,
                Bottom = -row.Above + row.CellHeight / row.Scale,
                UvLeft = Mathf.Lerp(uv.x, uv.z, index * row.CellWidth / SheetSize.x),
                UvRight = Mathf.Lerp(uv.x, uv.z, (index + 1) * row.CellWidth / SheetSize.x),
                UvTop = Mathf.Lerp(uv.w, uv.y, row.Top / SheetSize.y),
                UvBottom = Mathf.Lerp(uv.w, uv.y, (row.Top + row.CellHeight) / SheetSize.y),
            };
        }

        // 칸 안의 자리 (u 왼쪽 0 → 오른쪽 1, v 위 0 → 아래 1) 의 그림 좌표
        private static Vector2 Uv(Cell cell, float u, float v) => new Vector2(Mathf.Lerp(cell.UvLeft, cell.UvRight, u), Mathf.Lerp(cell.UvTop, cell.UvBottom, v));

        // 둘레의 count 개 중 index 번째 방향으로 radius 만큼 (turn = 첫 방향을 돌리는 양, 한 칸 = 1)
        private static Vector2 Around(int index, int count, float radius, float turn)
        {
            float angle = (index + turn) * Mathf.PI * 2f / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Color32 Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);
    }
}
