using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

namespace Muffin.Game.Cards
{

    [CreateAssetMenu(fileName = "ActionCardPlayed", menuName = "CardSystem/Condition")]
    public class ActionCardPlayedCondition : CardCondition
    {
        public override string CheckCondition(Player caster, Player target = null)
        {
            return null;
        }
    }
}
