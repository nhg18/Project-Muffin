using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardAbility : MonoBehaviour
{
    public enum AbilityType
    {
        Attack,
        Heal,
        Draw,
        Throw,
        Steal
    }
    [Header("Ability List")]
    [SerializeField] private List<AbilityType> abilityList = new List<AbilityType>();

    [Header("Values")]
    [SerializeField] private float card_damage = 10f;
    [SerializeField] private float card_heal = 10f;
    [SerializeField] private int card_draw = 1;
    [SerializeField] private int card_throw = 1;
    [SerializeField] private int card_steal = 1;



    public void ExecuteActions(List<int> result)
    {
        foreach (int i in result)
        {
            foreach (AbilityType action in abilityList)
            {
                ExecuteAction(action, i);
            }

        }

    }

    private void ExecuteAction(AbilityType action, int targetNumber)
    {
        switch (action)
        {
            case AbilityType.Attack: Attack(targetNumber); break;
            case AbilityType.Heal:  break;
            case AbilityType.Draw:  break;
            case AbilityType.Throw:  break;
            case AbilityType.Steal:  break;
        }
    }

    private void Attack(int targetNumber)
    {
        Attack_effect();
        //GameRule.Instance.RequestAttack(targetNumber,card_damage);
    }
    private void Attack_effect()
    {

    }
}
