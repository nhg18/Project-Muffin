namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 AIMC 의 rng(seed) — mulberry32. 씨앗이 같으면 시안과 같은 수열이 나와 나무 · 눈 둔덕 · 눈발 자리가 시안과 같다.
    /// 수열이 한 번이라도 어긋나면 그 뒤가 모두 달라지므로, 시안이 그리지 않고 수만 뽑는 곳도 같은 수만큼 뽑는다 (<see cref="Skip"/>).
    /// </summary>
    public sealed class AimRandom
    {
        private const int Increment = 0x6D2B79F5;
        private const double Range = 4294967296.0;

        private int _state;

        /// <summary>씨앗으로 시작한다.</summary>
        public AimRandom(int seed)
        {
            _state = seed;
        }

        /// <summary>처음부터 다시 — 매 프레임 같은 씨앗으로 같은 눈발을 그릴 때.</summary>
        public void Reset(int seed)
        {
            _state = seed;
        }

        /// <summary>시안 R() — 0 이상 1 미만.</summary>
        public double NextDouble()
        {
            unchecked
            {
                _state += Increment;
                int t = (_state ^ (int)((uint)_state >> 15)) * (1 | _state);
                t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                return (uint)(t ^ (int)((uint)t >> 14)) / Range;
            }
        }

        /// <summary>시안 R() 를 float 로 — 좌표 · 크기에 쓴다.</summary>
        public float Next()
        {
            return (float)NextDouble();
        }

        /// <summary>시안 R() &lt; p — 갈림 비교는 시안처럼 double 로 해서 수열이 갈리지 않게.</summary>
        public bool Below(double p)
        {
            return NextDouble() < p;
        }

        /// <summary>그리지 않고 수만 count 개 뽑아 버린다.</summary>
        public void Skip(int count)
        {
            for (int i = 0; i < count; i++)
                NextDouble();
        }
    }
}
