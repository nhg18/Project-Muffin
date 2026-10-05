using System.Collections.Generic;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    // GameServer.Card.cs — 카드 규칙 원본과 카드 사용 처리 (04-card.md)
    public partial class GameServer
    {
        private readonly Dictionary<int, CardRule> _cardRules = new Dictionary<int, CardRule>(); // CardId → 규칙

        /// <summary>게임 시작 1회: 이번 게임에 쓰는 카드 규칙을 받는다. InitDeck 보다 먼저 부른다.</summary>
        public void InitCards(IEnumerable<CardRule> rules)
        {
            _cardRules.Clear();
            foreach (CardRule rule in rules)
                _cardRules[rule.Id] = rule;
        }
    }
}
