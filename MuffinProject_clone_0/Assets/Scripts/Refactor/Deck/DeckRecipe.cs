using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDeckRecipe", menuName = "CardSystem/DeckRecipe")]
public class DeckRecipe : ScriptableObject
{
    [Header("덱 기본 정보")]
    public string deckName = "기본 덱";

    [Header("덱에 포함될 카드 ID 목록")]
    // 인스펙터에서 1, 1, 1, 2, 3... 이런 식으로 입력하게 됩니다.
    public List<Card> cardIDs = new List<Card>();
}
