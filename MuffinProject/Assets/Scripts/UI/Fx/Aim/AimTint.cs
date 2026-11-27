using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 chuParts 의 달밤 물들이기 — 부품 그림마다 곱하기 색(#9ba7de) 위에 soft-light 그라데이션(왼쪽 위 밝게 → 아래 어둡게)을 덮는다.
    /// soft-light 는 곱하기로 근사한다: 그림 칸의 왼쪽 위(0, 0)에서 (가로 40%, 아래 끝)으로 가는 선을 따라 정점 색을 start → end 로 곱한다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class AimTint : BaseMeshEffect
    {
        [Tooltip("그라데이션 시작(왼쪽 위)에서 곱할 색 — 물들이기 색 × 밝힘.")]
        [SerializeField] private Color start = new Color(0.76f, 0.83f, 1f, 1f);
        [Tooltip("그라데이션 끝(가로 40%, 아래 끝)에서 곱할 색 — 물들이기 색 × 어둡힘.")]
        [SerializeField] private Color end = new Color(0.41f, 0.45f, 0.66f, 1f);
        [Tooltip("그라데이션 끝점 (그림 칸 비율, 왼쪽 위 기준).")]
        [SerializeField] private Vector2 direction = new Vector2(0.4f, 1f);

        private readonly List<UIVertex> _stream = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;
            Rect rect = graphic.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            // 시안 createLinearGradient(0, 0, w·0.4, h) — 픽셀 공간에서 그 선에 내린 위치
            Vector2 axis = new Vector2(rect.width * direction.x, rect.height * direction.y);
            float lengthSq = axis.sqrMagnitude;
            _stream.Clear();
            vh.GetUIVertexStream(_stream);
            for (int i = 0; i < _stream.Count; i++)
            {
                UIVertex v = _stream[i];
                var fromTopLeft = new Vector2(v.position.x - rect.xMin, rect.yMax - v.position.y);
                float t = Mathf.Clamp01(Vector2.Dot(fromTopLeft, axis) / lengthSq);
                Color tint = Color.Lerp(start, end, t);
                Color c = v.color;
                v.color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
                _stream[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_stream);
        }
    }
}
