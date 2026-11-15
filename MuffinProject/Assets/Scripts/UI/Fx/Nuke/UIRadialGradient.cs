using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 타원 그라데이션 — CSS radial-gradient(ellipse, …) 처럼 가운데(0)에서 가장자리(1)로 색 · 알파 키마다 고리를 놓고 그 사이를 잇는다 (시안 「대소동」 글자 뒤 어둠).
    /// 타원 = 이 칸에 꽉 차는 크기. 색은 <see cref="gradient"/> × Graphic.color.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIRadialGradient : MaskableGraphic
    {
        private const int Segments = 64;

        [Tooltip("가운데(0) → 가장자리(1) 색.")]
        [SerializeField] private Gradient gradient = new Gradient();

        private readonly List<float> _stops = new List<float>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f)
                return;

            // 고리 자리 = 색 · 알파 키 (가운데 · 가장자리 포함, 겹치는 자리는 하나로)
            _stops.Clear();
            _stops.Add(0f);
            _stops.Add(1f);
            foreach (GradientColorKey key in gradient.colorKeys)
                _stops.Add(key.time);
            foreach (GradientAlphaKey key in gradient.alphaKeys)
                _stops.Add(key.time);
            _stops.Sort();

            Vector2 center = r.center;
            Vector2 radius = r.size * 0.5f;
            vh.AddVert(center, gradient.Evaluate(0f) * color, Vector2.zero);
            int rings = 0;
            float last = 0f;
            foreach (float stop in _stops)
            {
                if (stop - last < 1e-4f)
                    continue;
                last = stop;
                Color c = gradient.Evaluate(stop) * color;
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    vh.AddVert(center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y) * stop, c, Vector2.zero);
                }

                int start = 1 + rings * Segments;
                for (int i = 0; i < Segments; i++)
                {
                    int next = (i + 1) % Segments;
                    if (rings == 0)
                    {
                        vh.AddTriangle(0, start + next, start + i);
                        continue;
                    }

                    int inner = start - Segments;
                    vh.AddTriangle(inner + i, start + next, start + i);
                    vh.AddTriangle(inner + i, inner + next, start + next);
                }

                rings++;
            }
        }
    }
}
