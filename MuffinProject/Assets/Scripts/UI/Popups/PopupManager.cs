using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopupManager : SingletonPersistent<PopupManager>
{
    [SerializeField] private RectTransform popupRoot;
    
    private readonly Stack<BasePopup> _stack = new();

    public T Show<T>() where T : BasePopup
    {
        var prefab = PopupRegistry.Get<T>();
        var popup = Instantiate(prefab, popupRoot);
        
        popup.OnShow();
        _stack.Push(popup);
        return popup;
    }

    public void Hide()
    {
        if (_stack.Count == 0) return;

        var top = _stack.Pop();
        top.OnHide();
        Destroy(top.gameObject);
    }

    public void HideAll()
    {
        while (_stack.Count > 0)
        {
            var top = _stack.Pop();
            top.OnHide();
            Destroy(top.gameObject);
        }
    }
    
    public bool HasPopup => _stack.Count > 0;
}
