using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum CardType
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
    public CardType type;
    public TargetType targetType;

    [TextArea]
    public string description;

    [Header("카드 조건 리스트")]
    public List<CardCondition> conditions = new List<CardCondition>();

    [Header("카드 효과 리스트")]
    public List<CardEffect> effects = new List<CardEffect>();

    public void PlayCard(int caster, int[] targets)
    {
        foreach(int target in targets)
        {
            foreach (CardEffect effect in effects)
            {
                effect.Execute(PhotonNetwork.CurrentRoom.GetPlayer(caster), PhotonNetwork.CurrentRoom.GetPlayer(target));
            }
        }

        StatBuffer.Commit();//효과 씹힘 방지

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
