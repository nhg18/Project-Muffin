using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopupManager : SingletonPersistent<PopupManager>
{
    public RectTransform _popupRoot;

    private BasePopup _current;

    protected override void Awake()
    {
        base.Awake();
        _popupRoot = GetComponentInChildren<RectTransform>();
    }

    public T Show<T>() where T : BasePopup
    {
        // 기존 팝업 닫기
        if (_current != null)
            Hide();

        var prefab = Get<T>();
        _current = Instantiate(prefab, _popupRoot);
        _current.OnShow();
        return (T)_current;
    }

    public void Hide()
    {
        if (_current == null) return;
        _current.OnHide();
        Destroy(_current.gameObject);
        _current = null;
    }
    
    public T Get<T>() where T : BasePopup
    {
        return Resources.Load<T>($"Popups/{typeof(T).Name}");
    }
}
