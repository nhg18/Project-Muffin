using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 오른쪽 위 — 재화 둘(뼈다귀 · 츄르) · 친구(배지) · 설정. 홈과 섹션 모두에서 보인다.
    /// 기준: 「통합 화면 UI v2」 로비 .tr · .wallet
    /// </summary>
    public class LobbyTopBarView : MonoBehaviour
    {
        [SerializeField] private Button boneButton;
        [SerializeField] private TMP_Text boneText;
        [SerializeField] private Button churuButton;
        [SerializeField] private TMP_Text churuText;
        [SerializeField] private Button friendsButton;
        [SerializeField] private GameObject friendsBadge;
        [SerializeField] private TMP_Text friendsBadgeText;
        [SerializeField] private Button settingsButton;

        public event Action<HubCurrency> OnWalletRequested;
        public event Action OnFriendsRequested;
        public event Action OnSettingsRequested;

        private void Awake()
        {
            if (boneButton == null || boneText == null || churuButton == null || churuText == null
                || friendsButton == null || friendsBadge == null || friendsBadgeText == null || settingsButton == null)
                Debug.LogError($"[{nameof(LobbyTopBarView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            boneButton.onClick.AddListener(HandleBoneClicked);
            churuButton.onClick.AddListener(HandleChuruClicked);
            friendsButton.onClick.AddListener(HandleFriendsClicked);
            settingsButton.onClick.AddListener(HandleSettingsClicked);
        }

        private void OnDisable()
        {
            boneButton.onClick.RemoveListener(HandleBoneClicked);
            churuButton.onClick.RemoveListener(HandleChuruClicked);
            friendsButton.onClick.RemoveListener(HandleFriendsClicked);
            settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        }

        public void SetWallet(int bone, int churu)
        {
            boneText.text = LobbyStyle.Number(bone);
            churuText.text = LobbyStyle.Number(churu);
        }

        /// <summary>재화 칸 — 「+N」 이 뜨는 자리</summary>
        public RectTransform CoinRect(HubCurrency currency)
            => (RectTransform)(currency == HubCurrency.Bone ? boneButton : churuButton).transform;

        /// <summary>친구 배지 = 받은 요청 + 초대. bump 면 배지가 톡 튄다 (새 소식).</summary>
        public void SetFriendsBadge(int count, bool bump = false)
        {
            friendsBadge.SetActive(count > 0);
            friendsBadgeText.text = count.ToString();
            if (bump && count > 0)
                UIJuice.Pop((RectTransform)friendsBadge.transform, 0.6f);
        }

        /// <summary>초대가 왔다 — 친구 버튼이 살짝 흔들린다.</summary>
        public void ShakeFriends() => UIJuice.Shake((RectTransform)friendsButton.transform, 6f, 0.26f);

        private void HandleBoneClicked() => OnWalletRequested?.Invoke(HubCurrency.Bone);

        private void HandleChuruClicked() => OnWalletRequested?.Invoke(HubCurrency.Churu);

        private void HandleFriendsClicked() => OnFriendsRequested?.Invoke();

        private void HandleSettingsClicked() => OnSettingsRequested?.Invoke();
    }
}
