using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 대상이 고른다 — 0 손패 전부 버리기 · 1 amount 피해 받기 (119 목표 조준, 11-card-list 119). seconds 안에 안 고르면 방장이 무작위.
    /// 고르는 동안 처리가 멈춘다 (GameServer.Choice.cs). 손패가 0장이어도 버리기를 고를 수 있다 — 아무 손해 없이 끝.
    /// </summary>
    [Serializable]
    public sealed class DiscardOrDamageEffect : CardEffect
    {
        public const int DiscardAll = 0, TakeDamage = 1;

        public int amount;
        public float seconds;

        internal override Bots.BotRole BotRole => Bots.BotRole.Attack;
        internal override EffectTag Tag => EffectTag.Damage; // 고르기 전에는 피해일 수 있다 — 「나에게 피해」 반응 조건은 받는 사람 기준
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) => Affected(server, entry).Contains(actor) ? amount : 0;

        // 18-bot 7-2 · 7-3 (사용자 2026-11-13 · 프로토타입 v2): 찹츄 상대(7-1 줄 3 이 먼저 넘긴다), 아니면 손패 × 3 + (HP ≤ amount 면 40) 이 가장 큰 상대
        internal override int BotPreferredTarget(GameServer server, int bot, int[] pickable) =>
            pickable.Length == 0 ? -1 : pickable.OrderByDescending(t => server.HandCountOf(t) * 3 + (server.PlayerHp(t) <= amount ? 40 : 0)).First();

        /// <summary>봇이 대상일 때 (프로토타입 v2): HP ≤ amount 면 버리기, 아니면 손패 5장 이상이거나 찹츄면 피해, 그 밖은 버리기.</summary>
        internal int BotOption(GameServer server, int bot)
        {
            if (server.PlayerHp(bot) <= amount)
                return DiscardAll;
            return server.HandCountOf(bot) >= 5 || server.StateOf(bot).IsChapchu ? TakeDamage : DiscardAll;
        }

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            int[] receivers = server.ReceiversOf(EffectSubject.Target, entry);
            if (receivers.Length > 0)
                server.BeginChoice(entry, this, receivers[0]); // 대상은 1명 (OneOther)
        }

        internal void ApplyOption(GameServer server, ChainEntry entry, int actor, int option)
        {
            if (!server.InGame(actor))
                return;
            if (option == DiscardAll)
                server.DiscardRandom(actor, server.HandCountOf(actor), entry.User);
            else
                server.ApplyDamage(entry, actor, amount);
        }
    }
}
