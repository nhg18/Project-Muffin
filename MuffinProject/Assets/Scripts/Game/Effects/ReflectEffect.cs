using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 행동 카드(바로 아래)를 무효로 하고, 이 카드를 낸 사람이 그 카드를 쓴 사람에게 다시 쓴다 — 「반사」 (2002, 04-card.md 7절 · 11-card-list 1-3).
    /// 다시 쓸 때 카드의 조건은 따지지 않고, 그 카드의 불이익(1005 의 반동)도 이 카드를 낸 사람이 진다. 이 카드가 무효되면 아무 일도 없다.
    /// </summary>
    [Serializable]
    public sealed class ReflectEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.ReflectBelow(entry);
    }
}
