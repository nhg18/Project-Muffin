using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카운터가 반응한 카드(체인에서 두 칸 아래)의 적힌 숫자를 2배로 (Double — 2005 묻고 따블로 가, 11-card-list 1-3).
    /// 피해 · 회복 · 뽑기 · 빼앗기 · 버리기 효과가 ChainEntry.Scale 로 읽는다. 한 카드에 1회 — 두 번 걸려도 2배다.
    /// 바로 아래 카운터를 막는 것은 같은 카드의 NegateEffect 가 한다.
    /// </summary>
    [Serializable]
    public sealed class DoubleEffect : CardEffect
    {
        internal override void Apply(GameServer server, ChainEntry entry)
        {
            ChainEntry twoBelow = entry.Below?.Below;
            if (twoBelow != null)
                twoBelow.Doubled = true;
        }
    }
}
