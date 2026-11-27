using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 함정을 발동시킨 행동 카드를 잡는다 — 함정 줄이 다 끝난 뒤 버림 더미에서 함정 주인 손패로 (TakeTriggerCard — 3007 두고 보자, 04 9.2). 그 카드의 효과는 이미 받았다 — 막지 않는다.
    /// 한 카드는 한 사람만 잡는다 — 앞 사람이 잡았으면 뒤 사람의 두고 보자는 발동 전에 다시 보아 슬롯에 남는다 (GameServer.StillHolds).
    /// </summary>
    [Serializable]
    public sealed class TakeTriggerCardEffect : CardEffect
    {
        internal override bool GainsHand => true;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.ClaimTriggerCard(entry.User, entry.TriggerCardInstanceId);
        }
    }
}
