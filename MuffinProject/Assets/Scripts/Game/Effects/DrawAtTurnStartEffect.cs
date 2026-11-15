using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람의 다음 실제 턴 시작에 count 장 더 뽑기 (DrawAtTurnStart — T15).</summary>
    [Serializable]
    public sealed class DrawAtTurnStartEffect : CardEffect
    {
        public EffectSubject subject;
        public int count;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.AddTurnStartDraws(actor, count);
        }
    }
}
