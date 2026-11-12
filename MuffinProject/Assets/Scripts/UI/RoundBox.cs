using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Chapchu.UI
{
    /// <summary>
    /// 둥근 상자 하나 — 테두리와 안쪽 채움을 한 오브젝트가 그린다 (CODE_CONVENTION 10절의 9-slice 스프라이트를 그대로 쓴다).
    /// 채움 색은 Graphic 의 Color, 테두리는 따로. 테두리 두께가 0 이면 채움만 그린다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Chapchu/UI/Round Box")]
    public class RoundBox : MaskableGraphic
    {
        [Tooltip("모서리가 둥근 9-slice 스프라이트 (Common/ui_round64)")]
        [SerializeField] private Sprite sprite;

        [Tooltip("바깥 모서리 반지름 (캔버스 px)")]
        [SerializeField] private float radius = 24f;

        [Tooltip("테두리 두께 (캔버스 px). 0 이면 테두리 없음")]
        [SerializeField] private float borderWidth = 6f;

        [Tooltip("테두리 색. 안쪽 채움 색은 위의 Color")]
        [SerializeField] private Color borderColor = new Color32(0x2A, 0x24, 0x20, 0xFF);

        public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

        /// <summary>안쪽 채움 색 (Graphic.color 와 같다).</summary>
        public Color FillColor
        {
            get => color;
            set => color = value;
        }

        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor == value)
                    return;

                borderColor = value;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (sprite == null)
                Debug.LogError($"[{nameof(RoundBox)}] {nameof(sprite)} 참조가 비어 있습니다.", this);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sprite == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            Vector4 outer = DataUtility.GetOuterUV(sprite);
            Vector4 inner = DataUtility.GetInnerUV(sprite);

            if (borderWidth <= 0f)
            {
                AddSliced(vh, rect, radius, color, outer, inner);
                return;
            }

            AddSliced(vh, rect, radius, borderColor, outer, inner);
            var fillRect = new Rect(rect.x + borderWidth, rect.y + borderWidth, rect.width - borderWidth * 2f, rect.height - borderWidth * 2f);
            if (fillRect.width > 0f && fillRect.height > 0f)
                AddSliced(vh, fillRect, Mathf.Max(radius - borderWidth, 1f), color, outer, inner);
        }

        // 9-slice 한 장 — 모서리 크기는 corner, 자리가 모자라면 반씩 나눠 줄인다
        private static void AddSliced(VertexHelper vh, Rect rect, float corner, Color32 tint, Vector4 outer, Vector4 inner)
        {
            float cornerX = Mathf.Min(corner, rect.width * 0.5f);
            float cornerY = Mathf.Min(corner, rect.height * 0.5f);
            float[] xs = { rect.xMin, rect.xMin + cornerX, rect.xMax - cornerX, rect.xMax };
            float[] ys = { rect.yMin, rect.yMin + cornerY, rect.yMax - cornerY, rect.yMax };
            float[] us = { outer.x, inner.x, inner.z, outer.z };
            float[] vs = { outer.y, inner.y, inner.w, outer.w };

            for (int x = 0; x < 3; x++)
            {
                if (xs[x + 1] <= xs[x])
                    continue;

                for (int y = 0; y < 3; y++)
                {
                    if (ys[y + 1] <= ys[y])
                        continue;

                    int start = vh.currentVertCount;
                    vh.AddVert(new Vector3(xs[x], ys[y]), tint, new Vector2(us[x], vs[y]));
                    vh.AddVert(new Vector3(xs[x], ys[y + 1]), tint, new Vector2(us[x], vs[y + 1]));
                    vh.AddVert(new Vector3(xs[x + 1], ys[y + 1]), tint, new Vector2(us[x + 1], vs[y + 1]));
                    vh.AddVert(new Vector3(xs[x + 1], ys[y]), tint, new Vector2(us[x + 1], vs[y]));
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start + 2, start + 3, start);
                }
            }
        }
    }
}
