using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 띠 컷인의 띠 안을 흐르는 가로 줄 — 앞뒤가 뾰족한 줄들이 저마다 빠르기로 왼쪽으로 흐른다 (속도감).
    /// 띠 면(마스크) 아래, 띠와 같은 크기로 두고 <see cref="BandSkew"/> 를 같이 붙여 띠 기울기를 따르게 한다. 색 = Graphic.color (<see cref="CutInBand"/> 가 띠 색보다 밝게 넣는다).
    /// 줄 자리 · 길이 · 굵기는 씨앗으로 정해 매번 같다. 왼쪽 끝을 지난 줄은 오른쪽 끝으로 돌아온다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다
    public class BandStreaks : MaskableGraphic
    {
        // 줄 앞쪽 굵은 곳 — 길이 비율
        private const float BulgeAt = 0.3f;

        [Tooltip("줄 수.")]
        [SerializeField] private int count = 16;
        [Tooltip("줄 길이 — 가장 짧은 · 긴 (px).")]
        [SerializeField] private Vector2 length = new Vector2(260f, 760f);
        [Tooltip("줄 굵기 — 가장 가는 · 굵은 (px).")]
        [SerializeField] private Vector2 thickness = new Vector2(8f, 22f);
        [Tooltip("흐르는 빠르기 (px/초) — 줄마다 0.7 ~ 1.3 배.")]
        [SerializeField] private float speed = 3200f;
        [Tooltip("자리 · 길이를 정하는 씨앗.")]
        [SerializeField] private int seed = 7;

        private float _shift;

        /// <summary>흐른 거리를 처음으로 — 연출을 틀 때마다.</summary>
        public void Restart()
        {
            _shift = 0f;
            SetVerticesDirty();
        }

        private void Update()
        {
            _shift += speed * Time.deltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            float span = rect.width + length.y;
            float row = rect.height / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                float y = rect.yMin + (i + 0.5f) * row + (Next() - 0.5f) * row;
                float len = Mathf.Lerp(length.x, length.y, Next());
                float half = Mathf.Lerp(thickness.x, thickness.y, Next()) / 2f;
                float pace = 0.7f + 0.6f * Next();
                float x = rect.xMax - Mathf.Repeat(Next() * span + _shift * pace, span);
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(x, y), color, Vector4.zero);
                vh.AddVert(new Vector3(x + len * BulgeAt, y + half), color, Vector4.zero);
                vh.AddVert(new Vector3(x + len, y), color, Vector4.zero);
                vh.AddVert(new Vector3(x + len * BulgeAt, y - half), color, Vector4.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v, v + 2, v + 3);
            }
        }
    }
}
