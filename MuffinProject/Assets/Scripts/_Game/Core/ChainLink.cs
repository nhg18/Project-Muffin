using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// 처리 체인의 한 칸 (04-card.md 9절). 낸 사람 · 카드 · 사용할 때 확정된 대상.
    /// </summary>
    public class ChainLink
    {
        public Card Card { get; }
        public int OwnerId { get; }

        /// <summary>사용할 때 확정된 대상. 처리 중 바뀌지 않는다 (04-card.md 5절).</summary>
        public IReadOnlyList<int> Targets { get; }

        /// <summary>위 칸의 카운터에 무효화되면 true — 효과 없이 버림 더미로 간다.</summary>
        public bool IsNegated { get; set; }

        public ChainLink(Card card, int ownerId, IReadOnlyList<int> targets)
        {
            Card = card;
            OwnerId = ownerId;
            Targets = targets;
        }
    }
}
