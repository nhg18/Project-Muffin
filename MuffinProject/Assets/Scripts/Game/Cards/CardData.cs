using Photon.Pun;
using Photon.Realtime;
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

        [Header("카드 조건 리스트")]
        public List<CardCondition> conditions = new List<CardCondition>();

        [Header("카드 효과 리스트")]
        public List<CardEffect> effects = new List<CardEffect>();

        /// <summary>서버(GameServer)가 판정에 쓰는 규칙만 뽑는다. 데미지는 effects 의 DamageEffect 수치를 합친다.</summary>
        public CardRule ToRule()
        {
            int damage = effects.OfType<DamageEffect>().Sum(e => e.damageAmount);
            return new CardRule(id, type, targetType, damage);
        }

        public void PlayCard(int caster, int[] targets)
        {
            foreach(int target in targets)
            {
                foreach (CardEffect effect in effects)
                {
                    effect.Execute(PhotonNetwork.CurrentRoom.GetPlayer(caster), PhotonNetwork.CurrentRoom.GetPlayer(target));
                }
            }

            StatBuffer.Commit();//효과 씹힘 방지

        }

        public string ValidateConditions(Player caster, Player target=null)
        {
            foreach(var condition in conditions)
            {
                string failReason = condition.CheckCondition(caster, target);
                if (!string.IsNullOrEmpty(failReason))
                {
                    return failReason;
                }
            }
            return null;
        }

    }
}
