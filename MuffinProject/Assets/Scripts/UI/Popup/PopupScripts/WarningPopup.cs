using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UI.Popup;
using UnityEngine;
using UnityEngine.UI;

public class WarningPopup : Popup
{
    [SerializeField] private TMP_Text mainText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private Button okButton;

    public string MainText
    {
        set => mainText.text = value;
    }

    public string SubText
    {
        set => subText.text = value;
    }
    
    public Action OnClickedOkButton;

    private void OnEnable()
    {
        okButton.onClick.AddListener(ClickedOkButton);
    }

    private void OnDisable()
    {
        okButton.onClick.RemoveListener(ClickedOkButton);
    }

    private void ClickedOkButton()
    {
        // PopupManager.Instance.CloseModal(this);
        OnClickedOkButton?.Invoke();
    }
}
