using System.Collections;
using TMPro;
using UnityEngine;

public class ToastPopup : MonoBehaviour

{
    [SerializeField] private TMP_Text messageText;
    
    private float _duration;

    public void Setup(string message, float duration)
    {
        messageText.text = message;
        _duration = duration;
        StartCoroutine(AutoDismiss());
    }

    private IEnumerator AutoDismiss()
    {
        yield return new WaitForSeconds(_duration);
        ToastPopupManager.Instance.Dismiss(this);
    }
}