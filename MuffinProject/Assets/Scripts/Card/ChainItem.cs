using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChainItem
{
    public int cardID;
    public int usePlayerNumber;
    public List<int> targetPlayerNumber;

    //카드 취소 여부 및 감소효과
    public bool isCanceled = false;
    public float valueMultiplier = 1.0f;

    public ChainItem(int cardID, int caster, List<int> target)
    {
        this.cardID = cardID;
        this.usePlayerNumber = caster;
        this.targetPlayerNumber = target;
    }
}
