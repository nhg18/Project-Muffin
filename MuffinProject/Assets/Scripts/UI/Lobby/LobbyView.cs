using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 화면 루트 뷰. 버튼 탭을 이벤트로만 알린다.
    /// 네트워크 · 팝업 · 씬 전환은 모른다 (TitleView 와 같은 구조). 기준 문서: docs/systems/14-lobby-ui.md
    /// </summary>
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private Button randomMatchButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;

        public event Action OnRandomMatchRequested;
        public event Action OnCreateRoomRequested;
        public event Action OnJoinRoomRequested;

        private void Awake()
        {
            if (nicknameText == null)
                Debug.LogError($"[{nameof(LobbyView)}] {nameof(nicknameText)} 참조가 비어 있습니다.", this);

            if (randomMatchButton == null)
                Debug.LogError($"[{nameof(LobbyView)}] {nameof(randomMatchButton)} 참조가 비어 있습니다.", this);

            if (createRoomButton == null)
                Debug.LogError($"[{nameof(LobbyView)}] {nameof(createRoomButton)} 참조가 비어 있습니다.", this);

            if (joinRoomButton == null)
                Debug.LogError($"[{nameof(LobbyView)}] {nameof(joinRoomButton)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            randomMatchButton.onClick.AddListener(HandleRandomMatchClicked);
            createRoomButton.onClick.AddListener(HandleCreateRoomClicked);
            joinRoomButton.onClick.AddListener(HandleJoinRoomClicked);
        }

        private void OnDisable()
        {
            randomMatchButton.onClick.RemoveListener(HandleRandomMatchClicked);
            createRoomButton.onClick.RemoveListener(HandleCreateRoomClicked);
            joinRoomButton.onClick.RemoveListener(HandleJoinRoomClicked);
        }

        /// <summary>좌상단 닉네임 표시 (14-lobby-ui 7절)</summary>
        public void SetNickname(string nickname) => nicknameText.text = nickname;

        private void HandleRandomMatchClicked() => OnRandomMatchRequested?.Invoke();
        private void HandleCreateRoomClicked() => OnCreateRoomRequested?.Invoke();
        private void HandleJoinRoomClicked() => OnJoinRoomRequested?.Invoke();
    }
}
