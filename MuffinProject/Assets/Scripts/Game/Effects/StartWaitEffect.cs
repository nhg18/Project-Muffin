using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 쓴 사람의 다음 실제 턴이 시작되기 전까지 아무도 찹츄로 이기지 않는다 (StartWait — 123 기다려!, 11-card-list 123 · 07 4.3).
    /// 처치 승리는 막지 않는다. 쓴 사람이 최종 사망하거나 나가면 즉시 끝. 봇은 쓰지 않는다 (18-bot.md 7-3 ㅁ).
    /// </summary>
    [Serializable]
    public sealed class StartWaitEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.StartWait(entry.User);
    }
}
