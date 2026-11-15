using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 3D 장면이 같이 쓰는 시간표 · 착탄점 · 미사일 길 · 보간 · 고정 난수 — 시안 NUKE3D 의 T · IMP · MP0 · MP1 · SW · bez · mPos · RND 와 같다.
    /// 좌표는 시안 그대로 (x 오른쪽 · y 위 · z 앞, 단위 m). 유니티 월드에는 <see cref="NukeSceneFx"/> 의 원점만큼 옮겨 놓는다.
    /// </summary>
    public static class NukeMath
    {
        // 장면 시각 (초, 장면 시간축 — 착탄 직전 느린 화면 앞)
        public const float Siren = 1.6f;
        public const float Notice = 1.9f;
        public const float Sky = 2.6f;
        public const float Hero = 3.5f;
        public const float Run = 4.4f;
        public const float Side = 6.2f;
        public const float Up = 7.4f;
        public const float Dive = 8.2f;
        public const float Hit = 9.8f;
        public const float Street = 11.2f;
        public const float Fall = 12.4f;
        public const float End = 15.4f;

        /// <summary>착탄 직전 느린 화면의 실제 길이 (초) — 장면 시간은 이 동안 0.1초만 흐른다.</summary>
        public const float Hold = 0.9f;

        /// <summary>느린 화면이 시작하는 장면 시각.</summary>
        public const float SlowStart = Hit - 0.1f;

        /// <summary>미사일이 나타나는 장면 시각.</summary>
        public const float MissileStart = 1.2f;

        /// <summary>바닥을 훑는 먼지 벽의 속도 (m/s).</summary>
        public const float DustSpeed = 36f;

        /// <summary>장면 가까운 · 먼 면 (m).</summary>
        public const float Near = 0.3f;
        public const float Far = 1100f;

        /// <summary>착탄점.</summary>
        public static readonly Vector3 Impact = new Vector3(0f, 0f, 30f);

        /// <summary>미사일 출발점 · 휘는 점 (2차 베지어).</summary>
        public static readonly Vector3 MissileFrom = new Vector3(8f, 250f, 420f);
        public static readonly Vector3 MissileBend = new Vector3(2f, 170f, 90f);

        /// <summary>해가 있는 쪽 (빛이 나아가는 방향의 반대).</summary>
        public static readonly Vector3 SunDir = new Vector3(-0.09f, 0.4f, 0.9f).normalized;

        private static float[] _rnd;

        /// <summary>실제 흐른 시간 → 장면 시간 (착탄 직전 0.9초 동안 0.1초만 흐른다).</summary>
        public static float SceneTime(float real)
        {
            if (real < SlowStart)
                return real;
            if (real < SlowStart + Hold)
                return SlowStart + (real - SlowStart) * (0.1f / Hold);
            return real - (Hold - 0.1f);
        }

        /// <summary>번쩍하는 순간의 실제 시각 (초).</summary>
        public static float HitReal => Hit + Hold - 0.1f;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>시안 sm — smoothstep(0, 1, k).</summary>
        public static float Smooth(float k)
        {
            k = Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>시안 eo — 끝에서 느려지는 3차.</summary>
        public static float EaseOut(float k)
        {
            float u = 1f - Clamp01(k);
            return 1f - u * u * u;
        }

        /// <summary>시안 eio — 양끝에서 느린 3차.</summary>
        public static float EaseInOut(float k)
        {
            k = Clamp01(k);
            if (k < 0.5f)
                return 4f * k * k * k;
            float u = -2f * k + 2f;
            return 1f - u * u * u / 2f;
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) 이징 — 시간 비율 x(0~1) 의 진행 비율.</summary>
        public static float CssEase(float x1, float y1, float x2, float y2, float x)
        {
            x = Clamp01(x);
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float error = BezierAt(x1, x2, t) - x;
                if (Mathf.Abs(error) < 1e-5f)
                    break;
                float slope = BezierSlope(x1, x2, t);
                if (Mathf.Abs(slope) < 1e-6f)
                    break;
                t = Clamp01(t - error / slope);
            }

            return BezierAt(y1, y2, t);
        }

        public static float Lerp(float a, float b, float k) => a + (b - a) * k;

        public static Vector3 Mix(Vector3 a, Vector3 b, float k) => a + (b - a) * k;

        /// <summary>시안 rn(i, k) — 고정 난수 표 (0~1).</summary>
        public static float Rn(int i, int k)
        {
            if (_rnd == null)
                _rnd = BuildTable();
            return _rnd[(i * 37 + k * 1013) & 4095];
        }

        /// <summary>시안 R() — 씨앗에서 이어지는 0~1 난수 (mulberry32).</summary>
        public static float Next(ref int seed)
        {
            unchecked
            {
                seed += 0x6D2B79F5;
                int s = seed;
                int t = Imul(s ^ (int)((uint)s >> 15), 1 | s);
                t = (t + Imul(t ^ (int)((uint)t >> 7), 61 | t)) ^ t;
                return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296f;
            }
        }

        /// <summary>미사일이 길을 얼마나 왔나 (0~1).</summary>
        public static float MissileProgress(float t) => Mathf.Pow(Clamp01((t - MissileStart) / (Hit - MissileStart)), 1.35f);

        /// <summary>미사일 길(베지어) 위의 점.</summary>
        public static Vector3 Bezier(float s)
        {
            float a = (1f - s) * (1f - s);
            float b = 2f * s * (1f - s);
            float c = s * s;
            Vector3 end = Impact + new Vector3(0f, 1.2f, 0f);
            return a * MissileFrom + b * MissileBend + c * end;
        }

        /// <summary>미사일 길의 중심 (나선 없이).</summary>
        public static Vector3 MissileBase(float t) => Bezier(MissileProgress(t));

        /// <summary>미사일 길의 방향 · 옆 · 위 (시안 mFrame).</summary>
        public static void MissileFrame(float t, out Vector3 forward, out Vector3 side, out Vector3 up, out float s)
        {
            s = MissileProgress(t);
            float a = 2f * (1f - s);
            float b = 2f * s;
            Vector3 end = Impact + new Vector3(0f, 1.2f, 0f);
            forward = (a * (MissileBend - MissileFrom) + b * (end - MissileBend)).normalized;
            side = new Vector3(forward.z, 0f, -forward.x).normalized;
            up = Vector3.Cross(forward, side);
        }

        /// <summary>미사일 위치 — 제 축으로 돌며 작게 원을 그린다.</summary>
        public static Vector3 MissilePos(float t)
        {
            MissileFrame(t, out _, out Vector3 side, out Vector3 up, out float s);
            Vector3 b = Bezier(s);
            float r = (1.1f + 2.6f * (1f - s)) * (1f - Smooth((s - 0.93f) / 0.07f));
            float th = Spin(s, t);
            return b + side * (Mathf.Cos(th) * r) + up * (Mathf.Sin(th) * r);
        }

        /// <summary>미사일 코가 보는 방향.</summary>
        public static Vector3 MissileDir(float t)
        {
            MissileFrame(t, out Vector3 forward, out Vector3 side, out Vector3 up, out float s);
            float th = Spin(s, t);
            float k = 0.13f * (1f - Smooth((s - 0.9f) / 0.1f));
            return (forward + side * (-Mathf.Sin(th) * k) + up * (Mathf.Cos(th) * k)).normalized;
        }

        /// <summary>미사일 꽁무니 (비행운이 나오는 곳).</summary>
        public static Vector3 MissileTail(float t) => MissilePos(t) - MissileDir(t) * 7.3f;

        private static float Spin(float s, float t) => Mathf.PI * 2f * 7.5f * Mathf.Pow(s, 1.1f) + t * 1.5f;

        private static int Imul(int a, int b) => unchecked((int)((uint)a * (uint)b));

        // 0 · p1 · p2 · 1 을 잇는 3차 베지어의 한 축
        private static float BezierAt(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }

        private static float BezierSlope(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * p1 + 6f * u * t * (p2 - p1) + 3f * t * t * (1f - p2);
        }

        private static float[] BuildTable()
        {
            var table = new float[4096];
            int seed = 9731;
            for (int i = 0; i < table.Length; i++)
                table[i] = Next(ref seed);
            return table;
        }
    }
}
