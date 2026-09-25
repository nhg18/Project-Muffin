using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 한 줄 입력. 제출은 OnSubmitted 로 알리고 열어 둔다 — 결과(예: 방 참가 성공 · 실패)를 아는 쪽이 닫는다.
    /// 닫기 버튼은 스스로 닫는다.
    /// </summary>
    public class InputPopup : Popup
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text submitButtonText;
        [SerializeField] private Button exitButton;

        public int CharacterLimit
        {
            set => inputField.characterLimit = value;
        }

        public string PlaceholderText
        {
            set => placeholderText.text = value;
        }

        public string SubmitButtonText
        {
            set => submitButtonText.text = value;
        }

        /// <summary>제출 버튼을 누름. 인자는 입력값</summary>
        public event Action<string> OnSubmitted;

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
