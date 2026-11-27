using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>막힌 카드를 받을 사람. 에셋에는 순서 번호로 저장된다 — 끝에만 추가한다.</summary>
    public enum ReturnReceiver
    {
        Caster,     // 그 카드를 쓴 사람 (2003 진짜 할거야?)
        CounterUser // 이 카운터를 낸 사람 (2020 내 거야!)
    }

    /// <summary>반응한 카드를 효과 없이 to 의 손패로 (ReturnToHand — C01 · 2003 · 2020). 받을 사람이 게임에서 나갔으면 버림 더미로.</summary>
    [Serializable]
    public sealed class ReturnToHandEffect : CardEffect
    {
        public ReturnReceiver to;

        internal override void Apply(GameServer server, ChainEntry entry) => server.ReturnBelowToHand(entry, to);
    }
}
