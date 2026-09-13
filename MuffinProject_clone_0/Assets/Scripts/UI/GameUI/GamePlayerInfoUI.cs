using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GamePlayerInfoUI : MonoBehaviour
{
    [Header("기본 정보")]
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text cardsCountText;
    
    public void Refresh(PlayerInfoData data)
    {
        SetNickname(data.Nickname);
        SetHP(data.HP);
        SetCardsCount(data.CardsCount);
        SetChapChu(data.IsChapChu);
    }
    
    private void SetNickname(string nickname)
    {
        if (nicknameText != null)
            nicknameText.text = nickname;
    }

    private void SetHP(float hp)
    {
        if (hpText != null)
            hpText.text = $"{hp}";
    }

    private void SetCardsCount(int count)
    {
        if (cardsCountText != null)
            cardsCountText.text = $"{count}";
    }

    private void SetChapChu(bool isChapChu)
    {
        
    }
}
