using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카드(체인에서 바로 아래 카드)의 손패 감소 효과에서 이 카드를 쓴 사람만 뺀다 — 「무효 (내 몫 · 손패 −)」 (2019 안 놔!, 04-card.md 7절).
    /// 다른 사람 · 그 카드의 다른 효과(피해 등)는 그대로다. 잃을 뻔한 장수를 이때 세어 아래 카드에 남긴다 (DrawBlockedHandLossEffect 가 쓴다).
    /// </summary>
    [Serializable]
    public sealed class NegateHandLossEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.BlockHandLossBelow(entry);
        }
    }
}
