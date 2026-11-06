using System.Collections.Generic;

namespace Chapchu.Game
{
    /// <summary>
    /// 처리 체인에 오른 카드 한 장 (04-card.md 9절). 대상은 낼 때 확정되고 바뀌지 않는다 (04 5 · 10절).
    /// 무효화 표시는 위에 쌓인 카운터가 처리될 때 붙는다 — 체인은 위에서부터 처리하므로 아래 카드보다 먼저 정해진다.
    /// </summary>
    internal sealed class ChainEntry
    {
        public Card Card { get; }
        public int User { get; }
        public int[] Targets { get; }

        /// <summary>효과 전체가 무효화됐다 — 효과 없이 버림 더미로.</summary>
        public bool Negated;

        /// <summary>「본인에게만 무효화」 한 사람들 (C05). 효과를 받는 사람에서 뺀다.</summary>
        public readonly HashSet<int> Excluded = new HashSet<int>();

        public ChainEntry(Card card, int user, int[] targets)
        {
            Card = card;
            User = user;
            Targets = targets;
        }
    }
}
