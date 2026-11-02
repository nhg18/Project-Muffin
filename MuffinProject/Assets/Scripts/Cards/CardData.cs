using System.Collections.Generic;
using Chapchu.Game;
using Chapchu.Game.Effects;
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

        // 위에서부터 순서대로 실행. 인스펙터의 + 메뉴(Editor/CardDataEditor)로 종류를 골라 추가한다.
        [SerializeReference]
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
