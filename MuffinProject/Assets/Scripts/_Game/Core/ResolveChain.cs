using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// 처리 체인 (04-card.md 8 · 9절). 카드를 쌓고, 쌓을 때마다 반응 시간을 처음부터 다시 잰다.
    /// 반응 시간이 끝나면 맨 위부터 꺼내 처리한다.
    /// </summary>
    public class ResolveChain
    {
        private readonly List<ChainLink> _links = new List<ChainLink>();

        /// <summary>처리할 카드가 남아 있는가. 열려 있는 동안은 턴 마감을 보지 않는다.</summary>
        public bool IsOpen => _links.Count > 0;

        public double ReactionDeadline { get; private set; }

        /// <summary>맨 위에 쌓고 반응 시간을 다시 시작한다.</summary>
        public void Push(ChainLink link, double now)
        {
            _links.Add(link);
            ReactionDeadline = now + Rules.ReactionSeconds;
        }

        public bool IsReactionOver(double now) => IsOpen && now >= ReactionDeadline;

        /// <summary>맨 위 칸을 꺼낸다. 마지막에 쌓인 카드부터 처리한다 (04-card.md 9.1절).</summary>
        public ChainLink Pop()
        {
            int top = _links.Count - 1;
            ChainLink link = _links[top];
            _links.RemoveAt(top);
            return link;
        }
    }
}
