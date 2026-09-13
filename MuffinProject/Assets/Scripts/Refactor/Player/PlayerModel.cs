using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerModel
{
    public int ActorNumber { get; }
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }

    public int CurrentHandCount { get; private set; }




    public PlayerModel(int actorNumber, int maxHP)
    {
        ActorNumber = actorNumber;
        MaxHP = maxHP;
        CurrentHP = maxHP;
    }

    public void SetHP(int newHP)
    {
        CurrentHP = Math.Clamp(newHP, 0, MaxHP);
        GameEvents.RaiseHpChanged(CurrentHP);
        
    }

    public void SetHandCount(int newHandCount)
    {
        CurrentHandCount = newHandCount;
        GameEvents.RaiseHandCountChanged(CurrentHandCount);
    }
}
