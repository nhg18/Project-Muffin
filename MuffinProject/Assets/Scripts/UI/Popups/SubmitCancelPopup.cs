using System.Collections;
using System.Collections.Generic;
using UI.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public abstract class SubmitCancelPopup : BasePopup
{
    [SerializeField] protected Button submitButton;
    [SerializeField] protected Button cancelButton;
    
    protected virtual void OnEnable()
    {
        submitButton.onClick.AddListener(OnSubmit);
        cancelButton.onClick.AddListener(OnCancel);
    }

    protected virtual void OnDisable()
    {
        submitButton.onClick.RemoveListener(OnSubmit);
        cancelButton.onClick.RemoveListener(OnCancel);
    }

    protected virtual void OnDestroy()
    {
        submitButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
    }

    protected abstract void OnSubmit();

    protected abstract void OnCancel();
}
