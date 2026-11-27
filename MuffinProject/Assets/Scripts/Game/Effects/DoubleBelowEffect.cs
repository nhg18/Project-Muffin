using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카운터(바로 아래)가 반응했던 카드 — 체인에서 두 칸 아래 — 의 적힌 숫자를 2배로 한다 (2005 묻고 따블로 가, 11-card-list 1-3).
    /// 아래 카운터의 무효는 NegateEffect 가 한다. 이 카드가 무효되면 2배 표시도 남지 않는다.
    /// </summary>
    [Serializable]
    public sealed class DoubleBelowEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry)
        {
            server.DoubleTwoBelow(entry);
        }
    }
}
