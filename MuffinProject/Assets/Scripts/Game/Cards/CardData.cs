using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chapchu.Game.Cards
{
    // CardType · TargetType 은 서버도 쓰므로 서버 어셈블리(Game/Server/Card.cs)에 있다.

    /// <summary>카드 종류 1개의 원본 데이터 (에셋). 게임 안의 카드 한 장은 Card — ToCard 로 만든다.</summary>
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

        [Header("덱 구성")]
        [Tooltip("이번 게임 덱에 몇 장 넣는가. 0 이면 덱에 안 들어간다. 매수는 05-deck.md 미정 — 지금 값은 임시")]
        public int deckCount;

        /// <summary>
        /// 이 종류의 Card 를 만든다. 서버는 0 으로 종류 템플릿을 받아 덱을 만들 때 번호를 매기고,
        /// 화면은 OnDrawn 으로 받은 번호를 넣어 손패 카드를 만든다. 데미지는 effects 의 DamageEffect 수치를 합친다.
        /// </summary>
        public Card ToCard(int instanceId)
        {
            int damage = effects.OfType<DamageEffect>().Sum(e => e.damageAmount);
            return new Card(instanceId, id, type, targetType, damage);
        }
    }
}
