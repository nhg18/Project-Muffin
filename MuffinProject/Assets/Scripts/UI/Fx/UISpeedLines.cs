using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 만화 집중선 — 바깥에서 가운데를 향하는 뾰족한 쐐기들 (프로토타입 v2 fxSpeed). 가운데 = 이 RectTransform 의 피벗.
    /// 쐐기마다 각도 · 굵기 · 안쪽 끝이 조금씩 다르다. <see cref="Shuffle"/> 을 부를 때마다 다시 뽑는다. 색 = Graphic.color.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다 — 없으면 아무것도 그려지지 않는다
    public class UISpeedLines : MaskableGraphic
    {
        [Tooltip("쐐기 수 (시안 44).")]
        [SerializeField] private int count = 44;
        [Tooltip("쐐기 바깥 끝까지의 반지름 — 화면 모서리보다 멀어야 한다 (시안 hypot(960, 540) × 2).")]
        [SerializeField] private float outerRadius = 2203f;
        [Tooltip("쐐기 안쪽 끝의 반지름 — 바깥 반지름에 대한 비율의 가장 작은 값 (시안 0.3).")]
        [SerializeField] private float innerMin = 0.3f;
        [Tooltip("쐐기 안쪽 끝의 반지름 — 바깥 반지름에 대한 비율의 가장 큰 값 (시안 0.42).")]
        [SerializeField] private float innerMax = 0.42f;
        [Tooltip("쐐기 바깥쪽 벌어진 각 (라디안) 의 가장 작은 값 (시안 0.022).")]
        [SerializeField] private float widthMin = 0.022f;
        [Tooltip("쐐기 바깥쪽 벌어진 각 (라디안) 의 가장 큰 값 (시안 0.042).")]
        [SerializeField] private float widthMax = 0.042f;
        [Tooltip("쐐기 각도가 고른 간격에서 어긋나는 가장 큰 값 (라디안, 시안 0.06).")]
        [SerializeField] private float angleJitter = 0.06f;
        [Tooltip("켜면 안쪽 끝 비율을 바깥 반지름이 아니라 그 방향의 화면(이 RectTransform) 가장자리까지 거리에 곱한다 — 화면 전환 집중선 (15-screen.md 9-4).")]
        [SerializeField] private bool innerFromEdge;

        private int _seed = 1;

        /// <summary>쐐기를 다시 뽑는다 — 연출을 틀 때마다 모양이 달라진다.</summary>
        public void Shuffle()
        {
            _seed = Random.Range(1, int.MaxValue);
            SetVerticesDirty();
        }

        // 가운데(피벗)에서 그 방향으로 이 RectTransform 가장자리까지
        private float EdgeDistance(float angle)
        {
            Rect r = rectTransform.rect;
            float cos = Mathf.Abs(Mathf.Cos(angle)), sin = Mathf.Abs(Mathf.Sin(angle));
            float x = cos > 1e-4f ? r.width / 2f / cos : float.MaxValue;
            float y = sin > 1e-4f ? r.height / 2f / sin : float.MaxValue;
            return Mathf.Min(x, y);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var random = new System.Random(_seed);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f + Next(0f, angleJitter);
                float angleEnd = angle + Next(widthMin, widthMax);
                float inner = (innerFromEdge ? EdgeDistance(angle) : outerRadius) * Next(innerMin, innerMax);

                vertex.position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
                vh.AddVert(vertex);
                vertex.position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * inner;
                vh.AddVert(vertex);
                vertex.position = new Vector2(Mathf.Cos(angleEnd), Mathf.Sin(angleEnd)) * outerRadius;
                vh.AddVert(vertex);
                vh.AddTriangle(i * 3, i * 3 + 1, i * 3 + 2);
            }
        }
    }
}
