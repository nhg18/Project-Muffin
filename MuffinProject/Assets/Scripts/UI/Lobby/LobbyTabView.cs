using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 묶음 탭 한 칸 (시안 .seg · .hseg · .ptabs 의 버튼) — 고른 칸은 노랑 채움 + 잉크 테, 나머지는 회색 글자.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LobbyTabView : MonoBehaviour
    {
        [SerializeField] private GameObject pressed;
        [SerializeField] private TMP_Text label;

        private Button _button;

        public event Action<LobbyTabView> OnClicked;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (pressed == null || label == null)
                Debug.LogError($"[{nameof(LobbyTabView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => _button.onClick.AddListener(HandleClicked);

        private void OnDisable() => _button.onClick.RemoveListener(HandleClicked);

        public void SetPressed(bool isPressed)
        {
            pressed.SetActive(isPressed);
            label.color = isPressed ? LobbyStyle.Ink : LobbyStyle.Ink2;
        }

        public void SetLabel(string text) => label.text = text;

        private void HandleClicked() => OnClicked?.Invoke(this);
    }
}
