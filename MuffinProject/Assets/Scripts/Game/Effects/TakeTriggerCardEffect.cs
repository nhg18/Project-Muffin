using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 함정을 발동시킨 행동 카드를 버림 더미에서 쓴 사람(함정 주인) 손패로 (TakeTriggerCard — 3007 두고 보자). 그 카드의 효과는 이미 받았다 — 막지 않는다.
    /// 그 카드가 버림 더미에 없으면(앞 함정이 먼저 가져갔다 등) 발동 전에 다시 보아 슬롯에 남는다 (GameServer.StillHolds). 그래도 없으면 불발.
    /// </summary>
    [Serializable]
    public sealed class TakeTriggerCardEffect : CardEffect
    {
        internal override bool GainsHand => true;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.TakeFromDiscard(entry.User, entry.TriggerCardInstanceId);
        }
    }
}
