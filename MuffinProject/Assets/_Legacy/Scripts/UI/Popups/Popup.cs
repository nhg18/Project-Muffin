using UnityEngine;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 모든 팝업의 기반. 생성 · 파괴는 PopupManager 가 한다 — 직접 Instantiate / Destroy 하지 않는다.
    /// 프리팹 규약: Resources/Popups/{타입명}.prefab, 루트에 같은 타입의 컴포넌트.
    /// </summary>
    public abstract class Popup : MonoBehaviour
    {
        /// <summary>
        /// 이 팝업을 닫는다.
        /// </summary>
        public void Close() => PopupManager.Instance.Close(this);

        // PopupManager 전용 — 생성 직후 · 파괴 직전에 한 번씩 불린다.
        internal void NotifyOpened() => OnOpen();
        internal void NotifyClosed() => OnClose();

        protected virtual void OnOpen() { }

        protected virtual void OnClose() { }
    }
}
