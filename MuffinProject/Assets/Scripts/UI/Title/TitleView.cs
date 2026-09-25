using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 타이틀 화면 루트 뷰. 닉네임을 검증하고, 통과하면 ConnectRequested 로 알린다.
    /// 네트워크 · 씬 전환은 모른다. 기준 문서: docs/systems/12-title-ui.md
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        private const string ConnectText = "접속";
        private const string ConnectingText = "접속 중…";

        [SerializeField] private NicknameFieldView nicknameField;
        [SerializeField] private Button connectButton;
        [SerializeField] private TMP_Text connectLabel;
        [SerializeField] private ErrorLabel errorLabel;

        private bool _connecting;

        /// <summary>검증을 통과한 닉네임(앞뒤 공백 제거)</summary>
        public event Action<string> ConnectRequested;

        private void OnEnable()
        {
            connectButton.onClick.AddListener(HandleConnectClicked);
            nicknameField.ValueChanged += HandleNicknameChanged;
        }

        private void OnDisable()
        {
            connectButton.onClick.RemoveListener(HandleConnectClicked);
            nicknameField.ValueChanged -= HandleNicknameChanged;
        }

        /// <summary>
        /// 외부 실패 사유(연결 실패 등)를 표시하고 버튼 글자를 "접속"으로 되돌린다.
        /// </summary>
        public void ShowError(string message)
        {
            SetConnecting(false);
            errorLabel.Show(message);
        }

        /// <summary>
        /// 저장된 닉네임 복원용. 카운터도 갱신된다.
        /// </summary>
        public void SetNickname(string nickname) => nicknameField.SetText(nickname);

        /// <summary>
        /// 버튼 글자 "접속" ↔ "접속 중…". 버튼을 비활성화하지는 않고, 접속 중의 탭만 무시한다.
        /// </summary>
        public void SetConnecting(bool connecting)
        {
            _connecting = connecting;
            connectLabel.text = connecting ? ConnectingText : ConnectText;
        }

        private void HandleNicknameChanged(string _) => errorLabel.Clear();

        private void HandleConnectClicked()
        {
            // 12-title-ui 5절: 접속 중에는 중복 요청을 막는다.
            if (_connecting) return;

            string nickname = nicknameField.Text.Trim();
            NicknameValidationResult result = NicknameValidator.Validate(nickname);
            if (result != NicknameValidationResult.Valid)
            {
                ShowError(NicknameValidator.GetErrorMessage(result));
                return;
            }

            errorLabel.Clear();
            SetConnecting(true);
            ConnectRequested?.Invoke(nickname);
        }
    }
}
