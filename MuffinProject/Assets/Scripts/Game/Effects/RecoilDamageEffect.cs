using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 대상을 못 쓰러뜨렸으면 쓴 사람이 amount 피해 (105 드롭킥). 「쓰러뜨림」 = 이 카드의 피해로 대상 HP 가 0 이 됐다 — 그 순간에 정한다.
    /// 사망 대기에서 살아나도 쓰러뜨린 것이다 (사용자 2026-11-13 · 프로토타입 v2, 11-card-list 4절 #2). 대상이 빠졌으면 불발.
    /// </summary>
    [Serializable]
    public sealed class RecoilDamageEffect : CardEffect
    {
        public int amount;

        internal override EffectTag Tag => EffectTag.Damage;
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Self, entry);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) => Affected(server, entry).Contains(actor) ? amount : 0;

        // 18-bot 7-3 (사용자 2026-11-13): 쓰러뜨릴 수 있으면 먼저 쓴다 (피해 효과의 쓰러뜨림 점수). 못 쓰러뜨리는데 내 HP 가 amount 이하면 쓰지 않는다 (반동으로 내가 쓰러진다)
        internal override bool BotWants(GameServer server, int bot, int[] receivers) =>
            receivers.Any(t => server.PlayerHp(t) <= amount) || server.PlayerHp(bot) > amount;

        // 못 쓰러뜨리면 반동만큼 감점 — 그래서 봇은 이 카드를 거의 마무리에만 쓴다
        internal override double BotScore(Bots.BotView view, int[] receivers) =>
            receivers.Any(t => view.Server.PlayerHp(t) <= amount) ? 0 : view.SelfDamageScore(amount);

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (entry.Targets.Length == 0 || entry.Targets.Any(entry.KnockedDown.Contains))
                return;
            if (!entry.Targets.Any(server.InGame))
                return; // 대상이 빠졌다 — 판정할 상대가 없어 불발 (04 10절)

            foreach (int actor in server.ReceiversOf(EffectSubject.Self, entry))
                server.ApplyDamage(entry, actor, amount);
        }
    }
}
