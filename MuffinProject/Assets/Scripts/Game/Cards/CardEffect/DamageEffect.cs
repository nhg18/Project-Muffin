using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Muffin.Game.Cards
{

    [CreateAssetMenu(fileName = "NewDamageEffect", menuName = "CardSystem/Effects/Damage")]
    public class DamageEffect : CardEffect
    {
        private const string HP_KEY = "HP";

        [Header("데미지량")]
        public float damageAmount;

        public override void Execute(Player caster, Player target)
        {
            if (target != null)
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (target == null) return;

                float curHP = StatBuffer.Get(target, HP_KEY);
                float newHp = Mathf.Max(0, curHP - damageAmount);

                StatBuffer.Set(target, HP_KEY, newHp);

                //damage 로직
                Debug.Log($"{target.NickName}에게 {damageAmount}의 데미지!");
            }
        }
    }
}
