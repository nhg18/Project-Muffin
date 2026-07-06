using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameInputView : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text errorText;
    
    public event Action ButtonClicked;
    public event Action TextChanged;

    private void Awake()
    {
        inputField.onValueChanged.AddListener(_ => TextChanged?.Invoke());
        submitButton.onClick.AddListener(() => ButtonClicked?.Invoke());
    }
    
    public string Nickname => inputField.text;
    
    public string ErrorMessage => errorText.text;
    
    public void SetCountText(string text)
    {
        countText.text = text;
    }

    public void SetErrorText(string text)
    {
        errorText.text = text;
    }
}
