using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 컷인 끝의 큰 글자 — 시안 NUKE3D .nk-name.
    /// 대 · 소 · 동 이 위에서 묵직하게 내려와 '쿵' (마지막 글자가 가장 크게) — 쿵마다 글자 밑에서 고리가 퍼지고 컷인이 흔들리고 밑줄이 1/3 씩 자란다.
    /// 다 놓이면 큰 글자 전체가 천천히 다가오고, 작은 설명 줄이 올라온다. 시각(ms, 글자 시작 기준)으로 모습을 정하므로 어느 순간이든 같은 그림이다.
    /// </summary>
    public class NukeTitleFx : MonoBehaviour
    {
        private const int LetterCount = 3;
        private const int GradientTextureHeight = 64;
        // 쿵 하는 순간 글자의 가로 늘어남 · 내려앉음 (em) — 시안 키프레임 .34
        private const float LandStretch = 1.04f;
        private const float LandDrop = 0.035f;

        private static readonly int FaceTexId = Shader.PropertyToID("_FaceTex");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

        // 시안 쿵 흔들림 키 (가로 반, 세로 번갈아)
        private static readonly float[] ShakeKeys = { 0f, -1f, 0.8f, -0.55f, 0.35f, -0.15f, 0f };

        [Header("그림")]
        [Tooltip("글자판 전체의 투명도.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("글자판 (화면 가운데, 가로 꽉).")]
        [SerializeField] private RectTransform block;
        [Tooltip("글자 뒤 어두운 타원 (시안 radial-gradient).")]
        [SerializeField] private UIRadialGradient backdrop;
        [Tooltip("고리를 담는 판 — 큰 글자 줄과 같은 자리 · 크기 (글자 뒤, 줄이 커질 때 같이 커지지 않는다).")]
        [SerializeField] private RectTransform ringLayer;
        [Tooltip("쿵 할 때 퍼지는 고리 — 대 · 소 · 동 · 동(두 번째).")]
        [SerializeField] private Image[] rings;
        [Tooltip("동 이 쿵 할 때 글자 둘레가 번쩍이는 빛.")]
        [SerializeField] private Image flash;
        [Tooltip("큰 글자 줄 — 다 놓인 뒤 천천히 커진다.")]
        [SerializeField] private RectTransform row;
        [Tooltip("대 · 소 · 동.")]
        [SerializeField] private TextMeshProUGUI[] letters;
        [Tooltip("밑줄.")]
        [SerializeField] private RectTransform underline;
        [Tooltip("밑줄 둘레의 초록 빛.")]
        [SerializeField] private UIShape underlineGlow;
        [Tooltip("작은 설명 줄 전체 (투명도 · 올라오기).")]
        [SerializeField] private CanvasGroup subtitle;
        [Tooltip("설명 줄 왼쪽 글 (게임 리셋).")]
        [SerializeField] private TextMeshProUGUI subtitleLeft;
        [Tooltip("설명 줄 가운데 세로 선.")]
        [SerializeField] private Image subtitleBar;
        [Tooltip("설명 줄 오른쪽 글 (매 턴 피폭 1 · 2 · 3 …).")]
        [SerializeField] private TextMeshProUGUI subtitleRight;

        [Header("글자 색")]
        [Tooltip("큰 글자 아래(0) → 위(1) 색 — 글자 칸 기준 (시안 #fdfff0 18% · #dcff5e 52% · #86d81f 92%, 줄 칸 기준).")]
        [SerializeField] private Gradient letterGradient = new Gradient();

        [Header("크기 (시안 CSS)")]
        [Tooltip("큰 글자 크기 = 화면 짧은 변 × 이 값 (23vmin).")]
        [SerializeField] private float letterVmin = 0.23f;
        [Tooltip("큰 글자 크기 하한 · 상한 (시안 px).")]
        [SerializeField] private Vector2 letterClamp = new Vector2(70f, 290f);
        [Tooltip("글자 사이 (em) · 글자 좌우 여백 (em).")]
        [SerializeField] private Vector2 letterGapPadding = new Vector2(0.06f, 0.02f);
        [Tooltip("글자판 위아래 여백 = 화면 높이 × 이 값 (9vh).")]
        [SerializeField] private float blockPadding = 0.09f;
        [Tooltip("밑줄 위 간격 · 굵기 = 화면 높이 × 이 값 (1.4vh · 0.5vh).")]
        [SerializeField] private Vector2 underlineGapHeight = new Vector2(0.014f, 0.005f);
        [Tooltip("밑줄 최소 굵기 (시안 px).")]
        [SerializeField] private float underlineMinHeight = 3f;
        [Tooltip("밑줄 폭 = min(화면 너비 × x, 글자 크기 × y) (74vw · 9.4em).")]
        [SerializeField] private Vector2 underlineWidth = new Vector2(0.74f, 9.4f);
        [Tooltip("밑줄 빛 번짐 (시안 px — box-shadow 14px).")]
        [SerializeField] private float underlineGlowBlur = 14f;
        [Tooltip("설명 줄 위 간격 = 화면 높이 × 이 값 (2.2vh).")]
        [SerializeField] private float subtitleGap = 0.022f;
        [Tooltip("설명 글자 크기 = 화면 짧은 변 × x, 하한 y · 상한 z (시안 px) (2.5vmin · 13px · 27px).")]
        [SerializeField] private Vector3 subtitleSize = new Vector3(0.025f, 13f, 27f);
        [Tooltip("설명 줄 높이 / 글자 크기.")]
        [SerializeField] private float subtitleLine = 1.3f;
        [Tooltip("설명 줄 세 칸 사이 (x, em) · 세로 선 높이 (y, em) (시안 gap 1.1em · 1.15em).")]
        [SerializeField] private Vector2 subtitleGapBar = new Vector2(1.1f, 1.15f);
        [Tooltip("어두운 타원 반지름 = (화면 너비, 글자판 높이) × 이 값 (시안 ellipse 62% 50%).")]
        [SerializeField] private Vector2 backdropRadius = new Vector2(0.62f, 0.5f);
        [Tooltip("고리 그림에서 고리 선이 있는 반지름 / 그림 반 변.")]
        [SerializeField] private float ringLineRatio = 0.78f;
        [Tooltip("번쩍이는 빛 크기 = 큰 글자 줄 × 이 값.")]
        [SerializeField] private Vector2 flashSize = new Vector2(1.3f, 2.2f);

        [Header("시간 (ms, 글자 시작 기준)")]
        [Tooltip("글자판이 나타나는 시간.")]
        [SerializeField] private float fadeMs = 320f;
        [Tooltip("대 · 소 · 동 이 내려오기 시작하는 때.")]
        [SerializeField] private Vector3 letterStartMs = new Vector3(40f, 350f, 700f);
        [Tooltip("글자가 내려와 자리 잡는 시간 — 대 · 소 (x) / 동 (y).")]
        [SerializeField] private Vector2 letterMs = new Vector2(440f, 520f);
        [Tooltip("내려오기 시작할 때 글자 크기 — 대 · 소 (x) / 동 (y).")]
        [SerializeField] private Vector2 letterFromScale = new Vector2(1.6f, 1.9f);
        [Tooltip("쿵 = 내려오는 시간의 이 비율.")]
        [SerializeField] private float landRatio = 0.34f;
        [Tooltip("고리가 퍼지는 시간 — 대 · 소 (x) / 동 (y), 동의 두 번째 고리가 늦는 시간 (z).")]
        [SerializeField] private Vector3 ringMs = new Vector3(650f, 900f, 140f);
        [Tooltip("고리가 다 퍼진 크기 (가로, 세로) — 대 · 소.")]
        [SerializeField] private Vector2 ringScale = new Vector2(2.4f, 0.5f);
        [Tooltip("고리가 다 퍼진 크기 (가로, 세로) — 동.")]
        [SerializeField] private Vector2 ringScaleLast = new Vector2(3.4f, 0.7f);
        [Tooltip("쿵 할 때 컷인 흔들림 (시안 px) — 대 · 소 (x) / 동 (y), 시간 (ms) — 대 · 소 (z) / 동 (w).")]
        [SerializeField] private Vector4 shake = new Vector4(5f, 9f, 240f, 320f);
        [Tooltip("밑줄이 1/3 씩 자라는 시간.")]
        [SerializeField] private float underlineMs = 260f;
        [Tooltip("큰 글자 줄이 커지기 시작하는 때 (x) · 걸리는 시간 (y) · 끝 크기 (z).")]
        [SerializeField] private Vector3 approach = new Vector3(900f, 2600f, 1.05f);
        [Tooltip("동 이 쿵 할 때 번쩍인 초록 빛이 가라앉는 시간.")]
        [SerializeField] private float flashMs = 900f;
        [Tooltip("설명 줄이 올라오기 시작하는 때 (x) · 걸리는 시간 (y) · 처음 아래로 내려가 있는 양 (z, em).")]
        [SerializeField] private Vector3 subtitleMs = new Vector3(1220f, 600f, 0.6f);

        [Header("빛")]
        [Tooltip("큰 글자의 초록 빛 (평소, 시안 drop-shadow .16em rgba(150, 255, 60, .7)) — TMP 빛은 알파를 두 배로 쓰고 그림자 위에 더해지므로 옅게 넣는다.")]
        [SerializeField] private Color glowColor = new Color(150f / 255f, 1f, 60f / 255f, 0.2f);
        [Tooltip("동 이 쿵 할 때의 초록 빛 (시안 drop-shadow .5em — 글자 둘레 번짐은 TMP 빛 폭까지라 넓은 쪽은 번쩍이는 빛 그림이 맡는다).")]
        [SerializeField] private Color flashColor = new Color(190f / 255f, 1f, 90f / 255f, 0.5f);
        [Tooltip("번쩍이는 빛의 가장 진한 투명도.")]
        [SerializeField] private float flashAlpha = 0.6f;

        private readonly float[] _letterWidths = new float[LetterCount];
        private readonly Vector2[] _letterCenters = new Vector2[LetterCount];
        private float _fontSize;
        private float _subtitleFontSize;
        private float _pixelScale = 1f;
        private Vector2 _subtitleBase;
        private Material _letterMaterial;
        private Texture2D _letterTexture;

        private void Awake()
        {
            if (group == null || block == null || backdrop == null || ringLayer == null || rings == null || rings.Length != LetterCount + 1 || flash == null
                || row == null || letters == null || letters.Length != LetterCount || underline == null || underlineGlow == null || subtitle == null
                || subtitleLeft == null || subtitleBar == null || subtitleRight == null)
                Debug.LogError($"[{nameof(NukeTitleFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnDestroy()
        {
            if (_letterMaterial != null)
                Destroy(_letterMaterial);
            if (_letterTexture != null)
                Destroy(_letterTexture);
        }

        /// <summary>화면 크기(화면 단위)에 맞춰 글자판을 놓는다. pixelScale = 시안 1px 의 화면 단위.</summary>
        public void Begin(Vector2 screen, float pixelScale)
        {
            _pixelScale = pixelScale;
            float vmin = Mathf.Min(screen.x, screen.y);
            float vh = screen.y;
            _fontSize = Mathf.Clamp(letterVmin * vmin, letterClamp.x * pixelScale, letterClamp.y * pixelScale);
            _subtitleFontSize = Mathf.Clamp(subtitleSize.x * vmin, subtitleSize.y * pixelScale, subtitleSize.z * pixelScale);
            float lineHeight = Mathf.Max(underlineMinHeight * pixelScale, underlineGapHeight.y * vh);
            float subtitleHeight = _subtitleFontSize * subtitleLine;
            float pad = blockPadding * vh;
            float blockHeight = pad + _fontSize + underlineGapHeight.x * vh + lineHeight + subtitleGap * vh + subtitleHeight + pad;
            block.sizeDelta = new Vector2(0f, blockHeight);
            block.anchoredPosition = Vector2.zero;
            float top = blockHeight * 0.5f;

            backdrop.rectTransform.sizeDelta = new Vector2(2f * backdropRadius.x * screen.x, 2f * backdropRadius.y * blockHeight);
            backdrop.rectTransform.anchoredPosition = Vector2.zero;

            // 큰 글자 줄 — 세 글자가 같은 재질 하나를 쓴다 (빛을 한 번에 바꾼다)
            if (_letterMaterial == null)
            {
                _letterMaterial = new Material(letters[0].fontSharedMaterial);
                _letterTexture = BuildGradientTexture(letterGradient);
                _letterMaterial.SetTexture(FaceTexId, _letterTexture);
            }

            float total = 0f;
            for (int i = 0; i < LetterCount; i++)
            {
                TextMeshProUGUI letter = letters[i];
                letter.fontSharedMaterial = _letterMaterial;
                letter.fontSize = _fontSize;
                _letterWidths[i] = letter.GetPreferredValues(letter.text, float.PositiveInfinity, float.PositiveInfinity).x + 2f * letterGapPadding.y * _fontSize;
                total += _letterWidths[i];
            }

            total += (LetterCount - 1) * letterGapPadding.x * _fontSize;
            var rowSize = new Vector2(total, _fontSize);
            var rowAt = new Vector2(0f, top - pad - _fontSize * 0.5f);
            row.sizeDelta = rowSize;
            row.anchoredPosition = rowAt;
            ringLayer.sizeDelta = rowSize;
            ringLayer.anchoredPosition = rowAt;
            flash.rectTransform.sizeDelta = Vector2.Scale(rowSize, flashSize);
            flash.rectTransform.anchoredPosition = rowAt;
            float x = -total * 0.5f;
            for (int i = 0; i < LetterCount; i++)
            {
                RectTransform rect = letters[i].rectTransform;
                rect.sizeDelta = new Vector2(_letterWidths[i], _fontSize);
                // 시안 transform-origin 50% 86% — pivot 은 프리팹이 (0.5, 0.14) 로 갖고 있다
                _letterCenters[i] = new Vector2(x + _letterWidths[i] * 0.5f, -_fontSize * 0.5f + _fontSize * rect.pivot.y);
                rect.anchoredPosition = _letterCenters[i];
                x += _letterWidths[i] + letterGapPadding.x * _fontSize;
            }

            // 밑줄
            float underlineY = top - pad - _fontSize - underlineGapHeight.x * vh - lineHeight * 0.5f;
            underline.sizeDelta = new Vector2(Mathf.Min(underlineWidth.x * screen.x, underlineWidth.y * _fontSize), lineHeight);
            underline.anchoredPosition = new Vector2(0f, underlineY);
            float blur = underlineGlowBlur * pixelScale;
            underlineGlow.rectTransform.sizeDelta = underline.sizeDelta;
            underlineGlow.rectTransform.anchoredPosition = underline.anchoredPosition;
            underlineGlow.Set(UIShape.ShapeKind.RoundRect, lineHeight * 0.5f, 0f, Color.clear, blur * 2f);

            // 설명 줄 — 왼쪽 글 · 세로 선 · 오른쪽 글을 가운데 정렬
            subtitleLeft.fontSize = _subtitleFontSize;
            subtitleRight.fontSize = _subtitleFontSize;
            float left = subtitleLeft.GetPreferredValues(subtitleLeft.text, float.PositiveInfinity, float.PositiveInfinity).x;
            float right = subtitleRight.GetPreferredValues(subtitleRight.text, float.PositiveInfinity, float.PositiveInfinity).x;
            float gap = subtitleGapBar.x * _subtitleFontSize;
            float barWidth = Mathf.Max(1f, pixelScale);
            float width = left + gap + barWidth + gap + right;
            var sub = (RectTransform)subtitle.transform;
            sub.sizeDelta = new Vector2(width, subtitleHeight);
            _subtitleBase = new Vector2(0f, underlineY - lineHeight * 0.5f - subtitleGap * vh - subtitleHeight * 0.5f);
            sub.anchoredPosition = _subtitleBase;
            subtitleLeft.rectTransform.sizeDelta = new Vector2(left, subtitleHeight);
            subtitleLeft.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + left * 0.5f, 0f);
            subtitleBar.rectTransform.sizeDelta = new Vector2(barWidth, subtitleGapBar.y * _subtitleFontSize);
            subtitleBar.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + left + gap + barWidth * 0.5f, 0f);
            subtitleRight.rectTransform.sizeDelta = new Vector2(right, subtitleHeight);
            subtitleRight.rectTransform.anchoredPosition = new Vector2(width * 0.5f - right * 0.5f, 0f);

            Tick(-1f);
        }

        /// <summary>글자 시작에서 ms 뒤의 모습으로 놓는다 (음수 = 아직 안 보임). 돌려주는 값 = 컷인 흔들림 (화면 단위, 위로 +).</summary>
        public Vector2 Tick(float ms)
        {
            group.alpha = ms < 0f ? 0f : NukeMath.CssEase(0f, 0f, 0.58f, 1f, ms / fadeMs);
            Vector2 offset = Vector2.zero;
            float grow = 0f;
            for (int i = 0; i < LetterCount; i++)
            {
                bool last = i == LetterCount - 1;
                float start = letterStartMs[i];
                float duration = last ? letterMs.y : letterMs.x;
                PlaceLetter(i, ms - start, duration, last ? letterFromScale.y : letterFromScale.x);

                float land = start + duration * landRatio;
                if (ms >= land)
                    grow = (i + NukeMath.CssEase(0.16f, 0.8f, 0.24f, 1f, (ms - land) / underlineMs)) / LetterCount;

                offset += ShakeAt(ms - land, last);
                PlaceRing(i, ms - land, i, last);
                if (last)
                    PlaceRing(LetterCount, ms - land - ringMs.z, i, true);
            }

            underline.localScale = new Vector3(grow, 1f, 1f);
            underlineGlow.rectTransform.localScale = underline.localScale;
            float rowScale = Mathf.Lerp(1f, approach.z, NukeMath.Clamp01((ms - approach.x) / approach.y));
            row.localScale = new Vector3(rowScale, rowScale, 1f);

            // 동 이 쿵 하면 초록 빛이 번쩍였다가 가라앉는다
            float lastLand = letterStartMs.z + letterMs.y * landRatio;
            float flashK = ms >= lastLand && ms < lastLand + flashMs ? 1f - NukeMath.CssEase(0f, 0f, 0.58f, 1f, (ms - lastLand) / flashMs) : 0f;
            // 빛 폭(Glow Outer)은 그대로 둔다 — 바꾸면 글자 칸 여백이 다시 계산되지 않아 빛이 잘린다
            if (_letterMaterial != null)
                _letterMaterial.SetColor(GlowColorId, Color.Lerp(glowColor, flashColor, flashK));

            Color flashTint = flash.color;
            flashTint.a = flashK * flashAlpha;
            flash.color = flashTint;
            flash.enabled = flashK > 0f;

            // 설명 줄 — 조금 아래에서 올라오며 나타난다
            float up = ms < subtitleMs.x ? 0f : NukeMath.CssEase(0.16f, 0.8f, 0.24f, 1f, (ms - subtitleMs.x) / subtitleMs.y);
            subtitle.alpha = up;
            ((RectTransform)subtitle.transform).anchoredPosition = _subtitleBase - new Vector2(0f, (1f - up) * subtitleMs.z * _subtitleFontSize);
            return offset;
        }

        // 1 × 64 세로 그라데이션 — TMP 글자 칸 아래(0) → 위(1) 에 깔린다 (Face Texture)
        private static Texture2D BuildGradientTexture(Gradient gradient)
        {
            var texture = new Texture2D(1, GradientTextureHeight, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = nameof(NukeTitleFx),
            };
            for (int y = 0; y < GradientTextureHeight; y++)
                texture.SetPixel(0, y, gradient.Evaluate(y / (GradientTextureHeight - 1f)));
            texture.Apply(false, true);
            return texture;
        }

        // 시안: 위에서 묵직하게 내려와 '쿵' — 살짝 눌렸다 펴진다 (키프레임 0 · .34 · .6 · 1, 전체에 cubic-bezier(.3, 0, .5, 1))
        private void PlaceLetter(int i, float ms, float duration, float fromScale)
        {
            TextMeshProUGUI letter = letters[i];
            if (ms < 0f)
            {
                letter.alpha = 0f;
                return;
            }

            float e = NukeMath.CssEase(0.3f, 0f, 0.5f, 1f, ms / duration);
            float alpha = 1f;
            float y;
            float sx;
            float sy;
            if (e < 0.34f)
            {
                float u = e / 0.34f;
                alpha = u;
                y = Mathf.Lerp(0.5f, -LandDrop, u);
                sx = Mathf.Lerp(fromScale, LandStretch, u);
                sy = Mathf.Lerp(fromScale, 0.9f, u);
            }
            else if (e < 0.6f)
            {
                float u = (e - 0.34f) / 0.26f;
                y = Mathf.Lerp(-LandDrop, 0.012f, u);
                sx = Mathf.Lerp(LandStretch, 0.985f, u);
                sy = Mathf.Lerp(0.9f, 1.03f, u);
            }
            else
            {
                float u = (e - 0.6f) / 0.4f;
                y = Mathf.Lerp(0.012f, 0f, u);
                sx = Mathf.Lerp(0.985f, 1f, u);
                sy = Mathf.Lerp(1.03f, 1f, u);
            }

            letter.alpha = alpha;
            RectTransform rect = letter.rectTransform;
            rect.anchoredPosition = _letterCenters[i] + new Vector2(0f, y * _fontSize);
            rect.localScale = new Vector3(sx, sy, 1f);
        }

        // 쿵 한 글자 밑(글자 칸 86% 높이)에서 바닥을 따라 퍼지는 고리
        private void PlaceRing(int index, float ms, int letter, bool last)
        {
            Image ring = rings[index];
            float duration = last ? ringMs.y : ringMs.x;
            // 두 번째 고리는 늦게 시작하지만 그동안 처음 모습으로 기다린다 (시안 fill both)
            float wait = index == LetterCount ? ringMs.z : 0f;
            if (ms < -wait || ms >= duration)
            {
                ring.enabled = false;
                return;
            }

            float e = NukeMath.CssEase(0.1f, 0.7f, 0.3f, 1f, ms / duration);
            Vector2 to = last ? ringScaleLast : ringScale;
            // 시안은 쿵 하는 순간(가로 1.04 · 아래로 .035em)의 글자 칸으로 고리 크기 · 자리를 잰다
            float side = _letterWidths[letter] * LandStretch * 1.05f / ringLineRatio;
            RectTransform rect = ring.rectTransform;
            rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = _letterCenters[letter] + new Vector2(0f, -LandDrop * _fontSize);
            rect.localScale = new Vector3(Mathf.Lerp(0.35f, to.x, e), Mathf.Lerp(0.1f, to.y, e), 1f);
            Color color = ring.color;
            color.a = Mathf.Lerp(0.95f, 0f, e);
            ring.color = color;
            ring.enabled = true;
        }

        // 쿵 할 때 컷인 흔들림 — 시안 [0, -1, .8, -.55, .35, -.15, 0] (가로 반, 세로 번갈아)
        private Vector2 ShakeAt(float ms, bool last)
        {
            float duration = last ? shake.w : shake.z;
            if (ms < 0f || ms >= duration)
                return Vector2.zero;
            float amount = (last ? shake.y : shake.x) * _pixelScale;
            float f = ms / duration * (ShakeKeys.Length - 1);
            int i = Mathf.Min(ShakeKeys.Length - 2, Mathf.FloorToInt(f));
            float u = f - i;
            float x = Mathf.Lerp(ShakeKeys[i], ShakeKeys[i + 1], u) * amount * 0.5f;
            float y0 = (i % 2 == 1 ? 1f : -1f) * Mathf.Abs(ShakeKeys[i]) * amount;
            float y1 = ((i + 1) % 2 == 1 ? 1f : -1f) * Mathf.Abs(ShakeKeys[i + 1]) * amount;
            // 시안은 y 가 아래로 + — 유니티는 위로 +
            return new Vector2(x, -Mathf.Lerp(y0, y1, u));
        }
    }
}
