using DG.Tweening.Plugins;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//public class CardData
//{
//    public int CardID;

//}

public class DeckModel : IDeck
{
    List<int> cardIDs = new List<int>();

    public void InitDeck(List<int> initialCards)
    {
        cardIDs = new List<int>(initialCards);
    }

    public void SyncDeck(int[] newDeck)
    {
        cardIDs = new List<int>(newDeck);
    }

    public int Draw()
    {
        if (cardIDs.Count == 0) return -1;

        int idx = UnityEngine.Random.Range(0, cardIDs.Count); // 카드를 뽑는 로직은 랜덤이긴한데, 추후 회의에서 수정필요
        int drawCard = cardIDs[idx];
        cardIDs.RemoveAt(idx);


        return drawCard;
    }


    public List<int> getCurrentDeck()
    {
        return cardIDs;
    }

}
