using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 부채꼴 (UI 도형) — 가운데에서 반지름만큼, 위(12시)에서 시계 방향으로 start 부터 sweep 도. 반지름은 RectTransform 의 짧은 변의 절반.
    /// 스프라이트로는 칸을 나눈 원(121 개판 오분전의 5칸 자명종)을 그릴 수 없어서 둔다.
    /// </summary>
    public class UIPie : MaskableGraphic
    {
        private const float DegreesPerStep = 5f;

        [Tooltip("시작 각도 — 위(12시)에서 시계 방향, 도.")]
        [SerializeField] private float start;
        [Tooltip("벌어진 각도, 도.")]
        [SerializeField] private float sweep = 72f;

        /// <summary>각도를 바꾸고 다시 그린다.</summary>
        public void Set(float newStart, float newSweep)
        {
            start = newStart;
            sweep = Mathf.Clamp(newSweep, 0f, 360f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) / 2f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(sweep / DegreesPerStep));
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = rect.center;
            vh.AddVert(vertex);
            for (int i = 0; i <= steps; i++)
            {
                float angle = (90f - start - sweep * i / steps) * Mathf.Deg2Rad; // 위에서 시계 방향 → 수학 각도
                vertex.position = rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(vertex);
            }

            for (int i = 1; i <= steps; i++)
                vh.AddTriangle(0, i, i + 1);
        }
    }
}
