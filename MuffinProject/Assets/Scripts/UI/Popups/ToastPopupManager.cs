using System.Collections.Generic;
using UnityEngine;

public class ToastPopupManager : SingletonPersistent<ToastPopupManager>
{
    [SerializeField] private Transform toastContainer;
    [SerializeField] private ToastPopup toastPrefab;
    
    private const int MaxToasts = 3;
    private readonly LinkedList<ToastPopup> _actives = new();

    public void Show(string message, float duration = 2f)
    {
        // 최대 초과시 가장 오래된 것 제거
        if (_actives.Count >= MaxToasts)
            Dismiss(_actives.First.Value);

        var toast = Instantiate(toastPrefab, toastContainer);
        toast.Setup(message, duration);
        _actives.AddLast(toast);
    }

    public void Dismiss(ToastPopup toast)
    {
        if (!_actives.Contains(toast)) return;
        _actives.Remove(toast);
        Destroy(toast.gameObject);
    }
}