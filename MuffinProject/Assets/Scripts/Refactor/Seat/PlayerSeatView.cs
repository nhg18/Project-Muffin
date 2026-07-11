using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerSeatView : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text myTurnText;

    public void SetNickname(string nickname)
    {
        nicknameText.text = nickname;
    }

    public void SetMyTurn(bool isMyTurn)
    {
        myTurnText.text = isMyTurn ? "My Turn" : "";
    }
}
