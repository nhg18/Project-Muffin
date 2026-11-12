using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 대상을 뺀 모두가 대상 손패에서 무작위 1장씩 가져간다 (ShareFromTarget — 118 무야호). 쓴 사람부터 턴 진행 방향 순서, 카드가 떨어지면 나머지는 못 받는다.
    /// 가져가기는 훔침이다 — 대상이 「손패를 빼앗김」 반응의 대상. 「배불러」(카운터 207, 제안) 예외는 그 카드가 확정될 때까지 없다 (11-card-list 1-2).
    /// 봇은 상대 손패를 바꾸는 카드로 본다 — 대상이 내가 아닐 때만 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class ShareFromTargetEffect : CardEffect
    {
        internal override Bots.BotRole BotRole => Bots.BotRole.HandChange;
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);

        internal override bool BotWants(GameServer server, int bot, int[] targets) => !targets.Contains(bot);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int target in server.ReceiversOf(EffectSubject.Target, entry))
                server.ShareFromTarget(target, entry);
        }
    }
}
