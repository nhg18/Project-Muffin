using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardPresenter : MonoBehaviour
{
    private CardModel cardModel = new CardModel();
    public CardView cardView;

    private void Awake()
    {

    }

    public void Setup(CardData data) //현재 PlayerHandView 에서 호출중
    {
        if (data == null)
        {
            Debug.LogError("null CardData in CardPresenter");
            return;
        }
        cardView.Setup(data);
        cardModel.Setup(data);

    }

    public bool LocalConditionCheck()
    {
        string conditionMet = cardModel.cardData.ValidateConditions(PhotonNetwork.LocalPlayer);
        if (string.IsNullOrEmpty(conditionMet))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void OnCardDropped()
    {
        Debug.Log("CardDropped!");



    }

    public List<int> SelectPlayer()
    {
        List<int> ActorNumbers = new List<int>();
        
        switch (cardModel.cardData.targetType)
        {
            case TargetType.SingleEnemy:
                
                break;
            case TargetType.TwoEnemy:
                break;
            case TargetType.AllEnemies:
                for (int i = 1; i <= 4; i++)
                {
                    if (PhotonNetwork.LocalPlayer.ActorNumber == i) continue;
                    ActorNumbers.Add(i);
                }
                break;
            case TargetType.AllPlayers:
                for(int i = 1; i <= 4; i++)
                {
                    ActorNumbers.Add(i);
                }
                break;
            case TargetType.Me:
                ActorNumbers.Add(PhotonNetwork.LocalPlayer.ActorNumber);
                break;
            case TargetType.None:
                break;

        }
        return ActorNumbers;
    }
}
