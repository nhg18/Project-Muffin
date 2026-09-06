using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Popup
{
    public abstract class ToastPopup : Popup, IPointerClickHandler
    {
        [SerializeField] private float duration = 3f;
        [SerializeField] private bool dismissOnClick = true;
        
        private Coroutine _autoCloseCoroutine;

        protected override void OnOpen()
        {
            _autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }

        protected override void OnClose()
        {
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dismissOnClick)
                PopupManager.Instance.CloseToast(this);
        }
        
        private IEnumerator AutoCloseRoutine()
        {
            yield return new WaitForSeconds(duration);
            PopupManager.Instance.CloseToast(this);
        }
    }
}
