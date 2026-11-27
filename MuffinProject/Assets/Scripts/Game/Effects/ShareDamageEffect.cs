using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 반응한 카드(바로 아래)에서 이 카드를 쓴 사람이 받는 피해를 이 카드의 대상도 같은 양만큼 받는다 — 「피해 같이 받기」 (2018 물귀신, 04-card.md 7절).
    /// 막지 않는다 — 쓴 사람은 그대로 받는다. 같은 양 = 실제로 받은 피해(2배 · 감소 뒤). 준 사람은 아래 카드를 쓴 사람이다.
    /// 이 효과의 일은 피해 · 받는 사람은 대상이라, 대상이 이 카드 위에 「피해를 내가 받는다」 카운터를 낼 수 있다.
    /// </summary>
    [Serializable]
    public sealed class ShareDamageEffect : CardEffect
    {
        internal override EffectTag Tag => EffectTag.Damage;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) =>
            Affected(server, entry).Contains(actor) ? server.PredictDamageBelow(entry) : 0;

        internal override void Apply(GameServer server, ChainEntry entry) => server.ShareDamageBelow(entry);
    }
}
