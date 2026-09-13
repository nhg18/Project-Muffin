using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Card_Effect_Cancel : Card_Effect
{
    public override void Excute(int casterViewID, int[] targetViewID)
    {
        CardSystem.Instance.RequestCancelNext();
        Debug.Log("¹«È¿È­!!!!!");
    }
}
