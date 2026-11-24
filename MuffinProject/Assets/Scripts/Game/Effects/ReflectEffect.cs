using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카드(바로 아래)에서 이 카드를 쓴 사람의 몫을 그 카드를 쓴 사람에게 되돌린다 — 「반사 (내 몫)」 (2002 반사, 04-card.md 7절 · 11-card-list 1-3).
    /// 다른 사람의 몫은 그대로다. 빼앗기는 방향이 뒤집힌다. 카운터에 내면 그 카운터의 덤 · 고른 대상만 돌아가고 능력(무효 등)은 그대로 처리된다.
    /// 이 카드가 무효되면 표시도 남지 않는다.
    /// </summary>
    [Serializable]
    public sealed class ReflectEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.ReflectBelow(entry);
    }
}
