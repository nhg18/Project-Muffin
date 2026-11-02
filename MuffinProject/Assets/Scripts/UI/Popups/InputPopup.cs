using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 한 줄 입력. 제출은 OnSubmitted 로 알리고 열어 둔다 — 결과(예: 방 참가 성공 · 실패)를 아는 쪽이 닫는다.
    /// 닫기 버튼은 스스로 닫는다. 기준 문서: docs/systems/10-ui.md 8절
    /// </summary>
    public class InputPopup : Popup
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text submitButtonText;
        [SerializeField] private Button exitButton;

        /// <summary>입력할 수 있는 최대 글자 수</summary>
        public int CharacterLimit
        {
            set => inputField.characterLimit = value;
        }

        /// <summary>입력창이 비었을 때 보이는 안내 문구</summary>
        public string PlaceholderText
        {
            set => placeholderText.text = value;
        }

        /// <summary>제출 버튼 글자</summary>
        public string SubmitButtonText
        {
            set => submitButtonText.text = value;
        }

        /// <summary>제출 버튼을 누름. 인자는 입력값</summary>
        public event Action<string> OnSubmitted;

        private void Awake()
        {
            if (inputField == null)
                Debug.LogError($"[{nameof(InputPopup)}] {nameof(inputField)} 참조가 비어 있습니다.", this);

            if (placeholderText == null)
                Debug.LogError($"[{nameof(InputPopup)}] {nameof(placeholderText)} 참조가 비어 있습니다.", this);

            if (submitButton == null)
                Debug.LogError($"[{nameof(InputPopup)}] {nameof(submitButton)} 참조가 비어 있습니다.", this);

            if (submitButtonText == null)
                Debug.LogError($"[{nameof(InputPopup)}] {nameof(submitButtonText)} 참조가 비어 있습니다.", this);

            if (exitButton == null)
                Debug.LogError($"[{nameof(InputPopup)}] {nameof(exitButton)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            submitButton.onClick.AddListener(HandleSubmitClicked);
            exitButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            submitButton.onClick.RemoveListener(HandleSubmitClicked);
            exitButton.onClick.RemoveListener(Close);
        }

        private void HandleSubmitClicked() => OnSubmitted?.Invoke(inputField.text);
    }
}
