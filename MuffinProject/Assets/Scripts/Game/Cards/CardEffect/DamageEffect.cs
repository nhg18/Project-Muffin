using UnityEngine;

namespace Chapchu.Game.Cards
{
    /// <summary>데미지 수치. CardData.ToRule() 이 읽어 CardRule.Damage 로 넘긴다.</summary>
    [CreateAssetMenu(fileName = "NewDamageEffect", menuName = "CardSystem/Effects/Damage")]
    public class DamageEffect : CardEffect
    {
        [Header("데미지량")]
        public int damageAmount;
    }
}
