using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상의 설치 함정 무작위 1장을 쓴 사람의 빈 함정 칸으로 (StealTrap — A17). 빈 칸이 없으면 불발.</summary>
    [Serializable]
    public sealed class StealTrapEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.StealRandomTrap(actor, entry.User);
        }
    }
}
