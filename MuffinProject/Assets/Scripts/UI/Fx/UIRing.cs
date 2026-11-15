using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 반지름과 두께를 따로 바꿀 수 있는 고리 (UI 도형). 스프라이트 고리는 키우면 두께도 같이 커져서
    /// 시안의 "커지면서 가늘어지는 고리"(ring)를 그릴 수 없다.
    /// </summary>
    public class UIRing : MaskableGraphic
    {
        private const int Segments = 72;

        [Tooltip("바깥 반지름 (가운데 기준).")]
        [SerializeField] private float radius = 50f;
        [Tooltip("선 두께.")]
        [SerializeField] private float thickness = 8f;

        /// <summary>반지름과 두께를 바꾸고 다시 그린다.</summary>
        public void Set(float newRadius, float newThickness)
        {
            radius = Mathf.Max(0f, newRadius);
            thickness = Mathf.Max(0f, newThickness);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float outer = radius + thickness / 2f;
            float inner = Mathf.Max(0f, radius - thickness / 2f);
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertex.position = direction * outer;
                vh.AddVert(vertex);
                vertex.position = direction * inner;
                vh.AddVert(vertex);
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2;
                vh.AddTriangle(a, a + 1, a + 2);
                vh.AddTriangle(a + 1, a + 3, a + 2);
            }
        }
    }
}
