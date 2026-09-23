using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chapchu.Core;

namespace Chapchu.Game.Cards
{

    [CreateAssetMenu(fileName = "NewDamageEffect", menuName = "CardSystem/Effects/Damage")]
    public class DamageEffect : CardEffect
    {
        [Header("데미지량")]
        public int damageAmount;

        public override void Execute(Player caster, Player target)
        {
            if (target != null)
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (target == null) return;

                int curHp = StatBuffer.Get(target, PlayerProps.Hp);
                int newHp = Mathf.Max(0, curHp - damageAmount);

                StatBuffer.Set(target, PlayerProps.Hp, newHp);

                //damage 로직
                Debug.Log($"{target.NickName}에게 {damageAmount}의 데미지!");
            }
        }
    }
}
