using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카드(체인에서 바로 아래 카드)를 무효화한다 (11-card-list.md 5절 Negate · 04-card.md 9절).
    /// selfOnly 면 그 카드의 효과를 이 카드를 쓴 사람에게만 무효화한다 (C05).
    /// </summary>
    [Serializable]
    public sealed class NegateEffect : CardEffect
    {
        public bool selfOnly;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.Negate(entry, selfOnly);
        }
    }
}
