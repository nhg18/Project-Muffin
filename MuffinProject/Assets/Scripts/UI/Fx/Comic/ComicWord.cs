using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 글자 — 굵은 먹 테 · 안쪽 색 테 · 입체(아래로 쌓인 그림자 색) · 흐린 그림자 · 윗부분 광택을 두른 제목 · 효과음 글자 (시안 word(txt, {size, fill, stroke, sw, inner, ext, extCol, gap, jit, seed, shadow, shine})).
    /// TMP SDF 글꼴 그림을 읽어 겹마다 다른 굵기로 그린다 — 머티리얼은 ComicWord (셰이더 Chapchu/UI Comic Word). 글꼴은 프로젝트 글꼴(Jua SDF).
    /// 기준점 = 이 RectTransform 의 피벗 = 시안 글자 그림의 (가운데, 위에서 pad + 0.55·size) — 시안 stamp 가 그 점을 (x, y) 에 놓는 것과 같다.
    /// 등장 움직임(튀어나오며 찍히기)은 같은 오브젝트의 <see cref="ComicStamp"/>. 투명도 = Graphic.color.a.
    /// 시안 fill 의 위아래 두 색 그라데이션은 시안 캔버스에서 실제로는 위 색 하나로 칠해진다 (그라데이션 좌표가 글자마다 옮겨진 판 기준이라) — 보이는 그대로 한 색(<see cref="Fill"/>)만 둔다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ComicWord : MaskableGraphic
    {
        // 시안 word(): 그림 여백 = 테 + 입체 + size·0.2, 글자 줄 = 여백 + size·0.95, 기준점 = 여백 + size·0.55, 그림 높이 = size·1.25 + 여백·2
        private const float PadRatio = 0.2f;
        private const float BaselineRatio = 0.95f;
        private const float AnchorRatio = 0.55f;
        private const float HeightRatio = 1.25f;
        // 시안 word(): 그림자 = 입체 + (size·0.05, size·0.07) 어긋남
        private const float ShadowShiftX = 0.05f;
        private const float ShadowShiftY = 0.07f;
        // 시안 word(): 입체는 depth 부터 max(1, size·0.012) 씩 줄이며 (d·0.55, d) 에 겹겹이
        private const float DepthStepRatio = 0.012f;
        private const float DepthStepMin = 1f;
        private const float DepthSlant = 0.55f;
        // 시안 word(): 안쪽 테 두께 = 테 × 0.9 의 반
        private const float InnerRatio = 0.45f;
        // 시안 word(): 광택 = 글자 줄에서 size·0.5 위까지, 흰색 0.45
        private const float ShineLineRatio = 0.5f;
        private const float ShineAmount = 0.45f;
        // 시안 word(): 글자마다 기울기 (R − 0.5)·jit·0.3, 높낮이 (R − 0.5)·jit·size·0.12, 씨앗 = 글자 수·31 + seed
        private const float JitterAngle = 0.3f;
        private const float JitterRise = 0.12f;
        private const int SeedStride = 31;
        // SDF 경계 = 0.5 (글자 테두리), 바깥으로 1 텍셀마다 1 / (2·GradientScale) 씩 줄어든다
        private const float SdfEdge = 0.5f;
        private const float SdfFloor = 0.01f;

        [Tooltip("글자.")]
        [SerializeField] private string text = "왈!!";
        [Tooltip("글꼴 (TMP SDF, 동적 — 없는 글자는 실행 때 더한다).")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("글자 크기 (설계 px, 시안 size).")]
        [SerializeField] private float size = 160f;
        [Tooltip("글자 면 색 (시안 fill 의 위 색).")]
        [SerializeField] private Color fill = Color.white;
        [Tooltip("바깥 테 색 (시안 stroke).")]
        [SerializeField] private Color stroke = ComicKit.Ink;
        [Tooltip("바깥 테 두께 / 글자 크기 (시안 sw 0.17).")]
        [SerializeField] private float strokeWidth = 0.17f;
        [Tooltip("안쪽 색 테를 두른다 (시안 inner).")]
        [SerializeField] private bool inner = true;
        [Tooltip("안쪽 테 색 (시안 inner).")]
        [SerializeField] private Color innerColor = new Color32(0xff, 0x8a, 0x1f, 0xff);
        [Tooltip("입체 깊이 / 글자 크기 (시안 ext 0.09).")]
        [SerializeField] private float depthRatio = 0.09f;
        [Tooltip("입체 색 (시안 extCol).")]
        [SerializeField] private Color depthColor = new Color32(0x7a, 0x2b, 0x00, 0xff);
        [Tooltip("글자 사이 / 글자 크기 (시안 gap 0.02).")]
        [SerializeField] private float gap = 0.02f;
        [Tooltip("글자마다 기울기 · 높낮이가 들쭉날쭉한 정도 (시안 jit).")]
        [SerializeField] private float jitter;
        [Tooltip("들쭉날쭉 씨앗 (시안 seed).")]
        [SerializeField] private int seed = 1;
        [Tooltip("흐린 그림자를 깐다 (시안 shadow).")]
        [SerializeField] private bool shadow = true;
        [Tooltip("그림자 색 · 진하기 (시안 shCol · 0.35).")]
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
        [Tooltip("윗부분을 밝게 (시안 shine).")]
        [SerializeField] private bool shine = true;

        private readonly List<Glyph> _glyphs = new List<Glyph>();
        private readonly HashSet<uint> _missing = new HashSet<uint>();
        private readonly List<float> _advances = new List<float>();
        private readonly List<Vector2> _jitters = new List<Vector2>();
        private readonly List<Corner> _quad = new List<Corner>();
        private readonly List<Corner> _above = new List<Corner>();
        private readonly List<Corner> _below = new List<Corner>();
        private float _total;
        private Texture _atlas;

        private struct Corner
        {
            public Vector2 Position;
            public Vector2 Uv;
        }

        /// <summary>글자. 바뀌면 다시 그린다.</summary>
        public string Text
        {
            get => text;
            set
            {
                if (text == value)
                    return;
                text = value;
                _missing.Clear();
                AddMissingCharacters();
                SetAllDirty();
            }
        }

        /// <summary>글자 면 색.</summary>
        public Color Fill
        {
            get => fill;
            set => SetColor(ref fill, value);
        }

        /// <summary>안쪽 테 색 (시안 inner).</summary>
        public Color InnerColor
        {
            get => innerColor;
            set => SetColor(ref innerColor, value);
        }

        /// <summary>입체 색 (시안 extCol).</summary>
        public Color DepthColor
        {
            get => depthColor;
            set => SetColor(ref depthColor, value);
        }

        /// <summary>글자 크기 (설계 px).</summary>
        public float Size => size;

        /// <summary>시안 글자 그림의 너비 (설계 px) — 글자 폭 합 + 여백 × 2 (시안 sprite.width). 제목 띠 길이 등에 쓴다.</summary>
        public float SpriteWidth
        {
            get
            {
                Measure();
                return _total + Pad * 2f;
            }
        }

        /// <summary>시안 글자 그림의 높이 (설계 px) — size·1.25 + 여백 × 2 (시안 sprite.height).</summary>
        public float SpriteHeight => size * HeightRatio + Pad * 2f;

        public override Texture mainTexture
        {
            get
            {
                Measure();
                return _atlas != null ? _atlas : s_WhiteTexture;
            }
        }

        private float StrokeRadius => strokeWidth * size;

        private float DepthLength => depthRatio * size;

        private float Pad => StrokeRadius + DepthLength + size * PadRatio;

        protected override void OnEnable()
        {
            AddMissingCharacters();
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Measure();
            if (font == null || _glyphs.Count == 0 || _atlas == null)
                return;

            float scale = size / font.faceInfo.pointSize;
            float sw = StrokeRadius, ext = DepthLength;
            float anchorX = (_total + Pad * 2f) / 2f, anchorY = Pad + size * AnchorRatio;
            float baseline = Pad + size * BaselineRatio - anchorY;
            float shineLine = Pad + size * BaselineRatio - size * ShineLineRatio - anchorY;

            float x = Pad;
            for (int i = 0; i < _glyphs.Count; i++)
            {
                Glyph glyph = _glyphs[i];
                float advance = _advances[i];
                if (glyph != null && glyph.glyphRect.width > 0 && glyph.glyphRect.height > 0)
                {
                    var origin = new Vector2(x + advance / 2f - anchorX, baseline + _jitters[i].y);
                    float angle = _jitters[i].x;
                    if (shadow)
                        Layer(vh, glyph, origin, angle, new Vector2(ext + size * ShadowShiftX, ext + size * ShadowShiftY), sw, shadowColor, scale, shineLine);
                    float step = Mathf.Max(DepthStepMin, size * DepthStepRatio);
                    for (float d = ext; d > 0f; d -= step)
                        Layer(vh, glyph, origin, angle, new Vector2(d * DepthSlant, d), sw, depthColor, scale, shineLine);
                    Layer(vh, glyph, origin, angle, Vector2.zero, sw, stroke, scale, shineLine);
                    if (inner)
                        Layer(vh, glyph, origin, angle, Vector2.zero, sw * InnerRatio, innerColor, scale, shineLine);
                    Layer(vh, glyph, origin, angle, Vector2.zero, 0f, fill, scale, shineLine);
                }
                x += advance + gap * size;
            }
        }

        // 글자 하나의 한 겹 — origin (글자 줄 가운데, 아래로 +) 에서 angle 만큼 돌린 판의 offset 자리에, 바깥으로 dilate 만큼 두껍게
        private void Layer(VertexHelper vh, Glyph glyph, Vector2 origin, float angle, Vector2 offset, float dilate, Color layerColor,
            float scale, float shineLine)
        {
            GlyphMetrics m = glyph.metrics;
            GlyphRect r = glyph.glyphRect;
            float padding = font.atlasPadding;
            float left = -m.horizontalAdvance / 2f + m.horizontalBearingX - padding, top = -m.horizontalBearingY - padding;
            float right = left + m.width + padding * 2f, bottom = top + m.height + padding * 2f;
            float aw = _atlas.width, ah = _atlas.height;
            float u0 = (r.x - padding) / aw, u1 = (r.x + r.width + padding) / aw;
            float v0 = (r.y + r.height + padding) / ah, v1 = (r.y - padding) / ah;

            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            _quad.Clear();
            _quad.Add(MakeCorner(left, top, u0, v0));
            _quad.Add(MakeCorner(right, top, u1, v0));
            _quad.Add(MakeCorner(right, bottom, u1, v1));
            _quad.Add(MakeCorner(left, bottom, u0, v1));
            for (int i = 0; i < _quad.Count; i++)
            {
                Corner c = _quad[i];
                // 글자 그림 px → 설계 px, 겹 어긋남, 글자 기울기, 글자 자리 (시안 each: translate · rotate 후 strokeText(ch, dx, dy))
                Vector2 p = c.Position * scale + offset;
                c.Position = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos) + origin;
                _quad[i] = c;
            }

            int gradientScale = font.atlasPadding + 1;
            float edge = Mathf.Max(SdfFloor, SdfEdge - dilate / scale / (2f * gradientScale));
            Color tinted = layerColor * color;
            if (!shine)
            {
                Emit(vh, _quad, tinted, edge);
                return;
            }
            Split(_quad, shineLine, _above, _below);
            Color shined = Color.Lerp(layerColor, Color.white, ShineAmount);
            shined.a = layerColor.a;
            Emit(vh, _above, shined * color, edge);
            Emit(vh, _below, tinted, edge);
        }

        private static Corner MakeCorner(float x, float y, float u, float v)
        {
            return new Corner { Position = new Vector2(x, y), Uv = new Vector2(u, v) };
        }

        // 볼록 다각형을 가로줄 y = line 으로 나눈다 (아래로 + — above 는 y < line)
        private static void Split(List<Corner> polygon, float line, List<Corner> above, List<Corner> below)
        {
            above.Clear();
            below.Clear();
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Corner a = polygon[i], b = polygon[(i + 1) % n];
                float fa = a.Position.y - line, fb = b.Position.y - line;
                if (fa <= 0f)
                    above.Add(a);
                if (fa >= 0f)
                    below.Add(a);
                if ((fa < 0f && fb > 0f) || (fa > 0f && fb < 0f))
                {
                    float t = fa / (fa - fb);
                    var cross = new Corner { Position = Vector2.Lerp(a.Position, b.Position, t), Uv = Vector2.Lerp(a.Uv, b.Uv, t) };
                    above.Add(cross);
                    below.Add(cross);
                }
            }
        }

        private static void Emit(VertexHelper vh, List<Corner> polygon, Color c, float edge)
        {
            if (polygon.Count < 3)
                return;
            Color32 c32 = c;
            int start = vh.currentVertCount;
            foreach (Corner corner in polygon)
                vh.AddVert(new Vector3(corner.Position.x, -corner.Position.y), c32, new Vector4(corner.Uv.x, corner.Uv.y, edge, 0f));
            for (int i = 1; i < polygon.Count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }

        // 글자마다 글꼴 그림 · 폭 · 들쭉날쭉을 다시 잰다
        private void Measure()
        {
            _glyphs.Clear();
            _advances.Clear();
            _jitters.Clear();
            _total = 0f;
            _atlas = null;
            if (font == null || string.IsNullOrEmpty(text))
                return;
            float scale = size / font.faceInfo.pointSize;
            var random = new ComicRandom(text.Length * SeedStride + (seed == 0 ? 1 : seed));
            int atlasIndex = -1;
            for (int i = 0; i < text.Length; i++)
            {
                uint unicode = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length)
                {
                    unicode = (uint)char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                Glyph glyph = null;
                float advance = 0f;
                if (TryGetCharacter(unicode, out TMP_Character character))
                {
                    glyph = character.glyph;
                    advance = glyph.metrics.horizontalAdvance * scale;
                    if (glyph.glyphRect.width > 0)
                    {
                        if (atlasIndex < 0)
                            atlasIndex = glyph.atlasIndex;
                        else if (glyph.atlasIndex != atlasIndex)
                            glyph = null; // 다른 글꼴 그림에 든 글자 — 한 Graphic 은 그림 하나만 쓴다
                    }
                }
                _glyphs.Add(glyph);
                _advances.Add(advance);
                float angle = (random.Next() - 0.5f) * jitter * JitterAngle;
                float rise = (random.Next() - 0.5f) * jitter * size * JitterRise;
                _jitters.Add(new Vector2(angle, rise));
            }
            for (int i = 0; i < _advances.Count; i++)
                _total += _advances[i];
            _total += gap * size * Mathf.Max(0, _advances.Count - 1);
            if (atlasIndex >= 0 && atlasIndex < font.atlasTextures.Length)
                _atlas = font.atlasTextures[atlasIndex];
        }

        // 글꼴 그림에 없는 글자는 (동적 글꼴이면) 한 번 더해 본다 — 그래도 없으면 다시 묻지 않는다
        private bool TryGetCharacter(uint unicode, out TMP_Character character)
        {
            if (font.characterLookupTable.TryGetValue(unicode, out character) && character.glyph != null)
                return true;
            if (font.atlasPopulationMode == AtlasPopulationMode.Static || !_missing.Add(unicode))
                return false;
            font.TryAddCharacters(char.ConvertFromUtf32((int)unicode), out string _);
            return font.characterLookupTable.TryGetValue(unicode, out character) && character.glyph != null;
        }

        private void AddMissingCharacters()
        {
            if (font == null || string.IsNullOrEmpty(text) || font.atlasPopulationMode == AtlasPopulationMode.Static)
                return;
            font.TryAddCharacters(text, out string _);
        }

        private void SetColor(ref Color field, Color value)
        {
            if (field == value)
                return;
            field = value;
            SetVerticesDirty();
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
