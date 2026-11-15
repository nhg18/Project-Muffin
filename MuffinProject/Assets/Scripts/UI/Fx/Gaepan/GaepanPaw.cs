using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 「5 바퀴 동안」 도장의 분홍 발바닥 (시안 .gp-five svg, viewBox 100 × 100) — 발가락 콩 넷 · 발바닥 · 밝은 점 넷, 밤색 테두리.
    /// 판 모양 그림자(시안 filter drop-shadow, 번짐 없음)를 먼저 깐다.
    /// 좌표는 viewBox (왼쪽 위 0, 아래 +) 를 이 그림의 사각형에 맞춘다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GaepanPaw : MaskableGraphic
    {
        private const float ViewBox = 100f;
        private const int EllipseSegments = 28;
        private const int CurveSegments = 10;
        private const int JoinSegments = 8;

        [SerializeField] private Color fill = new Color32(0xFF, 0x8F, 0xB0, 0xFF);
        [SerializeField] private Color stroke = new Color32(0x2B, 0x1E, 0x1A, 0xFF);
        [Tooltip("테두리 두께 (viewBox).")]
        [SerializeField] private float strokeWidth = 4.5f;
        [Tooltip("발가락 콩 (가운데 x · y, 반지름 x · y — viewBox). 시안의 rotate 는 CSS 애니메이션이 덮어 써서 보이지 않는다.")]
        [SerializeField] private Vector4[] beans =
        {
            new Vector4(20f, 38f, 10.5f, 12.5f),
            new Vector4(38f, 20f, 11f, 13.5f),
            new Vector4(62f, 20f, 11f, 13.5f),
            new Vector4(80f, 38f, 10.5f, 12.5f),
        };
        [Tooltip("발바닥 테두리 — 시작점 다음 3 점씩 3차 베지어 (viewBox, 시안 path).")]
        [SerializeField] private Vector2[] pad =
        {
            new Vector2(50f, 42f),
            new Vector2(63f, 42f), new Vector2(70f, 51f), new Vector2(76f, 61f),
            new Vector2(82f, 71f), new Vector2(83f, 83f), new Vector2(71f, 86f),
            new Vector2(63f, 88f), new Vector2(58f, 83f), new Vector2(50f, 83f),
            new Vector2(42f, 83f), new Vector2(37f, 88f), new Vector2(29f, 86f),
            new Vector2(17f, 83f), new Vector2(18f, 71f), new Vector2(24f, 61f),
            new Vector2(30f, 51f), new Vector2(37f, 42f), new Vector2(50f, 42f),
        };
        [Tooltip("발바닥을 부채꼴로 채우는 가운데 점 (viewBox).")]
        [SerializeField] private Vector2 padCenter = new Vector2(50f, 66f);
        [Tooltip("밝은 점 (가운데 · 반지름, viewBox).")]
        [SerializeField] private Vector4[] highlights =
        {
            new Vector4(34f, 17f, 3.5f, 4.5f),
            new Vector4(58f, 17f, 3.5f, 4.5f),
            new Vector4(17f, 34f, 3f, 4f),
            new Vector4(76f, 34f, 3f, 4f),
        };
        [SerializeField] private Color highlight = new Color32(0xFF, 0xC3, 0xD5, 0xFF);
        [SerializeField] private Color shadowColor = new Color32(43, 30, 26, 64);

        private readonly List<Vector2> _outline = new List<Vector2>();
        private float _shadowDrop;

        /// <summary>판 그림자가 아래로 밀린 양 (viewBox).</summary>
        public void SetShadowDrop(float shadowDrop)
        {
            if (Mathf.Approximately(_shadowDrop, shadowDrop))
                return;
            _shadowDrop = shadowDrop;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            BuildOutline();
            float half = strokeWidth / 2f;
            // 그림자: 테두리까지 포함한 모양을 그대로 아래로
            if (_shadowDrop > 0f)
            {
                var drop = new Vector2(0f, _shadowDrop);
                foreach (Vector4 bean in beans)
                    AddEllipse(vh, (Vector2)bean + drop, new Vector2(bean.z, bean.w), half, shadowColor);
                AddPadFill(vh, drop, shadowColor, half);
            }
            foreach (Vector4 bean in beans)
            {
                var radius = new Vector2(bean.z, bean.w);
                AddEllipse(vh, bean, radius + Vector2.one * half, 0f, stroke);
                AddEllipse(vh, bean, radius - Vector2.one * half, 0f, fill);
            }
            AddPadFill(vh, Vector2.zero, fill, 0f);
            AddPadStroke(vh, half);
            foreach (Vector4 h in highlights)
                AddEllipse(vh, h, new Vector2(h.z, h.w), 0f, highlight);
        }

        // 베지어를 잘게 나눈 발바닥 둘레 (viewBox)
        private void BuildOutline()
        {
            _outline.Clear();
            _outline.Add(pad[0]);
            for (int c = 1; c + 2 < pad.Length; c += 3)
            {
                Vector2 p0 = pad[c - 1], p1 = pad[c], p2 = pad[c + 1], p3 = pad[c + 2];
                for (int s = 1; s <= CurveSegments; s++)
                {
                    float t = (float)s / CurveSegments, u = 1f - t;
                    _outline.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
                }
            }
            _outline.RemoveAt(_outline.Count - 1);
        }

        // 가운데 점에서 부채꼴 (발바닥은 가운데에서 보면 둘레가 한 번만 지나간다). grow = 바깥으로 키우는 양 (그림자에 테두리 두께)
        private void AddPadFill(VertexHelper vh, Vector2 shift, Color color, float grow)
        {
            int start = vh.currentVertCount;
            vh.AddVert(ToLocal(padCenter + shift), color, Vector4.zero);
            foreach (Vector2 p in _outline)
            {
                Vector2 away = (p - padCenter).normalized;
                vh.AddVert(ToLocal(p + away * grow + shift), color, Vector4.zero);
            }
            int n = _outline.Count;
            for (int i = 0; i < n; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % n);
        }

        // 둘레 따라 두께 띠 + 꺾이는 곳마다 둥근 이음 (시안 stroke-linejoin round)
        private void AddPadStroke(VertexHelper vh, float half)
        {
            int n = _outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = _outline[i], b = _outline[(i + 1) % n];
                Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x) * half;
                int start = vh.currentVertCount;
                vh.AddVert(ToLocal(a - side), stroke, Vector4.zero);
                vh.AddVert(ToLocal(a + side), stroke, Vector4.zero);
                vh.AddVert(ToLocal(b + side), stroke, Vector4.zero);
                vh.AddVert(ToLocal(b - side), stroke, Vector4.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
                AddEllipse(vh, a, Vector2.one * half, 0f, stroke, JoinSegments);
            }
        }

        private void AddEllipse(VertexHelper vh, Vector2 center, Vector2 radius, float grow, Color color, int segments = EllipseSegments)
        {
            int start = vh.currentVertCount;
            vh.AddVert(ToLocal(center), color, Vector4.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vh.AddVert(ToLocal(center + new Vector2(Mathf.Cos(angle) * (radius.x + grow), Mathf.Sin(angle) * (radius.y + grow))), color, Vector4.zero);
            }
            for (int i = 0; i < segments; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
        }

        // viewBox(왼쪽 위, 아래 +) → 이 사각형 로컬 좌표
        private Vector2 ToLocal(Vector2 p)
        {
            Rect rect = rectTransform.rect;
            return new Vector2(rect.xMin + p.x / ViewBox * rect.width, rect.yMax - p.y / ViewBox * rect.height);
        }
    }
}
