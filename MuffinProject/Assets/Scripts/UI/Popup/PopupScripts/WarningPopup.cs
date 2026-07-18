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
    
    public Action OnOkButtonClick;

    private void OnEnable()
    {
        okButton.onClick.AddListener(ClickedOkButton);
    }

    private void OnDisable()
    {
        okButton.onClick.RemoveListener(ClickedOkButton);
    }

    public void SetMainText(string text)
    {
        mainText.text = text;
    }

    public void SetSubText(string text)
    {
        subText.text = text;
    }

    private void ClickedOkButton()
    {
        // PopupManager.Instance.CloseModal(this);
        OnOkButtonClick?.Invoke();
    }
}
