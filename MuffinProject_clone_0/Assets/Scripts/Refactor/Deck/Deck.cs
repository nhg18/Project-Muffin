using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Deck : CardCollection
{
    public void InitDeck(List<Card> initialCards)
    {
        cards = new List<Card>(initialCards);
    }

    public void SyncDeck(List<Card> newDeck)
    {
        cards = new List<Card>(newDeck);
    }

    public void Shuffle()
    {
        
    }

    public Card DrawAt(int index)
    {
        var card = cards[index];
        cards.RemoveAt(index);
        return card;
    }

    public Card DrawTop()
    {
        return DrawAt(0);
    }
    
    public List<Card> GetCurrentDeck()
    {
        return new List<Card>(cards);
    }



    //public void AutoAssignIDs() // AUTO ID SETTER IN UNITY INSPECTOR
    //{
    //    if (cards == null || cards.Count == 0) return;

    //    for (int i = 0; i < cards.Count; i++)
    //    {
    //        if (i == 0)
    //        {
    //            cards[i] = new Card(1);
    //        }
    //        else
    //        {
    //            if (cards[i].ID <= cards[i - 1].ID)
    //            {
    //                cards[i] = new Card(cards[i - 1].ID + 1);
    //            }
    //        }
    //    }
    //}


}