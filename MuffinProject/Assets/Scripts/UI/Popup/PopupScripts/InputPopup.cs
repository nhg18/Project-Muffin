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
    
    public string InputText => inputField.text;
    
    public int CharacterLimit
    {
        set => inputField.characterLimit = value;
    }
    
    public string PlaceholderText
    {
        set => placeholderText.text = value;
    }

    public string SubmitButtonText
    {
        set => submitButtonText.text = value;
    }

    public Action OnClickedSubmitButton;
    public Action OnClickedExitButton;

    private void OnEnable()
    {
        submitButton.onClick.AddListener(ClickedSubmitButton);
        exitButton.onClick.AddListener(ClickedExitButton);
    }

    private void OnDisable()
    {
        submitButton.onClick.RemoveListener(ClickedSubmitButton);
        exitButton.onClick.RemoveListener(ClickedExitButton);
    }
    
    private void ClickedSubmitButton()
    {
        OnClickedSubmitButton?.Invoke();
    }
    
    private void ClickedExitButton()
    {
        OnClickedExitButton?.Invoke();
    }
}
