using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Muffin.Game.Cards;

namespace Muffin.Game
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
        public int GetHandCount()
        {
            return cards.Count;
        }

        public void Sort()
        {

        }


    }
}
