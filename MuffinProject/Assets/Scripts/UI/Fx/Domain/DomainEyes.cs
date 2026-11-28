using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Domain
{
    /// <summary>
    /// 「영역전개」 강아지의 눈 뜨기 — 눈 뜬 그림에서 눈구멍 두 개만 잘라 그린다 (시안 drawDog 의 ellipse clip). 눈 감은 그림 위에 겹쳐 둔다.
    /// 뜬 정도 0 = 안 보임, 1 = 다 뜸. 1 을 넘으면 눈구멍이 눈보다 커진다 (시안의 「팍」 뜨는 튕김 — 눈 밖은 두 그림이 같아 티가 나지 않는다).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class DomainEyes : MaskableGraphic
    {
        private const int Segments = 28;

        [Tooltip("눈 뜬 그림 (눈 감은 그림과 같은 크기).")]
        [SerializeField] private Sprite sprite;
        [Tooltip("두 눈의 가운데 (그림 왼쪽 위 기준 비율 — 시안 EYES / 1024).")]
        [SerializeField] private Vector2[] eyes = { new Vector2(0.3525f, 0.5146f), new Vector2(0.709f, 0.5117f) };
        [Tooltip("감은 눈꺼풀 높이 (위 기준 비율 — 시안 LID / 1024). 눈구멍이 여기서 눈 가운데로 내려오며 열린다.")]
        [SerializeField] private float lid = 0.5283f;
        [Tooltip("다 뜬 눈구멍의 반지름 (그림 한 변 대비 가로 · 세로 — 시안 112 · 82).")]
        [SerializeField] private Vector2 radius = new Vector2(0.1094f, 0.0801f);

        private float _open;

        public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

        /// <summary>눈을 뜬 정도를 바꾼다 (0 = 감음, 1 = 다 뜸).</summary>
        public void SetOpen(float open)
        {
            if (Mathf.Approximately(open, _open)) return;

            _open = open;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_open <= 0f || sprite == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            Vector4 uv = UIQuadBatch.SpriteUv(sprite);
            float height = Mathf.Max(0.001f, radius.y * _open);
            foreach (Vector2 eye in eyes)
            {
                var center = new Vector2(eye.x, lid + (eye.y - lid) * Mathf.Min(1f, _open));
                int start = vh.currentVertCount;
                AddPoint(vh, rect, uv, center);
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / Segments;
                    AddPoint(vh, rect, uv, center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * height));
                }

                for (int i = 0; i < Segments; i++)
                    vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
            }
        }

        // point = 그림 왼쪽 위 기준 비율 (y 아래 +)
        private void AddPoint(VertexHelper vh, Rect rect, Vector4 uv, Vector2 point)
        {
            var position = new Vector2(rect.xMin + point.x * rect.width, rect.yMax - point.y * rect.height);
            var texture = new Vector2(Mathf.Lerp(uv.x, uv.z, point.x), Mathf.Lerp(uv.w, uv.y, point.y));
            vh.AddVert(position, color, texture);
        }
    }
}
