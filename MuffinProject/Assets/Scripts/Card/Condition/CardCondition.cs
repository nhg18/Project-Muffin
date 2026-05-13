using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardCondition : MonoBehaviour
{
    public enum ConditionType
    {
        IsMyTurn,
        AmIChapchu
    }
    [Header("Condition List")]
    [SerializeField] private List<ConditionType> conditionList = new List<ConditionType>();
    [SerializeField] private bool isAND = true;//false이면 OR(조건 하나만 참이여도 만족)

    private Dictionary<ConditionType, Func<bool>> conditionMap;

    void Awake()
    {
        conditionMap = new Dictionary<ConditionType, Func<bool>>
        {
            {ConditionType.IsMyTurn, IsMyTurn },
            {ConditionType.AmIChapchu, AmIChapchu }
        };
    }

    bool IsMyTurn()
    {
        return GameRule.Instance.IsMyTurn;
    }
    bool AmIChapchu()
    {
        return GameRule.Instance.isChapChu;
    }

    public bool CardConditionMet()
    {
        if (isAND)
        {
            return AllConditionsMet();
        }
        else
        {
            return AnyConditionMet();
        }
    }
    
    //모든 조건 충족
    private bool AllConditionsMet()
    {
        foreach(var condition in conditionList)
        {
            if (!conditionMap[condition].Invoke())
            {
                Debug.Log("조건 미충족");
                return false;
            }
        }
        return true;
    }

    //위 조건중 하나라도 충족
    private bool AnyConditionMet()
    {
        foreach (var condition in conditionList)
        {
            if (conditionMap[condition].Invoke())
            {
                Debug.Log($"[조건 충족] {condition}");
                return true;
            }
        }
        return false;
    }

    //특정 조건 충족(아직 필요 X)
    //public bool Check(ConditionType type)
    //{
    //    return conditionMap.TryGetValue(type, out var func) && func.Invoke();
    //}
}
