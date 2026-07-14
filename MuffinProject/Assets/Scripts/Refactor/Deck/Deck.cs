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
}