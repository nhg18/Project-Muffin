using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상마다 설치 함정 무작위 count 장 파괴 (DestroyTrap — A03). 함정 보호(C12)면 손패로.</summary>
    [Serializable]
    public sealed class DestroyTrapEffect : CardEffect
    {
        public int count = 1;

        internal override EffectTag Tag => EffectTag.DestroyTrap;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.DestroyRandomTraps(actor, count, entry);
        }
    }
}
