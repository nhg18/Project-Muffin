using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CardModel
{
    public CardData cardData;
    public void Setup(CardData data)
    {
        cardData = data;
    }
}
