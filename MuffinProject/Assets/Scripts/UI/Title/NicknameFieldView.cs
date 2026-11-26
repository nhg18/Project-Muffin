using System;
using Chapchu.UI.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 닉네임 입력 한 줄 — 폭 16 제한 · "n / 16" 카운터(n = 폭) · 에러 칩 · 빨간 테 · 포커스 노란 테두리. 검증은 TitlePresenter 가 한다.
    /// 기준: 「통합 화면 UI v2」 타이틀 .nick · .errslot
    /// </summary>
    public class NicknameFieldView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Image border;
        [SerializeField] private GameObject focusRing;
        [SerializeField] private GameObject errorChip;
        [SerializeField] private TMP_Text errorText;

        /// <summary>지금 입력값 (앞뒤 공백 포함 — 검증 쪽이 자른다)</summary>
        public string Text => inputField.text;

        /// <summary>입력창에서 Enter — 「시작」 과 같다</summary>
        public event Action OnSubmitted;

        private void Awake()
        {
            if (inputField == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(inputField)} 참조가 비어 있습니다.", this);

            if (countText == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(countText)} 참조가 비어 있습니다.", this);

            if (border == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(border)} 참조가 비어 있습니다.", this);

            if (focusRing == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(focusRing)} 참조가 비어 있습니다.", this);

            if (errorChip == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(errorChip)} 참조가 비어 있습니다.", this);

            if (errorText == null)
                Debug.LogError($"[{nameof(NicknameFieldView)}] {nameof(errorText)} 참조가 비어 있습니다.", this);

            inputField.characterLimit = NicknameValidator.MaxLength;
            UpdateCount(inputField.text);
            focusRing.SetActive(false);
        }

        private void OnEnable()
        {
            inputField.onValueChanged.AddListener(HandleValueChanged);
            inputField.onSelect.AddListener(HandleSelected);
            inputField.onDeselect.AddListener(HandleDeselected);
            inputField.onSubmit.AddListener(HandleSubmitted);
        }

        private void OnDisable()
        {
            inputField.onValueChanged.RemoveListener(HandleValueChanged);
            inputField.onSelect.RemoveListener(HandleSelected);
            inputField.onDeselect.RemoveListener(HandleDeselected);
            inputField.onSubmit.RemoveListener(HandleSubmitted);
            focusRing.SetActive(false);
        }

        /// <summary>값만 바꾸고 에러를 지운다.</summary>
        public void SetText(string text)
        {
            text = NicknameValidator.TrimToWidth(text);

            inputField.SetTextWithoutNotify(text);
            UpdateCount(text);
            ClearError();
        }

        /// <summary>입력창 아래 에러 칩 + 빨간 테 + 흔들기 (12-title-ui 3절 #2 · 시안 shakeNick)</summary>
        public void ShowError(string message)
        {
            errorText.text = message;
            errorChip.SetActive(true);
            border.color = LobbyStyle.Hp;
            UIJuice.Shake((RectTransform)border.transform.parent, 10f, 0.3f);
        }

        public void ClearError()
        {
            errorChip.SetActive(false);
            border.color = LobbyStyle.Ink;
        }

        // 입력이 바뀌면 에러가 바로 사라진다 (12-title-ui 3절 #3)
        // 폭 16 을 넘는 끝은 잘라 입력 자체를 막는다 (12-title-ui 4-2) — characterLimit 는 글자 수라 한글을 못 막는다
        private void HandleValueChanged(string text)
        {
            string trimmed = NicknameValidator.TrimToWidth(text);
            if (trimmed != text)
            {
                text = trimmed;
                inputField.SetTextWithoutNotify(text);
            }

            UpdateCount(text);
            ClearError();
        }

        private void HandleSelected(string _) => focusRing.SetActive(true);

        private void HandleDeselected(string _) => focusRing.SetActive(false);

        private void HandleSubmitted(string _) => OnSubmitted?.Invoke();

        // IME 조합 중인 글자는 text 에 들어오지 않으므로 조합이 끝난 글자 기준으로 센다. n = 폭 (20-title-ui-full 7-6)
        private void UpdateCount(string text) => countText.text = $"{NicknameValidator.GetWidth(text)} / {NicknameValidator.MaxLength}";
    }
}
