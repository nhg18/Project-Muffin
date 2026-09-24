using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 잠깐 떴다 사라지는 알림. duration 뒤 스스로 닫히고, 누르면 바로 닫힌다.
    /// </summary>
    public abstract class ToastPopup : Popup, IPointerClickHandler
    {
        [SerializeField] private float duration = 3f;
        [SerializeField] private bool dismissOnClick = true;

        private Coroutine _autoClose;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dismissOnClick)
                Close();
        }

        protected override void OnOpen()
        {
            _autoClose = StartCoroutine(AutoCloseRoutine());
        }

        protected override void OnClose()
        {
            if (_autoClose == null) return;
            StopCoroutine(_autoClose);
            _autoClose = null;
        }

        private IEnumerator AutoCloseRoutine()
        {
            // 일시정지(timeScale 0) 중에도 사라지도록 실제 시간으로 잰다.
            yield return new WaitForSecondsRealtime(duration);
            _autoClose = null;
            Close();
        }
    }
}
