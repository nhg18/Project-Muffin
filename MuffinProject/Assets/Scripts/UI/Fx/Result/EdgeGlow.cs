using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 화면 가장자리에서 안쪽으로 옅어지는 띠 (시안 「4단계 공통 연출」 .vign — inset box-shadow). 가장자리 색 = Graphic.color, 안쪽 끝은 투명.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class EdgeGlow : MaskableGraphic
    {
        [Tooltip("가장자리에서 투명해질 때까지의 폭 (이 화면 단위).")]
        [SerializeField] private float width = 180f;

        public void SetWidth(float newWidth)
        {
            width = newWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect outer = rectTransform.rect;
            float w = Mathf.Min(width, outer.width / 2f, outer.height / 2f);
            if (w <= 0f)
                return;

            Rect inner = new Rect(outer.xMin + w, outer.yMin + w, outer.width - w * 2f, outer.height - w * 2f);
            Color32 edge = color;
            Color32 clear = edge;
            clear.a = 0;

            // 바깥 네 모서리 → 안쪽 네 모서리, 변마다 사다리꼴 하나
            Vector2[] o = { new Vector2(outer.xMin, outer.yMin), new Vector2(outer.xMin, outer.yMax), new Vector2(outer.xMax, outer.yMax), new Vector2(outer.xMax, outer.yMin) };
            Vector2[] i = { new Vector2(inner.xMin, inner.yMin), new Vector2(inner.xMin, inner.yMax), new Vector2(inner.xMax, inner.yMax), new Vector2(inner.xMax, inner.yMin) };
            for (int k = 0; k < 4; k++)
            {
                vh.AddVert(o[k], edge, Vector4.zero);
                vh.AddVert(i[k], clear, Vector4.zero);
            }
            for (int k = 0; k < 4; k++)
            {
                int a = k * 2, b = (k + 1) % 4 * 2;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
            }
        }
    }
}
