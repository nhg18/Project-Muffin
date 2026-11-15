using System;
using System.Security.Cryptography;

namespace Chapchu.Game
{
    /// <summary>
    /// 덱 섞기 · 무작위 판정용 난수 — 운영체제 암호 난수에서 뽑는다.
    /// System.Random 은 시드 하나로 다음 값이 다 정해져, 나온 카드 몇 장으로 남은 덱 순서를 맞출 수 있다.
    /// GameServer 가 받는 System.Random 자리에 그대로 넣는다 (시험은 시드 있는 System.Random 을 쓴다).
    /// </summary>
    public sealed class SecureRandom : Random
    {
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private readonly byte[] _buffer = new byte[8];

        // [0, 1) — 53비트
        protected override double Sample()
        {
            _rng.GetBytes(_buffer);
            ulong bits = BitConverter.ToUInt64(_buffer, 0) >> 11;
            return bits * (1.0 / (1UL << 53));
        }

        public override int Next() => Next(0, int.MaxValue);

        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return Next(0, maxValue);
        }

        // 나머지 치우침 없이 [min, max)
        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            ulong range = (ulong)((long)maxValue - minValue);
            if (range == 0) return minValue;

            ulong limit = ulong.MaxValue - ulong.MaxValue % range;
            ulong value;
            do
            {
                _rng.GetBytes(_buffer);
                value = BitConverter.ToUInt64(_buffer, 0);
            } while (value >= limit);

            return (int)(minValue + (long)(value % range));
        }

        public override double NextDouble() => Sample();

        public override void NextBytes(byte[] buffer) => _rng.GetBytes(buffer);
    }
}
