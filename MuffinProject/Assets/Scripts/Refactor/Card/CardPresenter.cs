using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public async void OnCardDropped()
    {
        Debug.Log("CardDropped!");
        List<int> targets = await SelectPlayer();

        if(targets.Count == 0)
        {
            Debug.Log("no player Selected");
            cardView.do_returnToOrigin();
        }

        foreach (int player in targets)
        {
            Debug.Log("target : "+ player);
        }
    }

    public async Task<List<int>> SelectPlayer()
    {
        List<int> ActorNumbers = new List<int>();
        
        switch (cardModel.cardData.targetType)
        {
            case TargetType.SingleEnemy:
                int pNum = 0;
                pNum = await CardTargetPlayerSelector();
                if (pNum != 0)
                {
                    ActorNumbers.Add(pNum);
                }
                break;
            case TargetType.TwoEnemy:
                int p1Num = 0;
                p1Num = await CardTargetPlayerSelector();
                if (p1Num != 0)
                {
                    ActorNumbers.Add(p1Num);
                }
                int p2Num = 0;
                p2Num = await CardTargetPlayerSelector();
                if (p2Num != 0)
                {
                    ActorNumbers.Add(p2Num);
                }
                break;
            case TargetType.AllEnemies:
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    if (player.IsLocal) continue; // 나 자신 제외
                    ActorNumbers.Add(player.ActorNumber);
                }
                break;
            case TargetType.AllPlayers:
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    ActorNumbers.Add(player.ActorNumber);
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

    public async Task<int> CardTargetPlayerSelector()
    {
        int targetNumber = 0;
        targetNumber = await TargetSelectionManager.Instance.SelectPlayer(20.0f);
        if (targetNumber != 0)
        {
            Debug.Log($"선택 완료! 타겟 : {targetNumber}");
            return targetNumber;
        }
        else
        {
            Debug.Log("시간 초과! 카드 사용이 취소되었습니다.");
            return 0;
        }
    }
}
