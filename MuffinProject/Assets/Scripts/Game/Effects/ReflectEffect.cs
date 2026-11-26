using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카드(바로 아래)에서 내 몫을 쓴 사람에게 되돌린다 (Reflect — 2002 반사, 11-card-list 1-3).
    /// 그 카드를 처리할 때 받는 사람에서 나 대신 쓴 사람이 받는다 (GameServer.ReceiversOf). 빼앗기는 방향이 뒤집힌다 (StealEffect).
    /// 다른 사람의 몫은 그대로다. 카운터에 내면 그 카운터의 덤만 돌아간다 — 능력(무효 등)은 받는 사람이 없어 그대로다.
    /// </summary>
    [Serializable]
    public sealed class ReflectEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.MarkBelow(entry, below => below.Reflected.Add(entry.User));
        }
    }
}
