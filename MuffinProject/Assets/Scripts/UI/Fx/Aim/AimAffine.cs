using UnityEngine;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 canvas 의 변환(translate · rotate · scale) — 좌표는 아래로 +, 회전은 시계 방향 +.
    /// x' = A·x + C·y + E, y' = B·x + D·y + F (canvas setTransform 의 a b c d e f).
    /// </summary>
    public struct AimAffine
    {
        public float A;
        public float B;
        public float C;
        public float D;
        public float E;
        public float F;

        /// <summary>변환 없음.</summary>
        public static AimAffine Identity => new AimAffine { A = 1f, D = 1f };

        /// <summary>선 굵기에 곱할 배율 (고르게 늘인 만큼).</summary>
        public float LineScale => Mathf.Sqrt(Mathf.Abs(A * D - B * C));

        /// <summary>점 하나를 바꾼다.</summary>
        public Vector2 Apply(float x, float y)
        {
            return new Vector2(A * x + C * y + E, B * x + D * y + F);
        }

        /// <summary>점 하나를 바꾼다.</summary>
        public Vector2 Apply(Vector2 p)
        {
            return Apply(p.x, p.y);
        }

        /// <summary>canvas translate.</summary>
        public void Translate(float x, float y)
        {
            E += A * x + C * y;
            F += B * x + D * y;
        }

        /// <summary>canvas scale.</summary>
        public void Scale(float x, float y)
        {
            A *= x;
            B *= x;
            C *= y;
            D *= y;
        }

        /// <summary>canvas rotate (라디안, 시계 방향 +).</summary>
        public void Rotate(float radians)
        {
            float cs = Mathf.Cos(radians);
            float sn = Mathf.Sin(radians);
            float a = A * cs + C * sn;
            float b = B * cs + D * sn;
            float c = -A * sn + C * cs;
            float d = -B * sn + D * cs;
            A = a;
            B = b;
            C = c;
            D = d;
        }
    }
}
