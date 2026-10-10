using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.Game.Cards
{
    // CardType · TargetType 은 서버도 쓰므로 서버 어셈블리(Game/Server/Card.cs)에 있다.

    /// <summary>
    /// 카드 종류 1개의 정의 (에셋). "이 카드가 무엇인가" 만 담는다 — 덱에 몇 장 들어가는지는 DeckData, 게임 안의 한 장은 Card.
    /// 화면은 CardId 로 이것을 찾아 그린다. 방장은 게임 시작 때 이것과 DeckData 로 Card 를 만든다 (PunGameServer.BuildDeck).
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

        [Header("카드 효과 리스트")]
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
