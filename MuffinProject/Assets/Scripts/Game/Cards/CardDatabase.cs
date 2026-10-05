using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.Game.Cards
{

    [CreateAssetMenu(fileName ="CardDatabase", menuName = "CardSystem/Database")]
    public class CardDatabase : ScriptableObject
    {
        [SerializeField] private List<CardData> CardAssets;
        private Dictionary<int, CardData> cardDict;

        public IReadOnlyList<CardData> Cards => CardAssets;

        public void Initialize()
        {
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
    }
}
