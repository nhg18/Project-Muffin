using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDamageEffect", menuName = "CardSystem/Effects/Damage")]
public class DamageEffect : CardEffect
{

    [Header("데미지량")]
    public int damageAmount;

    public override void Execute(Player caster, Player target)
    {
        if (target != null)
        {
            //damage 로직
            Debug.Log($"{target.NickName}에게 {damageAmount}의 데미지!");
        }
    }
}
