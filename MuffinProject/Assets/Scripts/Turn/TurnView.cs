using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnView : MonoBehaviour
{
    [Header("UI Elements(The button is for Debugging)")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TMP_Text turnIndicatorText;

    public event Action OnEndTurnRequested;

    private void Awake()
    {
        if(endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(() => OnEndTurnRequested?.Invoke());
        }
    }

    public void updateTurnUI(bool isMyTurn, int currentTurnActor)
    {
        if (isMyTurn)
        {
            turnIndicatorText.text = "나의 턴!";
            turnIndicatorText.color = Color.green;
            endTurnButton.interactable = true;
        }
        else
        {
            turnIndicatorText.text = $"플레이어 {currentTurnActor}의 턴";
            turnIndicatorText.color = Color.red;
            endTurnButton.interactable = false;
        }
    }

}
