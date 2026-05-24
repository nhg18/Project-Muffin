using System.Collections.Generic;
using UnityEngine;

public class ToastPopupManager : SingletonPersistent<ToastPopupManager>
{
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private ToastPopup toastPrefab;
    
    private const int MAX_TOASTS = 3;
    private readonly LinkedList<ToastPopup> _actives = new();

    public void Show(string message, float duration = 5f)
    {
        // 최대 초과시 가장 오래된 것 제거
        if (_actives.Count >= MAX_TOASTS)
            Dismiss(_actives.First.Value);

        Debug.Log(message);
        Debug.Log($"prefab: {toastPrefab}, root: {popupRoot}");
        var toast = Instantiate(toastPrefab, popupRoot);
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