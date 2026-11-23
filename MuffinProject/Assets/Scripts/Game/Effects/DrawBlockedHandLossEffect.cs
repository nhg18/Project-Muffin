using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 이 카드를 쓴 사람이 「손패 − 내 몫 무효」 로 막은 장수만큼 뽑는다, 최대 max 장 (2019 안 놔!, 11-card-list 1-3).
    /// 카드의 효과 목록에서 NegateHandLossEffect 뒤에 둔다 — 그 효과가 장수를 센다.
    /// </summary>
    [Serializable]
    public sealed class DrawBlockedHandLossEffect : CardEffect
    {
        public int max = 3;

        internal override bool GainsHand => true;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.DrawCards(entry.User, Math.Min(max, server.BlockedHandLoss(entry)), entry);
        }
    }
}
