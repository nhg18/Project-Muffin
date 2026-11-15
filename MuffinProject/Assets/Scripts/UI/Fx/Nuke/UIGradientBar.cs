using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 가로 그라데이션 막대 — CSS linear-gradient(90deg, …) 처럼 색 · 알파 키마다 세로 줄을 놓고 그 사이를 잇는다 (시안 「대소동」 글자 밑줄).
    /// 색은 <see cref="gradient"/> × Graphic.color.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIGradientBar : MaskableGraphic
    {
        [Tooltip("왼쪽(0) → 오른쪽(1) 색.")]
        [SerializeField] private Gradient gradient = new Gradient();

        private readonly List<float> _stops = new List<float>();

        /// <summary>그라데이션을 나눌 자리 — 0 · 1 · 색 키 · 알파 키 자리를 작은 순으로, 겹치는 자리는 하나로. <see cref="UIRadialGradient"/> 도 같이 쓴다.</summary>
        public static void CollectStops(Gradient gradient, List<float> stops)
        {
            stops.Clear();
            stops.Add(0f);
            stops.Add(1f);
            foreach (GradientColorKey key in gradient.colorKeys)
                stops.Add(key.time);
            foreach (GradientAlphaKey key in gradient.alphaKeys)
                stops.Add(key.time);
            stops.Sort();

            int kept = 1;
            for (int i = 1; i < stops.Count; i++)
            {
                if (stops[i] - stops[kept - 1] < 1e-4f)
                    continue;
                stops[kept] = stops[i];
                kept++;
            }

            stops.RemoveRange(kept, stops.Count - kept);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            CollectStops(gradient, _stops);

            int count = 0;
            foreach (float stop in _stops)
            {
                Color c = gradient.Evaluate(stop) * color;
                float x = Mathf.Lerp(r.xMin, r.xMax, stop);
                vh.AddVert(new Vector3(x, r.yMin), c, Vector2.zero);
                vh.AddVert(new Vector3(x, r.yMax), c, Vector2.zero);
                if (count > 0)
                {
                    int a = (count - 1) * 2;
                    vh.AddTriangle(a, a + 1, a + 3);
                    vh.AddTriangle(a, a + 3, a + 2);
                }

                count++;
            }
        }
    }
}
