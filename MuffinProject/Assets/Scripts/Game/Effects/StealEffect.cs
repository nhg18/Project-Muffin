using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상마다 손패에서 무작위 count 장을 쓴 사람에게 (Steal — A04 · T08).</summary>
    [Serializable]
    public sealed class StealEffect : CardEffect
    {
        public int count;

        // 18-bot 7-1 — 상대는 잃고(찹츄에서 멀어지면 더) 나는 얻는다
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            receivers.Sum(actor => view.HandScore(actor, -Math.Min(count, view.Server.HandCountOf(actor))));

        internal override int BotHandGain(Bots.BotView view, int[] receivers) =>
            receivers.Where(actor => actor != view.Bot).Sum(actor => Math.Min(count, view.Server.HandCountOf(actor)));
        internal override EffectTag Tag => EffectTag.TakeHand;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            int n = entry.Scale(count);
            foreach (int actor in server.ReceiversOf(EffectSubject.Target, entry))
                server.StealRandom(actor, entry.User, n, entry.User); // 되돌린 대상(2002 반사)의 자리는 쓴 사람 자신이라 여기서는 아무 일도 없다

            // 2002 반사 — 빼앗기는 방향이 뒤집힌다: 되돌린 사람이 쓴 사람의 카드를 가져온다 (11-card-list 2002)
            foreach (int actor in entry.Targets.Where(t => entry.Reflected.Contains(t) && !entry.Excluded.Contains(t) && server.InGame(t)))
                server.StealRandom(entry.User, actor, n, actor);
        }
    }
}
