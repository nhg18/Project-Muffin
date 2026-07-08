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
            turnEndButton.onClick.AddListener(TurnEvents.RaiseEndTurnRequested);
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
