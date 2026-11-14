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

        internal override EffectTag Tag => EffectTag.Damage; // 고르기 전에는 피해일 수 있다 — 「나에게 피해」 반응 조건은 받는 사람 기준
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry) => server.ReceiversOf(EffectSubject.Target, entry);
        internal override int PredictDamage(GameServer server, ChainEntry entry, int actor) => Affected(server, entry).Contains(actor) ? amount : 0;

        // 18-bot 7-3 (사용자 2026-11-13 · 프로토타입 v2): 대상은 손패 × 3 + (HP ≤ amount 면 40) 이 가장 큰 상대 — 그 식이 그대로 점수다.
        // HP ≤ amount 인 찹츄 상대는 버리기를 고를 수밖에 없어 찹츄가 깨진다. 어느 쪽을 골라도 큰 손해라 바탕 점수가 있다
        private const double BotBaseScore = 40;

        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            if (receivers.Length != 1)
                return 0;

            int target = receivers[0];
            bool mustDiscard = view.Server.PlayerHp(target) <= amount;
            if (mustDiscard && view.SeesChapchu(target))
                view.Notes.Add("찹츄 깨기");
            return BotBaseScore + view.Server.HandCountOf(target) * 3 + (mustDiscard ? 40 : 0) + (mustDiscard && view.SeesChapchu(target) ? Bots.BotView.BreakChapchu : 0);
        }

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
