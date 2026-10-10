using UnityEngine;

namespace Chapchu.Game.Cards
{
    /// <summary>데미지 수치. 방장이 덱을 만들 때(PunGameServer.BuildDeck) 읽어 Card.Damage 로 넘긴다.</summary>
    [CreateAssetMenu(fileName = "NewDamageEffect", menuName = "CardSystem/Effects/Damage")]
    public class DamageEffect : CardEffect
    {
        [Header("데미지량")]
        public int damageAmount;
    }
}
