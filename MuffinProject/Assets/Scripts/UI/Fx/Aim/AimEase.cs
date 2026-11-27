using UnityEngine;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 목표 조준 연출의 시간 곡선 — 시안 AIMC 의 cl · sm · eo · ei 와 CSS cubic-bezier.
    /// </summary>
    public static class AimEase
    {
        private const int NewtonSteps = 8;
        private const int BisectSteps = 20;
        private const float Epsilon = 1e-5f;

        /// <summary>CSS 의 기본 「ease」.</summary>
        public static readonly Vector4 CssEase = new Vector4(0.25f, 0.1f, 0.25f, 1f);

        /// <summary>시안 sm — 0~1 로 자른 뒤 부드럽게 (smoothstep).</summary>
        public static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>시안 eo — 빠르게 시작해 천천히 멈춤 (1 − (1 − k)³).</summary>
        public static float Out(float k)
        {
            float r = 1f - Mathf.Clamp01(k);
            return 1f - r * r * r;
        }

        /// <summary>시안 ei — 천천히 시작해 빨라짐 (k³).</summary>
        public static float In(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * k;
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) — 진행 t(0~1)에서의 값.</summary>
        public static float Bezier(Vector4 curve, float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f || t >= 1f)
                return t;

            // x(s) = t 인 s 를 찾는다 — 뉴턴으로 빨리, 안 되면 반씩 나눠서
            float s = t;
            for (int i = 0; i < NewtonSteps; i++)
            {
                float x = Cubic(curve.x, curve.z, s) - t;
                if (Mathf.Abs(x) < Epsilon)
                    return Cubic(curve.y, curve.w, s);
                float dx = CubicSlope(curve.x, curve.z, s);
                if (Mathf.Abs(dx) < Epsilon)
                    break;
                s -= x / dx;
            }

            float lo = 0f;
            float hi = 1f;
            s = t;
            for (int i = 0; i < BisectSteps; i++)
            {
                float x = Cubic(curve.x, curve.z, s);
                if (Mathf.Abs(x - t) < Epsilon)
                    break;
                if (x < t)
                    lo = s;
                else
                    hi = s;
                s = (lo + hi) * 0.5f;
            }
            return Cubic(curve.y, curve.w, s);
        }

        // 끝점이 0 · 1 인 3차 베지어의 한 축
        private static float Cubic(float p1, float p2, float s)
        {
            float r = 1f - s;
            return 3f * r * r * s * p1 + 3f * r * s * s * p2 + s * s * s;
        }

        private static float CubicSlope(float p1, float p2, float s)
        {
            float r = 1f - s;
            return 3f * r * r * p1 + 6f * r * s * (p2 - p1) + 3f * s * s * (1f - p2);
        }
    }
}
