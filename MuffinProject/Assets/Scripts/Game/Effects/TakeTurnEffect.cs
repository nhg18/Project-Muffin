using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 이 체인의 처리가 끝나고 지금 턴이 끝나면 다음 턴이 이 카드를 쓴 사람의 것이 된다 — 「턴 가로채기」 (2013 새치기, 03-turn.md 7절 · 11-card-list 1-3).
    /// 아래 카드에 남기는 표시가 아니라 다음 턴을 바꾼다. 이 카드가 무효되면 아무 일도 없다.
    /// </summary>
    [Serializable]
    public sealed class TakeTurnEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.TakeTurn(entry.User);
    }
}
