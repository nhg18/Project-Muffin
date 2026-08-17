using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Card_Condition : ScriptableObject
{
    public abstract string CheckCondition(Player caster, Player target=null);
}
