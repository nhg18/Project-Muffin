using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum Type
{
    Action,
    Counter,
    Trap
}

public enum TargetType
{
    None,
    SingleEnemy,    
    TwoEnemy,
    AllEnemies,
    Me,
    AllPlayers
}


[CreateAssetMenu(fileName = "Card_", menuName = "CardSystem/Card Data")]
public class CardData : ScriptableObject
{
    public int id;
    public string cardName;
    public Sprite cardImage;
    public Type type;
    public TargetType targetType;

    [TextArea]
    public string description;

    [Header("카드 조건 리스트")]
    public List<Card_Condition> conditions = new List<Card_Condition>();

    [Header("카드 효과 리스트")]
    public List<CardEffect> effects = new List<CardEffect>();

    public void PlayCard(Player caster, Player target)
    {
        // 리스트에 담긴 효과들을 위에서부터 순서대로 실행합니다.
        foreach (CardEffect effect in effects)
        {
            effect.Execute(caster, target);
        }
    }

    public string ValidateConditions(Player caster, Player target=null)
    {
        foreach(var condition in conditions)
        {
            string failReason = condition.CheckCondition(caster, target);
            if (!string.IsNullOrEmpty(failReason))
            {
                return failReason;
            }
        }
        return null;
    }

}
