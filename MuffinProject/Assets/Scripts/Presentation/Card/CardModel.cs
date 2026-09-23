using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Muffin.Game.Cards;

namespace Muffin.Presentation
{

    public class CardModel
    {
        public CardData cardData;
        public int cardIndex;
        public void Setup(CardData data, int index = -1)
        {
            cardData = data;
            cardIndex = index;
        }
    }
}
