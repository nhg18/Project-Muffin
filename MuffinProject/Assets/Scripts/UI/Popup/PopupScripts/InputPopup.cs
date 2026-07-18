using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UI.Popup;
using UnityEngine;
using UnityEngine.UI;

public class InputPopup : Popup
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text placeholderText;
    
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text submitButtonText;
    
    [SerializeField] private Button exitButton;

    public void SetPlaceholderText(string text)
    {
        placeholderText.text = text;
    }

    public void SetSubmitButtonText(string text)
    {
        submitButtonText.text = text;
    }

    private void ClickedExitButton()
    {
        
    }
}
