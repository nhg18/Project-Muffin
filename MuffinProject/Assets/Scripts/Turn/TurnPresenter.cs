using System;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnPresenter : MonoBehaviour
{
    [SerializeField] private TurnView turnView;

    private void OnEnable()
    {
        TurnEvents.OnTurnChanged += UpdateTurnUI;
        turnView.OnEndTurnRequested += HandleEndTurnRequest;
    }
    
    private void OnDisable()
    {
        TurnEvents.OnTurnChanged -= UpdateTurnUI;
        turnView.OnEndTurnRequested -= HandleEndTurnRequest;
    }

    private void UpdateTurnUI()
    {
        if (TurnManager.Instance.IsMyTurn)
        {
            turnView.SetIndicatorText("나의 턴!");
            turnView.SetIndicatorColor(Color.green);
            turnView.SetButtonInteractable(true);
        }
        else
        {
            turnView.SetIndicatorText($"플레이어 {TurnManager.Instance.CurrentTurnActor}의 턴");
            turnView.SetIndicatorColor(Color.red);
            turnView.SetButtonInteractable(false);
        }
    }

    private void HandleEndTurnRequest()
    {
        TurnManager.Instance.RequestEndTurn();
    }
}

