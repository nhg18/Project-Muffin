using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionCardPlayed", menuName = "CardSystem/Condition")]
public class ActionCardPlayedCondition : Card_Condition
{
    public override string CheckCondition(Player caster, Player target = null)
    {
        return null;
    }
}
