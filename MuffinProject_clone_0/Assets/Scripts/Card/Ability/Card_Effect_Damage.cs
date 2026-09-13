using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Card_Effect_Damage : Card_Effect
{
    public int damageAmount = 5;
    public override void Excute(int casterViewID, int[] targetViewID)
    {
        Debug.Log("µ¥¹ÌÁö!!!!!!!!!");
    }
}
