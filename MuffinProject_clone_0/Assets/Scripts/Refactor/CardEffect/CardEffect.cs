using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public abstract class CardEffect : ScriptableObject
{
    public abstract void Execute(Player caster, Player target);
}
