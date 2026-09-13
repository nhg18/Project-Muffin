using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : CardCollection
{
    public bool isHandMod = false;

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
