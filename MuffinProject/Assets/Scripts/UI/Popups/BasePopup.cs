using UI.Interfaces;
using UnityEngine;

public abstract class BasePopup : MonoBehaviour
{
    public virtual void OnShow() { }
    public virtual void OnHide() { }
    
    protected void Close() => PopupManager.Instance.Hide();
    
    protected void CloseAll() => PopupManager.Instance.HideAll();
}
