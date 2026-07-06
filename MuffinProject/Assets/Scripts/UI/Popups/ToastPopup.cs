using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastPopup : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text messageText;
    
    private Coroutine _autoDismissCoroutine;

    private void OnEnable()
    {
        closeButton.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        closeButton.onClick.RemoveListener(OnClick);
    }

    public void Setup(string message, float duration)
    {
        messageText.text = message;
        _autoDismissCoroutine = StartCoroutine(AutoDismiss(duration));
    }

    private IEnumerator AutoDismiss(float duration)
    {
        yield return new WaitForSeconds(duration);
        ToastPopupManager.Instance.Dismiss(this);
    }


    private void OnClick()
    {
        if (_autoDismissCoroutine != null)
            StopCoroutine(_autoDismissCoroutine);
        
        ToastPopupManager.Instance.Dismiss(this);
    }
}