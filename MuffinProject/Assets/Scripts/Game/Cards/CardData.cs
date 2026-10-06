using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chapchu.Game.Cards
{
    // CardType · TargetType 은 서버도 쓰므로 서버 어셈블리(Game/Server/CardRule.cs)에 있다.

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

        [Header("카운터")]
        [Tooltip("반응한 행동 카드의 효과를 나에게만 무효로 한다 (C05). 행동 카드에만 반응할 수 있다.")]
        public bool negateForSelf;

        /// <summary>서버(GameServer)가 판정에 쓰는 규칙만 뽑는다. 데미지는 effects 의 DamageEffect 수치를 합친다.</summary>
        public CardRule ToRule()
        {
            int damage = effects.OfType<DamageEffect>().Sum(e => e.damageAmount);
            return new CardRule(id, type, targetType, damage, negateForSelf);
        }
    }
}
