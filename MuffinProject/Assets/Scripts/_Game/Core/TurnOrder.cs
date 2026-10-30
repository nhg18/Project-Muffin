using System;
using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// 턴 순서와 턴 마감 시각 (03-turn.md 6절). 시작 때 무작위로 섞고,
    /// 그 뒤로는 순서대로 다음 생존 플레이어에게 넘긴다.
    /// </summary>
    public class TurnOrder
    {
        private readonly List<int> _order;
        private int _index;

        /// <summary>턴 순서대로 나열한 플레이어 ID.</summary>
        public IReadOnlyList<int> Order => _order;

        /// <summary>지금 턴인 플레이어 ID.</summary>
        public int Current => _order[_index];

        /// <summary>지금 턴의 마감 시각. 시간 단위는 Match 에 넣어 주는 now 와 같다.</summary>
        public double Deadline { get; private set; }

        public TurnOrder(IEnumerable<int> playerIds, Random random)
        {
            _order = new List<int>(playerIds);
            _order.Shuffle(random);
        }

        /// <summary>첫 턴을 시작한다.</summary>
        public void Begin(double now) => Deadline = now + Rules.TurnSeconds;

        /// <summary>
        /// 다음 플레이어에게 턴을 넘긴다. isAlive 가 false 인 플레이어는 건너뛴다.
        /// 모두 건너뛰면 지금 플레이어가 그대로 다시 턴을 잡는다.
        /// </summary>
        public void Advance(double now, Func<int, bool> isAlive)
        {
            for (int step = 1; step <= _order.Count; step++)
            {
                int next = (_index + step) % _order.Count;
                if (!isAlive(_order[next]))
                    continue;

                _index = next;
                break;
            }

            Deadline = now + Rules.TurnSeconds;
        }

        /// <summary>지금 턴의 마감이 지났는가.</summary>
        public bool IsExpired(double now) => now >= Deadline;
    }
}
