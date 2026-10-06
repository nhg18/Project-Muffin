using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{

    public class PlayerHand : CardCollection
    {
        public bool isHandMode = false;

        public void DiscardCard(int index)
        {
            cards.RemoveAt(index);
        }
        public int GetHandCount()
        {
            return cards.Count;
        }
    }
}
