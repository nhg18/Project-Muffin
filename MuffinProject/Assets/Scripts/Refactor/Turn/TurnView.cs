using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnView : MonoBehaviour
{
    [Header("UI Elements(The button is for Debugging)")]
    [SerializeField] private Button turnEndButton;
    [SerializeField] private TMP_Text turnIndicatorText;

    private void Awake()
    {
        if(turnEndButton != null)
        {
            turnEndButton.onClick.AddListener(TurnManager.Instance.RequestEndTurn);
        }
    }
    
    private void OnEnable()
    {
        TurnEvents.OnTurnChanged += UpdateTurnUI;
    }
    
    private void OnDisable()
    {
        TurnEvents.OnTurnChanged -= UpdateTurnUI;
    }

    private void UpdateTurnUI()
    {
        if (TurnManager.Instance.IsMyTurn)
        {
            SetIndicatorText("나의 턴!");
            SetIndicatorColor(Color.green);
            SetButtonInteractable(true);
        }
        else
        {
            SetIndicatorText($"플레이어 {TurnManager.Instance.CurrentTurnActor}의 턴");
            SetIndicatorColor(Color.red);
            SetButtonInteractable(false);
        }
    }
    
    public void SetButtonInteractable(bool interactable)
    {
        turnEndButton.interactable = interactable;
    }

    public void SetIndicatorText(string text)
    {
        turnIndicatorText.text = text;
    }

    public void SetIndicatorColor(Color color)
    {
        turnIndicatorText.color = color;
    }
}
