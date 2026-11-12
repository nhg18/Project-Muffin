using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 턴 진행 방향을 반대로 (ReverseTurnOrder — 145 뒤로 돌아, 03-turn.md 6절 #4 · 7절). 다시 쓰면 원래 방향. 쌓인 턴 스킵은 그대로.
    /// 생존자가 2명이면 다음 사람을 건너뛰어 쓴 사람의 턴이 다시 온다. 봇은 쓰지 않는다 (18-bot.md 7-3 ㅁ).
    /// </summary>
    [Serializable]
    public sealed class ReverseTurnOrderEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.ReverseTurnOrder(entry.User);
    }
}
