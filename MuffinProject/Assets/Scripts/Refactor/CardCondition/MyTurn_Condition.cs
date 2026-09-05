using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IsMyTurn", menuName = "CardSystem/Condition")]
public class MyTurn_Condition : Card_Condition
{
    public override string CheckCondition(Player caster, Player target=null)
    {
        if (TurnManager.Instance.CurrentTurnActor != caster.ActorNumber)
        {
            return "내 턴에만 사용할 수 있습니다.";
        }

        // 문제가 없으면 성공(null) 반환
        return null;
    }
}
