using System;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 닉네임 입력창. 16자 제한과 "n / 16" 카운터만 담당한다. 검증은 TitleView 가 한다.
    /// </summary>
    public class NicknameFieldView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text countText;

        public event Action<string> OnValueChanged;

        public string Text => inputField.text;

        private void Awake()
        {
            if (inputField == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(inputField)} 참조가 비어 있습니다.", this);

            if (countText == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(countText)} 참조가 비어 있습니다.", this);

            inputField.characterLimit = NicknameValidator.MaxLength;
            UpdateCount(inputField.text);
        }

        private void OnEnable()
        {
            inputField.onValueChanged.AddListener(HandleValueChanged);
        }

        private void OnDisable()
        {
            inputField.onValueChanged.RemoveListener(HandleValueChanged);
        }

        /// <summary>
        /// 값만 바꾸고 OnValueChanged 는 발행하지 않는다.
        /// </summary>
        public void SetText(string text)
        {
            text ??= string.Empty;
            if (text.Length > NicknameValidator.MaxLength)
                text = text.Substring(0, NicknameValidator.MaxLength);

            inputField.SetTextWithoutNotify(text);
            UpdateCount(text);
        }

        private void HandleValueChanged(string text)
        {
            UpdateCount(text);
            OnValueChanged?.Invoke(text);
        }

        // IME 조합 중인 글자는 text 에 들어오지 않으므로 조합이 끝난 글자 기준으로 센다.
        private void UpdateCount(string text) => countText.text = $"{text.Length} / {NicknameValidator.MaxLength}";
    }
}
