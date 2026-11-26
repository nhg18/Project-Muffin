using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 터지는 글자 뒤의 별 — 색 면 + 잉크 테 (시안 「4단계 공통 연출」 .boom polygon, BOOM_STAR).
    /// 16 꼭짓점을 칸에 맞춰 늘이고(preserveAspectRatio none), 가운데 줄에 있지 않은 꼭짓점은 ±jitter 만큼 흔들어 터질 때마다 손으로 그린 듯 다르게 한다.
    /// 면 색 = Graphic.color.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class InkStar : MaskableGraphic
    {
        // 시안 BOOM_STAR — 100 × 100 칸, y 는 아래로 +
        private static readonly Vector2[] Shape =
        {
            new Vector2(50, 0), new Vector2(61, 22), new Vector2(86, 8), new Vector2(78, 34), new Vector2(100, 42), new Vector2(80, 58),
            new Vector2(94, 84), new Vector2(66, 74), new Vector2(52, 100), new Vector2(40, 76), new Vector2(12, 92), new Vector2(22, 62),
            new Vector2(0, 50), new Vector2(20, 36), new Vector2(8, 12), new Vector2(36, 22),
        };

        [Tooltip("테 두께 (이 화면 단위).")]
        [SerializeField] private float borderWidth = 6f;
        [SerializeField] private Color borderColor = new Color32(0x2a, 0x24, 0x20, 0xff);
        [Tooltip("꼭짓점을 흔드는 범위 (100 칸 기준, 시안 ±4).")]
        [SerializeField] private float jitter = 4f;

        private readonly List<Vector2> _points = new List<Vector2>();
        private Vector2[] _shaken;

        /// <summary>코드에서 만들 때 — 테 두께 · 색을 넣고 꼭짓점을 새로 흔든다.</summary>
        public void Set(float newBorderWidth, Color newBorderColor)
        {
            borderWidth = newBorderWidth;
            borderColor = newBorderColor;
            Shake();
        }

        /// <summary>꼭짓점을 다시 흔든다 — 같은 별을 다시 쓸 때.</summary>
        public void Shake()
        {
            _shaken = new Vector2[Shape.Length];
            for (int i = 0; i < Shape.Length; i++)
            {
                Vector2 p = Shape[i];
                // 시안: x % 50 이 0 이 아니면 x 를, y % 50 이 0 이 아니면 y 를 흔든다 (가운데 줄 · 가장자리 꼭짓점은 그대로)
                _shaken[i] = new Vector2(p.x % 50f != 0f ? p.x + Random.Range(-jitter, jitter) : p.x,
                    p.y % 50f != 0f ? p.y + Random.Range(-jitter, jitter) : p.y);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            if (_shaken == null)
                Shake();

            _points.Clear();
            foreach (Vector2 p in _shaken)
                _points.Add(new Vector2(rect.xMin + p.x / 100f * rect.width, rect.yMax - p.y / 100f * rect.height));

            InkMesh.Fill(vh, rect.center, _points, color);
            InkMesh.Stroke(vh, _points, borderWidth, borderColor);
        }
    }
}
