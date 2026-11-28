using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 기울인 평행사변형 한 장 — 윗변이 오른쪽으로 밀린다 (화면 전환 덮개, 15-screen.md 9-3). 크기 = 이 RectTransform, 기울기 = skewDegrees.
    /// 무늬 텍스처를 주면 tileSize(캔버스 px)마다 되풀이한다 — 텍스처의 반복(Wrap Repeat)을 켜 둔다. 없으면 color 한 색.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다
    public class UISkewQuad : MaskableGraphic
    {
        [Tooltip("기울기 (도) — 윗변이 아랫변보다 오른쪽으로 높이 × tan 만큼.")]
        [SerializeField] private float skewDegrees = 9f;
        [Tooltip("되풀이할 무늬 — 없으면 한 색.")]
        [SerializeField] private Texture pattern;
        [Tooltip("무늬 한 장의 크기 (캔버스 px).")]
        [SerializeField] private Vector2 tileSize = new Vector2(576f, 376f);

        public override Texture mainTexture => pattern != null ? pattern : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float shift = r.height * Mathf.Tan(skewDegrees * Mathf.Deg2Rad) / 2f;
            var corners = new[]
            {
                new Vector2(r.xMin - shift, r.yMin),
                new Vector2(r.xMin + shift, r.yMax),
                new Vector2(r.xMax + shift, r.yMax),
                new Vector2(r.xMax - shift, r.yMin),
            };

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            foreach (Vector2 corner in corners)
            {
                vertex.position = corner;
                // 무늬는 화면에 붙은 것처럼 — 판의 로컬 좌표를 칸 크기로 나눈다 (판이 움직이면 무늬도 같이 간다)
                vertex.uv0 = pattern != null && tileSize.x > 0f && tileSize.y > 0f ? new Vector2(corner.x / tileSize.x, corner.y / tileSize.y) : Vector2.zero;
                vh.AddVert(vertex);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
