using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.Popup
{
    public class PopupManager : SingletonPersistent<PopupManager>
    {
        [SerializeField] private RectTransform modalLayer;
        [SerializeField] private RectTransform nonModalLayer;
        [SerializeField] private RectTransform toastLayer;

        private Image _modalBlocker;
        
        private readonly Stack<Popup> _modalStack = new();
        private readonly List<Popup> _nonModalList = new();
        private readonly List<ToastPopup> _toastList = new();

        protected override void Awake()
        {
            base.Awake();
            _modalBlocker = modalLayer.GetComponent<Image>();
            _modalBlocker.enabled = false;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += CloseAllPopups;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= CloseAllPopups;
        }

        public T OpenModal<T>(T prefab) where T : Popup
        {
            var popup = Instantiate(prefab, modalLayer);
            popup.transform.SetAsLastSibling();
            _modalStack.Push(popup);
            popup.Open();
            RefreshBlocker();
            
            return popup;
        }

        public T OpenNonModal<T>(T prefab) where T : Popup
        {
            var popup = Instantiate(prefab, nonModalLayer);
            _nonModalList.Add(popup);
            popup.Open();
            return popup;
        }

        public T OpenToast<T>(T prefab) where T : ToastPopup
        {
            var popup = Instantiate(prefab, toastLayer);
            _toastList.Add(popup);
            popup.Open();
            return popup;
        }

        public void CloseModal()
        {
            if (_modalStack.Count == 0)
            {
                Debug.LogWarning("Modal count 0");
                return;
            }
            _modalStack.Pop().Close();
            RefreshBlocker();
        }

        public void CloseModal(Popup popup)
        {
            if (!_modalStack.Contains(popup))
            {
                Debug.LogWarning("Not contain modal");
                return;
            }
            
            var remaining = _modalStack.Where(p => p != popup).Reverse().ToArray();
            _modalStack.Clear();
            
            foreach (var p in remaining) 
                _modalStack.Push(p);

            popup.Close();
            RefreshBlocker();
        }

        public void CloseNonModal(Popup popup)
        {
            if (!_nonModalList.Remove(popup))
            {
                Debug.LogWarning("Not contain non-modal");
                return;
            }
            popup.Close();
        }

        public void CloseToast(ToastPopup popup)
        {
            if (!_toastList.Remove(popup))
            {
                Debug.LogWarning("Not contain toast");
                return;
            }
            
            popup.Close();
        }

        private void RefreshBlocker()
        {
            _modalBlocker.enabled = _modalStack.Count > 0;
        }

        private void CloseAllPopups(Scene scene, LoadSceneMode mode)
        {
            CloseAllModals();
            CloseAllNonModals();
            CloseAllToasts();
        }

        private void CloseAllModals()
        {
            while (_modalStack.Count > 0)
                _modalStack.Pop().Close();
            RefreshBlocker();
        }

        private void CloseAllNonModals()
        {
            foreach (var popup in _nonModalList)
                popup.Close();
            _nonModalList.Clear();
        }

        private void CloseAllToasts()
        {
            foreach (var popup in _toastList)
                popup.Close();
            _toastList.Clear();
        }
    }
}
