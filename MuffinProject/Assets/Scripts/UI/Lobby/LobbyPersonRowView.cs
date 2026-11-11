using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 사람 한 줄 (시안 .prow) — 친구(상태 + 「⋯」) · 받은 요청(수락 · 거절) · 보낸 요청(취소) · 차단(차단 해제) · 검색 결과(코드 + 요청).
    /// </summary>
    public class LobbyPersonRowView : MonoBehaviour
    {
        private const float FlashSeconds = 1.2f;
        private const string AcceptText = "수락";
        private const string DeclineText = "거절";
        private const string CancelText = "취소";
        private const string UnblockText = "차단 해제";
        private const string RequestText = "요청";
        private const string SentText = "요청됨";
        private const string FriendText = "친구";

        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject statusRow;
        [SerializeField] private RoundBox statusDot;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;
        [SerializeField] private Button menuButton;
        [SerializeField] private TMP_Text flatText;

        /// <summary>이 줄의 사람 — 검색 결과 줄이면 null</summary>
        public HubPerson Person { get; private set; }

        /// <summary>이 줄의 검색 결과 — 검색 결과 줄이 아니면 null</summary>
        public HubSearchResult Result { get; private set; }

        public RectTransform MenuButtonRect => (RectTransform)menuButton.transform;

        public event Action<LobbyPersonRowView> OnPrimaryClicked;
        public event Action<LobbyPersonRowView> OnSecondaryClicked;
        public event Action<LobbyPersonRowView> OnMenuClicked;

        private void Awake()
        {
            if (background == null || nameText == null || statusRow == null || statusDot == null || statusText == null || codeText == null
                || primaryButton == null || primaryLabel == null || secondaryButton == null || secondaryLabel == null || menuButton == null || flatText == null)
                Debug.LogError($"[{nameof(LobbyPersonRowView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            primaryButton.onClick.AddListener(HandlePrimaryClicked);
            secondaryButton.onClick.AddListener(HandleSecondaryClicked);
            menuButton.onClick.AddListener(HandleMenuClicked);
        }

        private void OnDisable()
        {
            primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
            secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
            menuButton.onClick.RemoveListener(HandleMenuClicked);
            background.DOKill();
            background.color = LobbyStyle.Soft;
        }

        public void BindFriend(HubPerson person)
        {
            Begin(person, null, person.Nickname);
            statusRow.SetActive(true);
            statusDot.FillColor = LobbyStyle.StatusColor(person.Status);
            statusText.text = LobbyHubData.StatusName(person.Status);
            menuButton.gameObject.SetActive(true);
        }

        public void BindRequestIn(HubPerson person)
        {
            Begin(person, null, person.Nickname);
            ShowPrimary(AcceptText);
            ShowSecondary(DeclineText, true);
        }

        public void BindRequestOut(HubPerson person)
        {
            Begin(person, null, person.Nickname);
            ShowSecondary(CancelText, true);
        }

        public void BindBlocked(HubPerson person)
        {
            Begin(person, null, person.Nickname);
            ShowSecondary(UnblockText, true);
        }

        /// <summary>검색 결과 — 닉네임이 겹칠 수 있어 친구 코드를 같이 보인다.</summary>
        public void BindSearch(HubSearchResult result)
        {
            Begin(null, result, result.Nickname);
            codeText.gameObject.SetActive(true);
            codeText.text = result.Code;
            if (result.Relation == HubRelation.None)
                ShowPrimary(RequestText);
            else if (result.Relation == HubRelation.Sent && result.IsJustSent)
                ShowSecondary(SentText, false);
            else
                ShowFlat(result.Relation == HubRelation.Friend ? FriendText : SentText);
        }

        /// <summary>방금 바뀐 줄 — 노랑에서 1.2초에 원래 색으로.</summary>
        public void Flash()
        {
            background.DOKill();
            background.color = LobbyStyle.Turn;
            background.DOColor(LobbyStyle.Soft, FlashSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void Begin(HubPerson person, HubSearchResult result, string nickname)
        {
            Person = person;
            Result = result;
            nameText.text = nickname;
            statusRow.SetActive(false);
            codeText.gameObject.SetActive(false);
            primaryButton.gameObject.SetActive(false);
            secondaryButton.gameObject.SetActive(false);
            menuButton.gameObject.SetActive(false);
            flatText.gameObject.SetActive(false);
        }

        private void ShowPrimary(string label)
        {
            primaryButton.gameObject.SetActive(true);
            primaryLabel.text = label;
        }

        private void ShowSecondary(string label, bool isEnabled)
        {
            secondaryButton.gameObject.SetActive(true);
            secondaryLabel.text = label;
            LobbyStyle.SetEnabled(secondaryButton, isEnabled);
        }

        private void ShowFlat(string text)
        {
            flatText.gameObject.SetActive(true);
            flatText.text = text;
        }

        private void HandlePrimaryClicked() => OnPrimaryClicked?.Invoke(this);

        private void HandleSecondaryClicked() => OnSecondaryClicked?.Invoke(this);

        private void HandleMenuClicked() => OnMenuClicked?.Invoke(this);
    }
}
