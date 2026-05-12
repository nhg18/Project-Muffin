using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopupManager : SingletonPersistent<PopupManager>
{
    private Transform popupRoot;

    private BasePopup current;

    protected override void Awake()
    {
        base.Awake();
        popupRoot = GetComponentInChildren<Transform>();
    }

    public T Show<T>() where T : BasePopup
    {
        // 기존 팝업 닫기
        if (current != null)
            Hide();

        var prefab = Get<T>();
        current = Instantiate(prefab, popupRoot);
        current.OnShow();
        return (T)current;
    }

    public void Hide()
    {
        if (current == null) return;
        current.OnHide();
        Destroy(current.gameObject);
        current = null;
    }
    
    public T Get<T>() where T : BasePopup
    {
        return Resources.Load<T>($"Popups/{typeof(T).Name}");
    }
}
