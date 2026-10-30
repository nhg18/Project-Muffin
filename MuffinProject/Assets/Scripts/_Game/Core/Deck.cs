using System;
using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// 덱과 버림 더미 (05-deck.md). 덱 맨 위에서 1장씩 뽑고,
    /// 덱이 비면 버림 더미 전체를 섞어 새 덱으로 만든다.
    /// </summary>
    public class Deck
    {
        // 리스트의 끝이 덱 맨 위. 끝에서 빼야 뽑을 때 복사가 없다.
        private readonly List<Card> _drawPile = new List<Card>();
        private readonly List<Card> _discardPile = new List<Card>();
        private readonly Random _random;

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        public Deck(IEnumerable<Card> cards, Random random)
        {
            _random = random;
            _drawPile.AddRange(cards);
            _drawPile.Shuffle(_random);
        }

        /// <summary>
        /// 맨 위 1장을 뽑는다. 덱이 비었으면 버림 더미를 섞어 채운 뒤 뽑는다.
        /// 덱과 버림 더미가 모두 비었으면 false.
        /// </summary>
        public bool TryDraw(out Card card)
        {
            if (_drawPile.Count == 0)
                Refill();

            if (_drawPile.Count == 0)
            {
                card = null;
                return false;
            }

            int top = _drawPile.Count - 1;
            card = _drawPile[top];
            _drawPile.RemoveAt(top);
            return true;
        }

        /// <summary>버림 더미 맨 위에 놓는다.</summary>
        public void Discard(Card card) => _discardPile.Add(card);

        private void Refill()
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            _drawPile.Shuffle(_random);
        }
    }
}
