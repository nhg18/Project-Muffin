using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인(시안 CI2) 공용 값 · 셈 — 설계 화면 크기, 먹 · 종이 색, 시안과 같은 이징, 설계 좌표 ↔ RectTransform.
    /// 설계 좌표는 시안 그대로: 1280×720, 왼쪽 위가 (0, 0), 아래로 +, 각도는 라디안 · 시계 방향 +.
    /// 장면 오브젝트는 앵커 · 피벗 기준점을 설계 판 왼쪽 위(앵커 (0, 1))에 두므로 anchoredPosition = (x, −y) 다 — <see cref="Place"/> 로 옮긴다.
    /// 시안: 아티팩트 「찹츄 카드 이펙트 v2」 CI2 (cl · lerp · eo · back · wob — 나머지 이징은 쓰는 장면이 생길 때 더한다).
    /// </summary>
    public static class ComicKit
    {
        /// <summary>설계 화면 너비 (시안 W).</summary>
        public const float Width = 1280f;

        /// <summary>설계 화면 높이 (시안 H).</summary>
        public const float Height = 720f;

        /// <summary>한 바퀴 (라디안).</summary>
        public const float Tau = Mathf.PI * 2f;

        /// <summary>먹색 — 선 · 테 · 글자 (시안 INK #17110f).</summary>
        public static readonly Color Ink = new Color32(0x17, 0x11, 0x0f, 0xff);

        /// <summary>종이색 — 한마디 띠 바탕 (시안 PAPER #fffaf0).</summary>
        public static readonly Color Paper = new Color32(0xff, 0xfa, 0xf0, 0xff);

        /// <summary>a ~ b 로 자른다 (시안 cl).</summary>
        public static float Cl(float v, float a = 0f, float b = 1f) => Mathf.Max(a, Mathf.Min(b, v));

        /// <summary>자르지 않는 보간 (시안 lerp).</summary>
        public static float Lerp(float a, float b, float k) => a + (b - a) * k;

        /// <summary>빠르게 시작해 느리게 끝 (시안 eo — ease-out cubic).</summary>
        public static float Eo(float k) => 1f - Mathf.Pow(1f - Cl(k), 3f);

        /// <summary>목표를 지나쳤다 돌아온다 (시안 back — a 가 클수록 많이 지나친다).</summary>
        public static float Back(float k, float a = 2.2f)
        {
            k = Cl(k);
            return 1f + (a + 1f) * Mathf.Pow(k - 1f, 3f) + a * Mathf.Pow(k - 1f, 2f);
        }

        /// <summary>떨림 — 0 둘레로 출렁이다 멎는다 (시안 wob).</summary>
        public static float Wob(float k, float frequency = 22f, float damping = 8f)
        {
            return k <= 0f ? 0f : Mathf.Exp(-k * damping) * Mathf.Sin(k * frequency);
        }

        /// <summary>설계 좌표 (x, y) 로 옮긴다 — 앵커가 설계 판 왼쪽 위인 오브젝트.</summary>
        public static void Place(RectTransform target, float x, float y)
        {
            target.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>오브젝트의 설계 좌표 (앵커가 설계 판 왼쪽 위일 때).</summary>
        public static Vector2 PositionOf(RectTransform target)
        {
            Vector2 p = target.anchoredPosition;
            return new Vector2(p.x, -p.y);
        }

        /// <summary>시안 각도(라디안, 시계 방향 +)로 돌린다.</summary>
        public static void Turn(RectTransform target, float radians)
        {
            target.localRotation = Quaternion.Euler(0f, 0f, -radians * Mathf.Rad2Deg);
        }

        /// <summary>Graphic 색의 투명도만 바꾼다 (바뀔 때만 다시 그린다).</summary>
        public static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            if (Mathf.Approximately(c.a, alpha))
                return;
            c.a = alpha;
            graphic.color = c;
        }
    }

    /// <summary>시안 rng(seed) — 같은 씨앗이면 시안과 같은 수열 (mulberry32). 값 형식이라 복사하면 그 자리부터 따로 간다.</summary>
    public struct ComicRandom
    {
        private int _state;

        public ComicRandom(int seed)
        {
            _state = seed;
        }

        /// <summary>다음 수 (0 이상 1 미만).</summary>
        public float Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                int t = _state ^ (int)((uint)_state >> 15);
                t *= 1 | _state;
                t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                return (float)((uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0);
            }
        }
    }
}
