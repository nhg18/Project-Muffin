using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopupManager : SingletonPersistent<PopupManager>
{
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private GameObject blockingPanel;

    private BasePopup _current;
//Todo: 팝업 스택으로 만들어서 팝업 여러개 띄울 수 있게
    public T Show<T>() where T : BasePopup
    {
        // 기존 팝업 닫기
        if (_current != null)
            Hide();
        
        blockingPanel.SetActive(true);

        var prefab = Get<T>();
        _current = Instantiate(prefab, popupRoot);
        _current.OnShow();
        return (T)_current;
    }

    public void Hide()
    {
        if (_current == null) return;
        _current.OnHide();
        Destroy(_current.gameObject);
        _current = null;
        
        blockingPanel.SetActive(false);
    }
    
    public T Get<T>() where T : BasePopup
    {
        return Resources.Load<T>($"Popups/{typeof(T).Name}");
    }
}
