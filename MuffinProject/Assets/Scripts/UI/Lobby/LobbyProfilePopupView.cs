using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 프로필 창 — 닉네임(변경) · 친구 코드(복사) · 전적 넷 · 로그인 수단 한 줄.
    /// 기준: 「통합 화면 UI v2」 로비 profileBody, docs/systems/17-lobby-ui-full.md 5-2
    /// </summary>
    public class LobbyProfilePopupView : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private Button changeButton;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button copyButton;
        [SerializeField] private TMP_Text winsText;
        [SerializeField] private TMP_Text lossesText;
        [SerializeField] private TMP_Text killWinsText;
        [SerializeField] private TMP_Text chapchuWinsText;
        [SerializeField] private TMP_Text loginText;

        public bool IsOpen => content.activeSelf;

        public event Action OnCloseRequested;
        public event Action OnChangeRequested;
        public event Action OnCopyRequested;

        private void Awake()
        {
            if (content == null || closeButton == null || nicknameText == null || changeButton == null || codeText == null || copyButton == null
                || winsText == null || lossesText == null || killWinsText == null || chapchuWinsText == null || loginText == null)
                Debug.LogError($"[{nameof(LobbyProfilePopupView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            changeButton.onClick.AddListener(HandleChangeClicked);
            copyButton.onClick.AddListener(HandleCopyClicked);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            changeButton.onClick.RemoveListener(HandleChangeClicked);
            copyButton.onClick.RemoveListener(HandleCopyClicked);
        }

        public void Show(string nickname, LobbyHubData data, string loginNote)
        {
            content.SetActive(true);
            nicknameText.text = nickname;
            codeText.text = data.MyCode;
            winsText.text = data.Wins.ToString();
            lossesText.text = data.Losses.ToString();
            killWinsText.text = data.KillWins.ToString();
            chapchuWinsText.text = data.ChapchuWins.ToString();
            loginText.text = loginNote;
        }

        public void SetNickname(string nickname) => nicknameText.text = nickname;

        public void Hide() => content.SetActive(false);

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();

        private void HandleChangeClicked() => OnChangeRequested?.Invoke();

        private void HandleCopyClicked() => OnCopyRequested?.Invoke();
    }
}
