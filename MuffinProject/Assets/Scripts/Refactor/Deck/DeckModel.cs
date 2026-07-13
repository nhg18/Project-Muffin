using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardData
{
    public int CardID;

}

public class DeckModel : IDeck
{
    List<CardData> cards = new List<CardData>();

    public void InitDeck(List<CardData> initialCards)
    {
        cards = new List<CardData>(initialCards);
    }
    public int Draw()
    {
        if (cards.Count == 0) return -1;

        int idx = UnityEngine.Random.Range(0, cards.Count); // 카드를 뽑는 로직은 pun2를 이용해서 해야할듯
        return cards[idx].CardID;
    }

}
