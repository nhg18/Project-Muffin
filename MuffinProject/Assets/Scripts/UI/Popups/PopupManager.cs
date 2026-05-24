using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopupManager : SingletonPersistent<PopupManager>
{
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private GameObject blockingPanel;
    
    private readonly Stack<BasePopup> _stack = new();

//Todo: 팝업 스택으로 만들어서 팝업 여러개 띄울 수 있게
    public T Show<T>() where T : BasePopup
    {
        var prefab = Get<T>();
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
    
    public T Get<T>() where T : BasePopup
    {
        return Resources.Load<T>($"Popups/{typeof(T).Name}");
    }
}
