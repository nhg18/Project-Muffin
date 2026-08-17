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
}
