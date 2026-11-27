using System;
using System.Collections.Generic;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 받는 사람의 막기 상태를 모두 푼다 — 다음 턴 스킵 · 행동 카드 금지(다음 턴 · 이번 턴). 찹츄는 그대로 (ClearStatus — 3012 편안).
    /// 방금 걸린 막기도 같이 풀린다 — 카드 문구 「상태를 모두 푼다」 그대로.
    /// </summary>
    [Serializable]
    public sealed class ClearStatusEffect : CardEffect
    {
        public EffectSubject subject;

        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.ClearBlockStatus(actor);
        }
    }
}
