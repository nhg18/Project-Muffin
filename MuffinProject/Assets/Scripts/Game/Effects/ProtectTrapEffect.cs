using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>반응한 카드가 이 카운터를 낸 사람의 함정을 파괴하면, 파괴 대신 손패로 (ProtectTrap — C12).</summary>
    [Serializable]
    public sealed class ProtectTrapEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.MarkBelow(entry, below => below.ProtectTraps.Add(entry.User));
    }
}
