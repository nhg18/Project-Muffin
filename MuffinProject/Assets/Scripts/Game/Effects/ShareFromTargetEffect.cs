using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 대상을 뺀 모두가 대상 손패에서 무작위 1장씩 가져간다 (ShareFromTarget — 118 무야호). 쓴 사람부터 턴 진행 방향 순서, 카드가 떨어지면 나머지는 못 받는다.
    /// 가져가기는 훔침이다 — 대상이 「손패를 빼앗김」 반응의 대상. 「배불러」(카운터 207, 제안) 예외는 그 카드가 확정될 때까지 없다 (11-card-list 1-2).
    /// 봇은 대상이 내가 아닐 때만 쓴다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class ShareFromTargetEffect : CardEffect
    {
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);

        internal override bool BotWants(GameServer server, int bot, int[] receivers) => receivers.Length == 1 && receivers[0] != bot;

        // 대상은 나눠 준 만큼 잃고, 나를 뺀 나머지는 1장씩 얻는다 (그 1장이 누군가의 10장째면 감점). 내 1장은 BotHandGain
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            if (receivers.Length != 1)
                return 0;

            int target = receivers[0];
            int[] takers = view.Server.ActorsInGame.Where(actor => actor != target).ToArray();
            int given = Math.Min(view.Server.HandCountOf(target), takers.Length);
            double score = view.HandScore(target, -given);
            if (given == takers.Length)
                score += takers.Sum(actor => view.HandScore(actor, 1));
            return score;
        }

        internal override int BotHandGain(Bots.BotView view, int[] receivers) =>
            receivers.Length == 1 && view.Server.HandCountOf(receivers[0]) > 0 ? 1 : 0;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int target in server.ReceiversOf(EffectSubject.Target, entry))
                server.ShareFromTarget(target, entry);
        }
    }
}
