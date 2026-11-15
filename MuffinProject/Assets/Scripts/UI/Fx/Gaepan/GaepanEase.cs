using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 개판 오분전 연출이 시안 CSS 애니메이션 · DOGPILE 을 옮길 때 쓰는 진행률 곡선 (제목 · 도장 · 장면이 같이 쓴다).
    /// </summary>
    public static class GaepanEase
    {
        private const int NewtonSteps = 8;

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) — 진행률 x(0 ~ 1) 에 대한 값. y 가 1 을 넘는 곡선은 튕긴다.</summary>
        public static float CubicBezier(Vector4 curve, float x)
        {
            x = Mathf.Clamp01(x);
            float t = x;
            for (int i = 0; i < NewtonSteps; i++)
            {
                float current = Bezier(curve.x, curve.z, t) - x;
                float slope = 3f * (1f - t) * (1f - t) * curve.x + 6f * (1f - t) * t * (curve.z - curve.x) + 3f * t * t * (1f - curve.z);
                if (Mathf.Abs(slope) < 1e-5f)
                    break;
                t = Mathf.Clamp01(t - current / slope);
            }
            return Bezier(curve.y, curve.w, t);
        }

        /// <summary>시안 eo — 빠르게 시작해 천천히 멈춘다 (1 − (1 − u)³).</summary>
        public static float OutCubic(float u)
        {
            u = Mathf.Clamp01(u);
            return 1f - (1f - u) * (1f - u) * (1f - u);
        }

        /// <summary>
        /// CSS 키프레임 두 칸(0% → split → 100%)의 진행 — 칸마다 곡선을 따로 건다 (CSS 는 키프레임 칸마다 timing function 을 다시 건다).
        /// 돌려주는 값: x = 몇째 칸(0 · 1), y = 그 칸 안의 곡선 값. progress 가 범위 밖이면 처음 · 끝 칸에 붙는다.
        /// </summary>
        public static Vector2 TwoStep(Vector4 curve, float progress, float split)
        {
            if (progress < split)
                return new Vector2(0f, CubicBezier(curve, progress / split));
            return new Vector2(1f, CubicBezier(curve, (progress - split) / (1f - split)));
        }

        private static float Bezier(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }
    }
}
