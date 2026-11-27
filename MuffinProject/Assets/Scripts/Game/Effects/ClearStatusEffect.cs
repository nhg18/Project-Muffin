using System;
using System.Collections.Generic;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 받는 사람의 막는 상태를 푼다 — 다음 턴 스킵 · 행동 카드 금지(다음 턴 · 이번 턴). 찹츄는 그대로 (ClearStatus — 3012 편안).
    /// **방금 걸린 종류는 풀지 않는다** — 「어차피 쉬는 김에 푹 쉰다」 (프로토타입 v3.4, 사용자 2026-11-27, 11-card-list 1-4). 종류 = 함정을 부른 사건의 값 (TriggerAmount).
    /// </summary>
    [Serializable]
    public sealed class ClearStatusEffect : CardEffect
    {
        public EffectSubject subject;

        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.ClearBlockStatus(actor, entry.TriggerAmount);
        }
    }
}
