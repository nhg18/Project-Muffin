using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerSeat : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text myTurnText;

    public void SetNicknameUI(string nickname)
    {
        nicknameText.text = nickname;
    }

    public void SetTurnUI(bool isTurn)
    {
        myTurnText.text = isTurn ? "Turn" : "";
    }
}
