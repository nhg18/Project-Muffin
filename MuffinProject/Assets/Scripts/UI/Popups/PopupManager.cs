using System.Collections.Generic;
using Chapchu.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 팝업 3계층(모달 · 일반 · 토스트)을 열고 닫는다. 씬이 바뀌면 전부 닫는다.
    /// 프리팹 규약: Resources/Popups/{타입명}.prefab, 루트에 같은 타입의 컴포넌트. 기준 문서: docs/systems/10-ui.md 8절
    /// </summary>
    public class PopupManager : SingletonPersistent<PopupManager>
    {
        // 토스트 아래 여백 (22-popup-ui.md 5-3). 기본은 MessageToast 프리팹 자리 48.
        private const float DefaultToastBottom = 48f;
        private const float GameToastBottom = 212f;

        [SerializeField] private RectTransform modalLayer;
        [SerializeField] private RectTransform nonModalLayer;
        [SerializeField] private RectTransform toastLayer;

        // 모달은 나중에 연 것이 위. 하나라도 열려 있으면 modalLayer 의 Image 가 아래 UI 입력을 막는다.
        private readonly List<Popup> _modals = new();
        private readonly List<Popup> _nonModals = new();
        private readonly List<Popup> _toasts = new();
        private Image _modalBlocker;
        private (string Title, string Text)? _warningOnNextScene;

        protected override void Awake()
        {
            base.Awake();
            if (modalLayer == null)
                Debug.LogError($"[{nameof(PopupManager)}] {nameof(modalLayer)} 참조가 비어 있습니다.", this);

            if (nonModalLayer == null)
                Debug.LogError($"[{nameof(PopupManager)}] {nameof(nonModalLayer)} 참조가 비어 있습니다.", this);

            if (toastLayer == null)
                Debug.LogError($"[{nameof(PopupManager)}] {nameof(toastLayer)} 참조가 비어 있습니다.", this);

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
            if (popup == null)
                return;

            if (!_modals.Remove(popup) && !_nonModals.Remove(popup) && !_toasts.Remove(popup))
            {
                Debug.LogWarning($"[{nameof(PopupManager)}] 열려 있지 않은 팝업: {popup.name}");
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

        /// <summary>
        /// 다음 씬이 열린 뒤 경고 팝업을 띄운다. 씬이 바뀌면 열린 팝업이 모두 닫히므로, 옮겨 간 화면에서 알릴 것은 여기에 맡긴다 (22-popup-ui.md 3절 「… 뒤 로비 도착」).
        /// </summary>
        public void ShowWarningOnNextScene(string title, string text) => _warningOnNextScene = (title, text);

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CloseAll();
            // 22-popup-ui.md 5-3: 토스트는 아래 48, 게임 화면만 손패 위 아래 212. 프리팹이 이미 48 위에 있으니 차이만큼 레이어 바닥을 올린다.
            float toastBottom = scene.name == ScenePaths.Game ? GameToastBottom : DefaultToastBottom;
            toastLayer.offsetMin = new Vector2(toastLayer.offsetMin.x, toastBottom - DefaultToastBottom);
            if (_warningOnNextScene == null)
                return;

            (string title, string text) = _warningOnNextScene.Value;
            _warningOnNextScene = null;
            WarningPopup popup = OpenModal<WarningPopup>();
            if (popup == null)
                return;
            popup.MainText = title;
            popup.SubText = text;
        }

        private T Open<T>(RectTransform layer, List<Popup> opened) where T : Popup
        {
            T prefab = Load<T>();
            if (prefab == null)
                return null;

            T popup = Instantiate(prefab, layer);
            opened.Add(popup);
            RefreshBlocker();
            popup.NotifyOpened();
            return popup;
        }

        private static T Load<T>() where T : Popup
        {
            string path = ResourcePaths.Popups + typeof(T).Name;
            T prefab = Resources.Load<T>(path);
            // 규약이 어긋나면 Resources.Load 는 조용히 null 을 준다. 원인이 보이게 남긴다.
            if (prefab == null)
                Debug.LogError($"[{nameof(PopupManager)}] Resources/{path}.prefab 이 없거나 루트에 {typeof(T).Name} 컴포넌트가 없다");
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
        private void DebugOpenModalThenReload() => StartCoroutine(DebugOpenModalThenReloadRoutine());

        private System.Collections.IEnumerator DebugOpenModalThenReloadRoutine()
        {
            WarningPopup popup = OpenModal<WarningPopup>();
            if (popup == null)
                yield break;

            popup.MainText = "테스트";
            popup.SubText = "2초 뒤 씬을 다시 불러옵니다.";

            // 같은 프레임에 로드하면 그려지기도 전에 닫혀 눈으로 확인할 수 없다.
            yield return new WaitForSecondsRealtime(2f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;

            int leftover = modalLayer.childCount + nonModalLayer.childCount + toastLayer.childCount;
            Debug.Log($"[{nameof(PopupManager)}] 씬 재로드 후 남은 팝업 오브젝트 {leftover}개 (0이면 정상), 차단막 {(_modalBlocker.enabled ? "켜짐(비정상)" : "꺼짐")}");
        }
#endif
    }
}
