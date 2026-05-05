using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _submitButton;
    // [SerializeField] private TMP_Text _feedbackText;
    
    public event Action OnSubmitClicked;
    
    public string InputText => _nicknameInput.text;
    
    private void Awake()
    {
        _submitButton.onClick.AddListener(() => OnSubmitClicked?.Invoke());
    }

    public void SetFeedback(string message, bool isError = false)
    {
        // _feedbackText.text = message;
        // _feedbackText.color = isError ? Color.red : Color.green;
        Debug.Log(message);
    }

    public void SetSubmitInteractable(bool interactable)
    {
        _submitButton.interactable = interactable;
    }

    public void SetInputText(string text)
    {
        _nicknameInput.text = text;
    }

    private void OnDestroy()
    {
        _submitButton.onClick.RemoveAllListeners();
    }
}
