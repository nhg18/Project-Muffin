using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSeat : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text hpText;
    
    [SerializeField] private TMP_Text myTurnText;
    
    [SerializeField] private Image myTurnImage;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void SetNicknameUI(string nickname)
    {
        nicknameText.text = nickname;
    }

    public void SetTurnUI(bool isTurn)
    {
        // SetTurnText(isTurn);
        SetTurnImage(isTurn);
    }

    private void SetTurnText(bool isTurn)
    {
        myTurnText.text = isTurn ? "Turn" : "";
    }

    private void SetTurnImage(bool isTurn)
    {
        myTurnImage.enabled = isTurn;
    }
}
