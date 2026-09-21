using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName ="CardDatabase", menuName = "CardSystem/Database")]
public class CardDatabase : ScriptableObject
{
    public static CardDatabase Instance { get; private set; }

    [SerializeField] private List<CardData> CardAssets;
    private Dictionary<int, CardData> cardDict;

    public void Initialize()
    {
        Instance = this;
        cardDict = new Dictionary<int, CardData>();
        foreach(var data in CardAssets)
        {
            cardDict[data.id] = data;
        }
    }

    public CardData GetCard(int id)
    {
        if (cardDict == null) Initialize();
        return cardDict.ContainsKey(id) ? cardDict[id] : null;
    }

    public List<Card> GetCardList()
    {
        List<Card> cards = new List<Card>();
        for(int i = 0; i < CardAssets.Count; i++)
        {
            cards.Add(new Card(CardAssets[i].id));
        }
        return cards;
    }
}
