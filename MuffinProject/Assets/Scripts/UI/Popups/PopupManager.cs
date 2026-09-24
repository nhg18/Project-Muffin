using System.Collections.Generic;
using Chapchu.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 팝업 3계층(모달 · 일반 · 토스트)을 열고 닫는다. 씬이 바뀌면 전부 닫는다.
    /// 프리팹 규약: Resources/Popups/{타입명}.prefab, 루트에 같은 타입의 컴포넌트.
    /// </summary>
    public class PopupManager : SingletonPersistent<PopupManager>
    {
        private const string ResourcePath = "Popups/";

        [SerializeField] private RectTransform modalLayer;
        [SerializeField] private RectTransform nonModalLayer;
        [SerializeField] private RectTransform toastLayer;

        // 모달은 나중에 연 것이 위. 하나라도 열려 있으면 modalLayer 의 Image 가 아래 UI 입력을 막는다.
        private readonly List<Popup> _modals = new();
        private readonly List<Popup> _nonModals = new();
        private readonly List<Popup> _toasts = new();
        private Image _modalBlocker;

        protected override void Awake()
        {
            base.Awake();
            _modalBlocker = modalLayer.GetComponent<Image>();
            RefreshBlocker();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// 아래 UI 입력을 막는 팝업을 연다. 프리팹이 없으면 null.
        /// </summary>
        public T OpenModal<T>() where T : Popup => Open<T>(modalLayer, _modals);

        /// <summary>
        /// 아래 UI 입력을 막지 않는 팝업을 연다. 프리팹이 없으면 null.
        /// </summary>
        public T OpenNonModal<T>() where T : Popup => Open<T>(nonModalLayer, _nonModals);

        /// <summary>
        /// 토스트를 연다. 정해진 시간 뒤 스스로 닫힌다. 프리팹이 없으면 null.
        /// </summary>
        public T OpenToast<T>() where T : ToastPopup => Open<T>(toastLayer, _toasts);

        /// <summary>
        /// 문구 한 줄짜리 토스트를 띄운다.
        /// </summary>
        public void ShowToast(string message)
        {
            MessageToast toast = OpenToast<MessageToast>();
            if (toast != null)
                toast.Message = message;
        }

        /// <summary>
        /// 어느 계층에 있든 해당 팝업을 닫는다.
        /// </summary>
        public void Close(Popup popup)
        {
            if (popup == null) return;

            if (!_modals.Remove(popup) && !_nonModals.Remove(popup) && !_toasts.Remove(popup))
            {
                Debug.LogWarning($"[PopupManager] 열려 있지 않은 팝업: {popup.name}");
                return;
            }

            Release(popup);
            RefreshBlocker();
        }

        /// <summary>
        /// 열려 있는 팝업을 전부 닫는다.
        /// </summary>
        public void CloseAll()
        {
            ReleaseAll(_modals);
            ReleaseAll(_nonModals);
            ReleaseAll(_toasts);
            RefreshBlocker();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => CloseAll();

        private T Open<T>(RectTransform layer, List<Popup> opened) where T : Popup
        {
            T prefab = Load<T>();
            if (prefab == null) return null;

            T popup = Instantiate(prefab, layer);
            opened.Add(popup);
            RefreshBlocker();
            popup.NotifyOpened();
            return popup;
        }

        private static T Load<T>() where T : Popup
        {
            string path = ResourcePath + typeof(T).Name;
            T prefab = Resources.Load<T>(path);
            // 규약이 어긋나면 Resources.Load 는 조용히 null 을 준다. 원인이 보이게 남긴다.
            if (prefab == null)
                Debug.LogError($"[PopupManager] Resources/{path}.prefab 이 없거나 루트에 {typeof(T).Name} 컴포넌트가 없다");
            return prefab;
        }

        private static void ReleaseAll(List<Popup> popups)
        {
            foreach (Popup popup in popups)
                Release(popup);
            popups.Clear();
        }

        // 컴포넌트가 아니라 GameObject 째 지운다. 컴포넌트만 지우면 누를 수도 닫을 수도 없는 껍데기가 화면에 남는다.
        private static void Release(Popup popup)
        {
            popup.NotifyClosed();
            Destroy(popup.gameObject);
        }

        private void RefreshBlocker()
        {
            _modalBlocker.enabled = _modals.Count > 0;
        }

#if UNITY_EDITOR
        // 동작 확인용: Play 중 Hierarchy 의 PopupManager 선택 → 인스펙터 컴포넌트 ⋮ 메뉴
        [ContextMenu("Debug/테스트 토스트")]
        private void DebugShowToast() => ShowToast("테스트 토스트입니다.");

        [ContextMenu("Debug/경고 모달을 띄운 채 현재 씬 다시 로드")]
        private void DebugOpenModalThenReload()
        {
            WarningPopup popup = OpenModal<WarningPopup>();
            popup.MainText = "테스트";
            popup.SubText = "씬을 다시 불러옵니다.";
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
#endif
    }
}
