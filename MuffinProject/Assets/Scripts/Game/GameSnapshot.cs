using System;
using System.Collections.Generic;

namespace Chapchu.Game
{
    /// <summary>
    /// 방장 메모리의 게임 상태 전체를 옮겨 담은 것 — 방장 이주용 (09-network.md 9.1). 손패 · 함정 · 덱 순서까지 들어 있다.
    /// 방장이 예비 방장 한 명에게만 보낸다. 카드는 InstanceId 로, 시각은 서버 시각 그대로 담는다.
    /// 무엇을 어떤 순서로 담는지는 GameServer.Snapshot.cs — 여기는 그릇만.
    /// </summary>
    public sealed class GameSnapshot
    {
        public int[] Ints { get; }
        public double[] Times { get; }

        public GameSnapshot(int[] ints, double[] times)
        {
            Ints = ints ?? Array.Empty<int>();
            Times = times ?? Array.Empty<double>();
        }
    }

    // 쓰는 순서 = 읽는 순서. 목록은 길이를 먼저 적는다.
    internal sealed class SnapshotWriter
    {
        private readonly List<int> _ints = new List<int>();
        private readonly List<double> _times = new List<double>();

        public void Int(int value) => _ints.Add(value);
        public void Bool(bool value) => _ints.Add(value ? 1 : 0);
        public void Time(double value) => _times.Add(value);

        public void Ints(ICollection<int> values)
        {
            _ints.Add(values.Count);
            _ints.AddRange(values);
        }

        public void Map(Dictionary<int, int> map)
        {
            _ints.Add(map.Count);
            foreach (KeyValuePair<int, int> pair in map)
            {
                _ints.Add(pair.Key);
                _ints.Add(pair.Value);
            }
        }

        public GameSnapshot ToSnapshot() => new GameSnapshot(_ints.ToArray(), _times.ToArray());
    }

    // 모자라거나 맞지 않으면 예외 — 받는 쪽(GameServer.Restore)이 잡아 「복원 실패」 로 돌려준다.
    internal sealed class SnapshotReader
    {
        // 목록 하나의 길이 상한 — 남이 보낸 큰 값으로 메모리를 잡지 않게. 카드 · 사람 수보다 넉넉하다
        private const int MaxCount = 4096;

        private readonly int[] _ints;
        private readonly double[] _times;
        private int _nextInt;
        private int _nextTime;

        public SnapshotReader(GameSnapshot snapshot)
        {
            _ints = snapshot.Ints;
            _times = snapshot.Times;
        }

        public bool AtEnd => _nextInt == _ints.Length && _nextTime == _times.Length;

        public int Int()
        {
            if (_nextInt >= _ints.Length)
                throw new InvalidOperationException("스냅샷이 모자란다 (int)");
            return _ints[_nextInt++];
        }

        public bool Bool() => Int() != 0;

        public double Time()
        {
            if (_nextTime >= _times.Length)
                throw new InvalidOperationException("스냅샷이 모자란다 (time)");
            return _times[_nextTime++];
        }

        public int Count()
        {
            int count = Int();
            if (count < 0 || count > MaxCount)
                throw new InvalidOperationException($"스냅샷의 목록 길이가 이상하다 ({count})");
            return count;
        }

        public int[] Ints()
        {
            var values = new int[Count()];
            for (int i = 0; i < values.Length; i++)
                values[i] = Int();
            return values;
        }

        public void Map(Dictionary<int, int> into)
        {
            int count = Count();
            for (int i = 0; i < count; i++)
            {
                int key = Int();
                into[key] = Int();
            }
        }
    }
}
