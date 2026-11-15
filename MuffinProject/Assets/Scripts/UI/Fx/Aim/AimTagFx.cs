using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 .aim-tag — 왼쪽 아래의 「목표 조준」 이름표: 돌아가는 조준경 표시 + 제목 + 「쓴 사람 ▸ 대상」.
    /// 왼쪽부터 드러나며 나타났다가(clip-path) 한동안 머문 뒤 사라진다. 시안의 흐림(blur)은 넣지 않았다.
    /// </summary>
    public class AimTagFx : MonoBehaviour
    {
        private const float ViewBox = 60f;
        private const float Tau = Mathf.PI * 2f;
        // 시안 svg: 고리 반지름 · 십자 눈금(안 끝 · 바깥 끝) · 가운데 점
        private const float RingRadius = 17f;
        private const float TickInner = 15f;
        private const float TickOuter = 27f;
        private const float DotRadius = 2.6f;
        private const float StrokeWidth = 2f;
        // 네 글자 위에 쓸 글꼴 (주 글꼴에 없는 글자)
        private const string ArrowFont = "NeoDunggeunmoPro-Regular SDF";
        private const string Arrow = "▸";

        [Header("부품")]
        [Tooltip("드러남(clip-path)을 맡는 잘라 내기.")]
        [SerializeField] private RectMask2D reveal;
        [SerializeField] private CanvasGroup group;
        [Tooltip("조준경 표시 (시안 svg viewBox 60).")]
        [SerializeField] private AimPainter crosshair;
        [Tooltip("글자 묶음 (제목 · 이름 · 줄).")]
        [SerializeField] private RectTransform textBlock;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text names;
        [Tooltip("이름 위 가는 줄 (시안 border-top).")]
        [SerializeField] private RectTransform rule;

        [Header("그림")]
        [SerializeField] private Color strokeColor = new Color32(0xee, 0xf3, 0xff, 0xff);
        [SerializeField] private Color dotColor = new Color32(0xff, 0x46, 0x54, 0xff);
        [Tooltip("조준경 둘레 빛 (시안 drop-shadow 6px).")]
        [SerializeField] private Color haloColor = new Color(140f / 255f, 180f / 255f, 1f, 0.8f);
        [Tooltip("둘레 빛 세기 — 가장 안쪽 겹 알파에 곱한다 (번진 빛이라 옅다).")]
        [SerializeField] private float haloStrength = 0.22f;
        [Tooltip("둘레 빛 번짐 (viewBox 단위) — 겹마다 이만큼 굵어진다.")]
        [SerializeField] private float haloSpread = 2.5f;
        [Tooltip("둘레 빛 겹 수.")]
        [SerializeField] private int haloLayers = 2;
        [Tooltip("조준경 십자가 한 바퀴 도는 데 걸리는 시간 (초).")]
        [SerializeField] private float spinSeconds = 6f;
        [Tooltip("조준경과 글자 사이 (화면 단위).")]
        [SerializeField] private float gap = 17.3f;

        [Header("시간 (시안 aim-tag 애니메이션)")]
        [SerializeField] private float duration = 2.3f;
        [Tooltip("전체 진행에 거는 이징 (cubic-bezier).")]
        [SerializeField] private Vector4 easing = new Vector4(0.2f, 0.7f, 0.2f, 1f);
        [Tooltip("다 드러나는 진행 지점.")]
        [SerializeField] private float revealEnd = 0.22f;
        [Tooltip("사라지기 시작하는 진행 지점.")]
        [SerializeField] private float fadeStart = 0.84f;

        private readonly List<Vector2> _points = new List<Vector2>();

        private void Awake()
        {
            if (reveal == null || group == null || crosshair == null || textBlock == null || title == null || names == null || rule == null)
                Debug.LogError($"[{nameof(AimTagFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>이름을 넣고 칸을 글자에 맞춘다 (시안 「쓴 사람 ▸ 대상」).</summary>
        public void Prepare(string caster, string target)
        {
            names.text = $"{caster} <font=\"{ArrowFont}\">{Arrow}</font> {target}";
            title.ForceMeshUpdate();
            names.ForceMeshUpdate();
            float width = Mathf.Max(title.preferredWidth, names.preferredWidth);
            textBlock.sizeDelta = new Vector2(width, textBlock.sizeDelta.y);
            rule.sizeDelta = new Vector2(width, rule.sizeDelta.y);
            var box = (RectTransform)transform;
            float side = crosshair.rectTransform.rect.width;
            textBlock.anchoredPosition = new Vector2(side + gap, textBlock.anchoredPosition.y);
            box.sizeDelta = new Vector2(side + gap + width, box.sizeDelta.y);
        }

        /// <summary>시작부터 time 초의 모습. spin 은 조준경 십자가가 돈 시간 (초).</summary>
        public void Draw(float time, float spin)
        {
            if (time < 0f || time > duration)
            {
                group.alpha = 0f;
                return;
            }
            float e = AimEase.Bezier(easing, Mathf.Clamp01(time / duration));
            float alpha = e < revealEnd ? e / revealEnd : e < fadeStart ? 1f : 1f - (e - fadeStart) / (1f - fadeStart);
            group.alpha = Mathf.Clamp01(alpha);
            float shown = Mathf.Clamp01(e / revealEnd);
            float width = ((RectTransform)transform).rect.width;
            reveal.padding = new Vector4(0f, 0f, width * (1f - shown), 0f);
            DrawCrosshair(spin / spinSeconds * Tau);
        }

        // 시안 svg — 고리 · 도는 십자 눈금 · 빨간 점, 둘레에 푸른 빛
        private void DrawCrosshair(float angle)
        {
            crosshair.Clear();
            float k = crosshair.rectTransform.rect.width / ViewBox;
            crosshair.Scale(k, k);
            var center = new Vector2(ViewBox / 2f, ViewBox / 2f);
            for (int layer = haloLayers; layer >= 1; layer--)
            {
                Color halo = haloColor;
                halo.a *= haloStrength / layer;
                DrawMarks(center, angle, StrokeWidth + haloSpread * 2f * layer, halo);
            }
            DrawMarks(center, angle, StrokeWidth, strokeColor);
            crosshair.Ellipse(center, DotRadius, DotRadius, 0f, dotColor);
        }

        private void DrawMarks(Vector2 center, float angle, float width, Color color)
        {
            _points.Clear();
            AimPainter.AddArc(_points, center, RingRadius, RingRadius, 0f, Tau, 48);
            crosshair.Stroke(_points, width, color, false, true);
            for (int i = 0; i < 4; i++)
            {
                float a = angle + i * Tau / 4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                crosshair.Line(center + dir * TickInner, center + dir * TickOuter, width, color, true);
            }
        }
    }
}
