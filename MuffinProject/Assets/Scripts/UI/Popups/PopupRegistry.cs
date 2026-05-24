using UnityEngine;

public class PopupRegistry
{
    public static T Get<T>() where T : BasePopup
    {
        return Resources.Load<T>($"Popups/{typeof(T).Name}");
    }
}