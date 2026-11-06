using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람의 다음 턴 스킵 — 겹쳐도 1회 (SkipTurn — A01 · T05, 03-turn.md 7절).</summary>
    [Serializable]
    public sealed class SkipTurnEffect : CardEffect
    {
        public EffectSubject subject;

        internal override EffectTag Tag => EffectTag.SkipTurn;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.MarkSkipTurn(actor);
        }
    }
}
