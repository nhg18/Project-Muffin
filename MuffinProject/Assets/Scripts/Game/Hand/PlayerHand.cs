using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{

    public class PlayerHand : CardCollection
    {
        public bool isHandMode = false;

        public void AddHandCard(Card card)
        {
            Add(card);
        }

        public void DiscardCard(Card card)
        {
            Remove(card);
        }
        public void DiscardCard(int index)
        {
            cards.RemoveAt(index);
        }

        public void Sort()
        {

        }


    }
}
