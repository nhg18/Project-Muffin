using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 처리되는 시점의 버림 더미 맨 위 1장을 쓴 사람 손패로 (TakeDiscardTop — 109 아 맞다!). 고를 수 없다. 이 카드 자신은 가져올 수 없다.
    /// 더미가 비어 있으면 불발 (11-card-list 1-2). 봇은 뽑기 묶음이라 1단계에서 쓰지 않는다 (18-bot.md 7-3 ㄹ).
    /// </summary>
    [Serializable]
    public sealed class TakeDiscardTopEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry) => server.TakeDiscardTop(entry.User, entry);
    }
}
