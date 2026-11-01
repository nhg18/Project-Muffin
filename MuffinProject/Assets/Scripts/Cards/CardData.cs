using System.Collections.Generic;
using Chapchu.Game;
using UnityEngine;

namespace Chapchu.Cards
{
    /// <summary>
    /// 카드 종류 1개의 정의 (에셋, 05-deck.md 8절). "이 카드가 무엇인가" 만 담는다 — 덱에 몇 장인지는 DeckData, 게임 안의 한 장은 Card.
    /// 화면은 CardId 로 이것을 찾아 그린다.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "CardSystem/Card Data")]
    public class CardData : ScriptableObject
    {
        public int id;
        public string cardName;
        public Sprite cardImage;
        public CardType type;
        public TargetType targetType;

        [TextArea]
        public string description;

        [Header("카드 효과 — 종류 · 받는 사람 · 수치. 위에서부터 순서대로 실행")]
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
