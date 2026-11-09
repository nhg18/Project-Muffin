using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상마다 설치 함정 무작위 count 장을 그 사람 손패로 (ReturnTrap — A16).</summary>
    [Serializable]
    public sealed class ReturnTrapEffect : CardEffect
    {
        public int count = 1;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.ReturnRandomTraps(actor, count);
        }
    }
}
