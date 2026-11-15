using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 .aim-hit — 명중 순간 화면 위쪽의 「명중」 제목: 조준경 표시가 돌며 꽂히고 → 빨간 띠가 왼쪽부터 뻗고 → 두 글자가 차례로 내려찍히고 →
    /// 아래 「TARGET · 대상」 이 자간을 좁히며 나타난다. 빠질 때는 띠가 오른쪽으로 줄어들고 글자는 그 뒤를 따라 왼쪽부터 지워진다.
    /// 크기는 글자 크기(em, 시안 13.5vmin) 기준. 시안의 흐림(blur)은 넣지 않았다.
    /// </summary>
    public class AimHitTitleFx : MonoBehaviour
    {
        private const float Tau = Mathf.PI * 2f;
        private const float ReticleViewBox = 100f;
        private const string DotFont = "LiberationSans SDF";
        private const string TargetColor = "#FF6B78";

        [Header("부품")]
        [Tooltip("빠질 때 왼쪽부터 지우는 잘라 내기 (화면 너비 전체).")]
        [SerializeField] private RectMask2D wipe;
        [SerializeField] private CanvasGroup group;
        [Tooltip("제목 한 줄 (조준경 표시 + 두 글자). 띠가 이 너비로 깔린다.")]
        [SerializeField] private RectTransform row;
        [Tooltip("빨간 띠 (줄 칸 전체에 그린다).")]
        [SerializeField] private AimPainter band;
        [SerializeField] private RectTransform reticle;
        [SerializeField] private CanvasGroup reticleGroup;
        [SerializeField] private AimPainter reticlePainter;
        [Tooltip("두 글자 — 칸 가운데가 기준점 (커지며 내려찍힌다).")]
        [SerializeField] private RectTransform[] glyphs;
        [SerializeField] private CanvasGroup[] glyphGroups;
        [SerializeField] private TMP_Text[] glyphTexts;
        [Tooltip("글자 둘레 어두운 번짐 (시안 text-shadow 0 0 .35em).")]
        [SerializeField] private AimPainter[] glyphHalos;
        [Tooltip("아래 「TARGET · 대상」 줄.")]
        [SerializeField] private RectTransform sub;
        [SerializeField] private CanvasGroup subGroup;
        [SerializeField] private TMP_Text subText;
        [Tooltip("글자 양옆 가는 줄 (시안 .ah-sub i).")]
        [SerializeField] private AimPainter subLines;

        [Header("크기 (em = 제목 글자 크기)")]
        [Tooltip("줄 왼쪽 · 오른쪽 여백 (em).")]
        [SerializeField] private Vector2 rowPadding = new Vector2(0.34f, 0.5f);
        [Tooltip("조준경 표시 한 변 (em).")]
        [SerializeField] private float reticleSize = 0.86f;
        [Tooltip("조준경 표시와 글자 사이 (em).")]
        [SerializeField] private float reticleGap = 0.14f;
        [Tooltip("두 글자 사이 (em).")]
        [SerializeField] private float glyphGap = 0.1f;
        [Tooltip("띠 높이 (em).")]
        [SerializeField] private float bandHeight = 0.62f;
        [Tooltip("띠 기울기 (도, 시안 skewX(-22deg)).")]
        [SerializeField] private float bandSkew = 22f;
        [Tooltip("띠 색 멈춤 — 위치 · 색 (시안 linear-gradient 90deg).")]
        [SerializeField] private float[] bandStops = { 0f, 0.09f, 0.62f, 1f };
        [Tooltip("띠 색 멈춤마다의 색 (bandStops 와 같은 수).")]
        [SerializeField] private Color[] bandColors =
        {
            new Color(214f / 255f, 30f / 255f, 52f / 255f, 0f), new Color32(0xd6, 0x1e, 0x34, 0xff), new Color32(0xff, 0x46, 0x54, 0xff), new Color(1f, 70f / 255f, 84f / 255f, 0f),
        };
        [Tooltip("띠 그림자 — 아래로 밀기 · 번짐 (em, 시안 box-shadow 0 .03em .2em).")]
        [SerializeField] private Vector2 bandShadow = new Vector2(0.03f, 0.2f);
        [Tooltip("띠 그림자 색.")]
        [SerializeField] private Color bandShadowColor = new Color(5f / 255f, 8f / 255f, 20f / 255f, 0.5f);
        [Tooltip("띠 위 흰 줄 — 왼쪽 · 오른쪽 비율, 띠 위로 떨어진 거리 · 두께 (em).")]
        [SerializeField] private Vector4 bandShine = new Vector4(0.06f, 0.9f, 0.09f, 0.022f);
        [Tooltip("띠 위 흰 줄 색.")]
        [SerializeField] private Color bandShineColor = new Color(1f, 1f, 1f, 0.75f);
        [Tooltip("글자 둘레 어두운 번짐 — 반지름(em) · 색.")]
        [SerializeField] private float haloRadius = 0.8f;
        [Tooltip("글자 둘레 어두운 번짐 색.")]
        [SerializeField] private Color haloColor = new Color(5f / 255f, 8f / 255f, 20f / 255f, 0.45f);
        [Tooltip("조준경 표시 둘레 어두운 빛 (시안 drop-shadow .05em).")]
        [SerializeField] private Color reticleShadow = new Color(5f / 255f, 8f / 255f, 20f / 255f, 0.35f);
        [Tooltip("아래 줄: 양옆 선 길이 · 글자와 사이 (화면 단위).")]
        [SerializeField] private Vector2 subLine = new Vector2(172.8f, 17f);
        [Tooltip("아래 줄 양옆 선 두께 (화면 단위).")]
        [SerializeField] private float subLineWidth = 1.5f;
        [Tooltip("아래 줄 양옆 선 색 (바깥으로 갈수록 투명).")]
        [SerializeField] private Color subLineColor = new Color(223f / 255f, 230f / 255f, 1f, 0.9f);

        [Header("시간 (초, 시작부터)")]
        [Tooltip("조준경 표시: 길이 · 가운데 키 지점, 시작 배율 · 회전(도) · 가운데 배율.")]
        [SerializeField] private Vector2 reticleTime = new Vector2(0.23f, 0.7f);
        [SerializeField] private Vector3 reticleFrom = new Vector3(2.4f, -120f, 0.92f);
        [Tooltip("조준경 표시 굽음 (cubic-bezier).")]
        [SerializeField] private Vector4 reticleEase = new Vector4(0.3f, 0f, 0.3f, 1f);
        [Tooltip("띠가 뻗기: 늦춤 · 길이.")]
        [SerializeField] private Vector2 bandIn = new Vector2(0.09f, 0.21f);
        [Tooltip("띠가 뻗기 · 아래 줄 나타남 굽음 (cubic-bezier).")]
        [SerializeField] private Vector4 inEase = new Vector4(0.2f, 0.8f, 0.2f, 1f);
        [Tooltip("글자: 첫 늦춤 · 글자마다 늦춤 · 길이 · 가운데 키 지점.")]
        [SerializeField] private Vector4 glyphTime = new Vector4(0.19f, 0.095f, 0.2f, 0.62f);
        [Tooltip("글자: 시작 배율 · 시작 높이(em, 위로 −) · 가운데 배율 · 가운데 높이(em).")]
        [SerializeField] private Vector4 glyphFrom = new Vector4(1.9f, -0.12f, 0.94f, 0.02f);
        [Tooltip("글자 내려찍기 굽음 (cubic-bezier).")]
        [SerializeField] private Vector4 glyphEase = new Vector4(0.3f, 0f, 0.6f, 1f);
        [Tooltip("아래 줄: 늦춤 · 길이, 자간 처음 · 끝 (em/100).")]
        [SerializeField] private Vector4 subTime = new Vector4(0.43f, 0.42f, 90f, 42f);
        [Tooltip("빠지기: 시작 · 띠 길이 · 지우기 길이 · 지우기가 끝나는 진행 지점.")]
        [SerializeField] private Vector4 exitTime = new Vector4(1.33f, 0.26f, 0.32f, 0.85f);
        [Tooltip("빠지기 굽음 (cubic-bezier).")]
        [SerializeField] private Vector4 exitEase = new Vector4(0.6f, 0f, 0.8f, 0.4f);

        private readonly List<Vector2> _points = new List<Vector2>();
        private float _em;
        private float _spacing = -1f;
        private Vector2[] _glyphHomes;

        private void Awake()
        {
            if (wipe == null || group == null || row == null || band == null || reticle == null || reticleGroup == null || reticlePainter == null
                || glyphs == null || glyphGroups == null || glyphTexts == null || glyphHalos == null || glyphs.Length != glyphGroups.Length || glyphs.Length != glyphTexts.Length
                || glyphs.Length != glyphHalos.Length || sub == null || subGroup == null || subText == null || subLines == null)
                Debug.LogError($"[{nameof(AimHitTitleFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>대상 이름을 넣고 줄을 글자에 맞춘다. 고정된 그림(조준경 · 번짐)도 여기서 그린다.</summary>
        public void Prepare(string target)
        {
            _em = glyphTexts[0].fontSize;
            subText.text = $"<color={TargetColor}>TARGET</color> <font=\"{DotFont}\">·</font> {target}";
            _spacing = -1f;
            LayoutRow();
            DrawReticle();
            foreach (AimPainter halo in glyphHalos)
            {
                halo.Clear();
                Rect rect = halo.rectTransform.rect;
                halo.Glow(new Vector2(rect.width / 2f, rect.height / 2f), haloRadius * _em, haloColor);
            }
            SetSpacing(subTime.w);
            Draw(-1f);
        }

        /// <summary>시작부터 time 초의 모습 (0 보다 앞이면 아무것도 안 보인다).</summary>
        public void Draw(float time)
        {
            if (time < 0f)
            {
                group.alpha = 0f;
                return;
            }
            group.alpha = 1f;

            // 조준경 표시: 크게 돌며 → 살짝 작게 → 제자리
            float r = AimEase.Bezier(reticleEase, Mathf.Clamp01(time / reticleTime.x));
            float rs = r < reticleTime.y ? Mathf.Lerp(reticleFrom.x, reticleFrom.z, r / reticleTime.y) : Mathf.Lerp(reticleFrom.z, 1f, (r - reticleTime.y) / (1f - reticleTime.y));
            float ra = r < reticleTime.y ? Mathf.Lerp(reticleFrom.y, 0f, r / reticleTime.y) : 0f;
            reticleGroup.alpha = r < reticleTime.y ? r / reticleTime.y : 1f;
            reticle.localScale = new Vector3(rs, rs, 1f);
            reticle.localRotation = Quaternion.Euler(0f, 0f, -ra);

            // 글자: 크게 위에서 → 살짝 작게 아래로 → 제자리
            for (int i = 0; i < glyphs.Length; i++)
            {
                float g = AimEase.Bezier(glyphEase, Mathf.Clamp01((time - glyphTime.x - i * glyphTime.y) / glyphTime.z));
                float mid = glyphTime.w;
                float scale = g < mid ? Mathf.Lerp(glyphFrom.x, glyphFrom.z, g / mid) : Mathf.Lerp(glyphFrom.z, 1f, (g - mid) / (1f - mid));
                float lift = g < mid ? Mathf.Lerp(glyphFrom.y, glyphFrom.w, g / mid) : Mathf.Lerp(glyphFrom.w, 0f, (g - mid) / (1f - mid));
                glyphGroups[i].alpha = g < mid ? g / mid : 1f;
                glyphs[i].localScale = new Vector3(scale, scale, 1f);
                Vector2 home = _glyphHomes[i];
                glyphs[i].anchoredPosition = new Vector2(home.x, home.y - lift * _em);
            }

            // 아래 줄: 넓은 자간에서 좁혀지며 나타남
            float s = AimEase.Bezier(inEase, Mathf.Clamp01((time - subTime.x) / subTime.y));
            subGroup.alpha = s;
            SetSpacing(Mathf.Lerp(subTime.z, subTime.w, s));

            // 띠: 왼쪽부터 뻗음 → 빠질 때 오른쪽으로 줄어듦
            float exit = time - exitTime.x;
            if (exit < 0f)
                DrawBand(AimEase.Bezier(inEase, Mathf.Clamp01((time - bandIn.x) / bandIn.y)), false);
            else
                DrawBand(1f - AimEase.Bezier(exitEase, Mathf.Clamp01(exit / exitTime.y)), true);

            // 빠지기: 화면 왼쪽부터 지우고 마지막에 투명하게
            wipe.enabled = exit >= 0f;
            if (exit < 0f)
                return;
            float w = AimEase.Bezier(exitEase, Mathf.Clamp01(exit / exitTime.z));
            float width = ((RectTransform)transform).rect.width;
            wipe.padding = new Vector4(width * Mathf.Clamp01(w / exitTime.w), 0f, 0f, 0f);
            group.alpha = w < exitTime.w ? 1f : 1f - (w - exitTime.w) / (1f - exitTime.w);
        }

        #region 배치

        // 시안 .ah-row — [여백][조준경 표시][사이][글자 사이 글자][여백], 띠는 줄 너비 전체
        private void LayoutRow()
        {
            float x = rowPadding.x * _em;
            float side = reticleSize * _em;
            reticle.sizeDelta = new Vector2(side, side);
            reticle.anchoredPosition = new Vector2(x + side / 2f, -_em / 2f);
            x += side + reticleGap * _em;
            if (_glyphHomes == null || _glyphHomes.Length != glyphs.Length)
                _glyphHomes = new Vector2[glyphs.Length];
            for (int i = 0; i < glyphs.Length; i++)
            {
                glyphTexts[i].ForceMeshUpdate();
                float w = glyphTexts[i].preferredWidth;
                glyphs[i].sizeDelta = new Vector2(w, _em);
                _glyphHomes[i] = new Vector2(x + w / 2f, -_em / 2f);
                glyphs[i].anchoredPosition = _glyphHomes[i];
                x += w + (i < glyphs.Length - 1 ? glyphGap * _em : 0f);
            }
            x += rowPadding.y * _em;
            row.sizeDelta = new Vector2(x, _em);
            var column = (RectTransform)row.parent;
            column.sizeDelta = new Vector2(x, column.sizeDelta.y);
        }

        // 자간이 바뀌면 양옆 줄을 글자 끝에 다시 붙인다
        private void SetSpacing(float spacing)
        {
            if (Mathf.Approximately(spacing, _spacing))
                return;
            _spacing = spacing;
            subText.characterSpacing = spacing;
            subText.ForceMeshUpdate();
            Bounds bounds = subText.textBounds;
            Rect rect = sub.rect;
            Vector2 offset = subText.rectTransform.anchoredPosition;
            float left = rect.width / 2f + offset.x + bounds.min.x - subLine.y;
            float right = rect.width / 2f + offset.x + bounds.max.x + subLine.y;
            float y = rect.height / 2f;
            Color clear = subLineColor;
            clear.a = 0f;
            subLines.Clear();
            float half = subLineWidth / 2f;
            subLines.Quad(new Vector2(left - subLine.x, y - half), new Vector2(left, y - half), new Vector2(left, y + half), new Vector2(left - subLine.x, y + half),
                clear, subLineColor, subLineColor, clear);
            subLines.Quad(new Vector2(right, y - half), new Vector2(right + subLine.x, y - half), new Vector2(right + subLine.x, y + half), new Vector2(right, y + half),
                subLineColor, clear, clear, subLineColor);
        }

        #endregion

        #region 그리기

        // 시안 .ah-band — 줄 너비의 기운 띠: 가로 그라데이션 + 아래 그림자 + 위 흰 줄. scale 은 scaleX, fromRight 면 오른쪽이 기준
        private void DrawBand(float scale, bool fromRight)
        {
            band.Clear();
            if (scale <= 0f)
                return;
            Rect rect = band.rectTransform.rect;
            float w = rect.width;
            float h = bandHeight * _em;
            float cy = rect.height / 2f;
            float top = cy - h / 2f;
            float ox = fromRight ? w : 0f;
            float k = Mathf.Tan(bandSkew * Mathf.Deg2Rad);

            // 그림자: 띠 밖으로만 번진다 (시안 box-shadow)
            float drop = bandShadow.x * _em;
            float blur = bandShadow.y * _em;
            Color edge = bandShadowColor;
            edge.a *= 0.5f;
            Color clear = edge;
            clear.a = 0f;
            Vector2 a0 = Map(0f, top + drop, ox, scale, cy, k);
            Vector2 a1 = Map(w, top + drop, ox, scale, cy, k);
            Vector2 a2 = Map(w, top + h + drop, ox, scale, cy, k);
            Vector2 a3 = Map(0f, top + h + drop, ox, scale, cy, k);
            Vector2 b0 = Map(-blur, top + drop - blur, ox, scale, cy, k);
            Vector2 b1 = Map(w + blur, top + drop - blur, ox, scale, cy, k);
            Vector2 b2 = Map(w + blur, top + h + drop + blur, ox, scale, cy, k);
            Vector2 b3 = Map(-blur, top + h + drop + blur, ox, scale, cy, k);
            band.Quad(b0, b1, a1, a0, clear, clear, edge, edge);
            band.Quad(a1, b1, b2, a2, edge, clear, clear, edge);
            band.Quad(a3, a2, b2, b3, edge, edge, clear, clear);
            band.Quad(b0, a0, a3, b3, clear, edge, edge, clear);

            // 띠 몸: 색 멈춤마다 한 칸
            for (int i = 1; i < bandStops.Length; i++)
            {
                float x0 = bandStops[i - 1] * w;
                float x1 = bandStops[i] * w;
                band.Quad(Map(x0, top, ox, scale, cy, k), Map(x1, top, ox, scale, cy, k), Map(x1, top + h, ox, scale, cy, k), Map(x0, top + h, ox, scale, cy, k),
                    bandColors[i - 1], bandColors[i], bandColors[i], bandColors[i - 1]);
            }

            // 위 흰 줄
            float sy0 = top - bandShine.z * _em;
            float sy1 = sy0 + bandShine.w * _em;
            band.Quad(Map(bandShine.x * w, sy0, ox, scale, cy, k), Map(bandShine.y * w, sy0, ox, scale, cy, k), Map(bandShine.y * w, sy1, ox, scale, cy, k), Map(bandShine.x * w, sy1, ox, scale, cy, k),
                bandShineColor, bandShineColor, bandShineColor, bandShineColor);
        }

        // 시안 transform: skewX(−기울기) scaleX(scale), 기준점 (ox, 가운데 높이)
        private static Vector2 Map(float x, float y, float ox, float scale, float cy, float k)
        {
            return new Vector2(ox + (x - ox) * scale - k * (y - cy), y);
        }

        // 시안 .ah-ret svg (viewBox 100) — 고리 · 네 눈금 · 가운데 점, 둘레 어두운 빛
        private void DrawReticle()
        {
            reticlePainter.Clear();
            float k = reticlePainter.rectTransform.rect.width / ReticleViewBox;
            reticlePainter.Scale(k, k);
            var center = new Vector2(50f, 50f);
            DrawReticleMarks(center, 5f + 12f, ScaleAlpha(reticleShadow, 0.5f));
            DrawReticleMarks(center, 5f + 6f, reticleShadow);
            reticlePainter.Glow(center, 7f + 8f, reticleShadow);
            DrawReticleMarks(center, 5f, Color.white);
            reticlePainter.Ellipse(center, 7f, 7f, 0f, Color.white);
        }

        private void DrawReticleMarks(Vector2 center, float width, Color color)
        {
            _points.Clear();
            AimPainter.AddArc(_points, center, 33f, 33f, 0f, Tau, 64);
            reticlePainter.Stroke(_points, width, color, false, true);
            reticlePainter.Line(new Vector2(50f, 2f), new Vector2(50f, 26f), width, color, true);
            reticlePainter.Line(new Vector2(50f, 74f), new Vector2(50f, 98f), width, color, true);
            reticlePainter.Line(new Vector2(2f, 50f), new Vector2(26f, 50f), width, color, true);
            reticlePainter.Line(new Vector2(74f, 50f), new Vector2(98f, 50f), width, color, true);
        }

        private static Color ScaleAlpha(Color color, float k)
        {
            color.a *= k;
            return color;
        }

        #endregion
    }
}
