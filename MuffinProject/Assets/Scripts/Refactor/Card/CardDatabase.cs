using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName ="CardDatabase", menuName = "Card System/Database")]
public class CardDatabase : ScriptableObject
{
    public static CardDatabase Instance { get; private set; }

    [SerializeField] private List<CardData> CardAssets;
    private Dictionary<int, CardData> cardDict = new Dictionary<int, CardData>();

    public void Initialize()
    {
        Instance = this;
        foreach(var data in CardAssets)
        {
            cardDict.Add(data.ID, data);
        }
    }

    public CardData GetCard(int id)
    {
        if (cardDict == null) Initialize();
        return cardDict.ContainsKey(id) ? cardDict[id] : null;
    }
}
