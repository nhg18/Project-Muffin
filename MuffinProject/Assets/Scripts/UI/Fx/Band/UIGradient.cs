using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 면에 곧은 그라데이션을 입힌다 (시안 linear-gradient(각도, 시작 색, 끝 색)). 같은 오브젝트의 도형(<see cref="UIShape"/> 등)의
    /// 면 색(Graphic.color) 꼭짓점만 바꾼다 — 테두리 · 번짐 꼭짓점은 그대로. 꼭짓점 사이는 곧게 섞이므로 삼각형 모양과 상관없이 정확하다.
    /// 색 = 그라데이션 색 × Graphic.color (면 색은 흰색으로 두면 그라데이션 색 그대로).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        [Tooltip("CSS 각도 (도) — 0 = 위로, 90 = 오른쪽으로, 135 = 오른쪽 아래로.")]
        [SerializeField] private float angle = 135f;
        [Tooltip("시작 색.")]
        [SerializeField] private Color from = Color.white;
        [Tooltip("끝 색.")]
        [SerializeField] private Color to = Color.white;

        private readonly List<UIVertex> _vertices = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;

            Rect rect = graphic.rectTransform.rect;
            Color32 fill = graphic.color;
            // CSS: 0deg 가 위, 시계 방향. 그라데이션 선 길이는 모서리가 끝 색이 되게 |w·sin| + |h·cos|
            float radians = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            float length = Mathf.Abs(rect.width * direction.x) + Mathf.Abs(rect.height * direction.y);
            if (length <= 0f)
                return;

            vh.GetUIVertexStream(_vertices);
            for (int i = 0; i < _vertices.Count; i++)
            {
                UIVertex vertex = _vertices[i];
                if (!SameColor(vertex.color, fill))
                    continue;
                Vector2 offset = (Vector2)vertex.position - rect.center;
                float t = Mathf.Clamp01(0.5f + Vector2.Dot(offset, direction) / length);
                vertex.color = Color.Lerp(from, to, t) * graphic.color;
                _vertices[i] = vertex;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_vertices);
        }

        private static bool SameColor(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
