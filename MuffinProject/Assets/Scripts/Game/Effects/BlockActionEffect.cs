using System;
using System.Collections.Generic;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 받는 사람의 다음 실제 턴에 행동 카드 금지 — 겹쳐도 1회 (BlockAction — 107 짖기, 11-card-list 107 · 03-turn.md 7절).
    /// 스킵된 턴은 세지 않는다. 뽑기 · 함정 설치 · 카운터는 그대로. 봇은 쓰지 않는다 (18-bot.md 7-3 ㅁ).
    /// </summary>
    [Serializable]
    public sealed class BlockActionEffect : CardEffect
    {
        public EffectSubject subject;

        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(subject, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.MarkActionBlock(actor);
        }
    }
}
