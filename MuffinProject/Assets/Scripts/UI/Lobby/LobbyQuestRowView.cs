using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 퀘스트 한 줄 (시안 .qrow) — 제목 · 진행 「p / 목표」 · 막대 · 보상 · 「받기」 / 진행 중 / 받음.
    /// </summary>
    public class LobbyQuestRowView : MonoBehaviour
    {
        private const string ProgressFormat = "{0} / {1}";
        private const string InProgressText = "진행 중";
        private const string ClaimedText = "받음";

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private Image barFillImage;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button claimButton;
        [SerializeField] private GameObject state;
        [SerializeField] private GameObject stateCheck;
        [SerializeField] private TMP_Text stateText;

        [Header("연출")]
        [Tooltip("보상을 받은 줄의 불투명도 (0 ~ 1)")]
        [SerializeField] private float doneAlpha = 0.6f;

        /// <summary>이 줄의 퀘스트 — 「일일 퀘스트 다 끝내기」 줄이면 null</summary>
        public HubQuest Quest { get; private set; }

        public event Action<LobbyQuestRowView> OnClaimClicked;

        private void Awake()
        {
            if (group == null || titleText == null || progressText == null || barFill == null || barFillImage == null || rewardIcon == null
                || rewardText == null || claimButton == null || state == null || stateCheck == null || stateText == null)
                Debug.LogError($"[{nameof(LobbyQuestRowView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => claimButton.onClick.AddListener(HandleClaimClicked);

        private void OnDisable() => claimButton.onClick.RemoveListener(HandleClaimClicked);

        public void Bind(HubQuest quest, Sprite currencyIcon)
        {
            Quest = quest;
            Draw(quest.Title, Mathf.Min(quest.Progress, quest.Goal), quest.Goal, currencyIcon, quest.RewardAmount, quest.IsReady, quest.IsClaimed);
        }

        /// <summary>일일 퀘스트를 모두 받으면 열리는 보너스 줄.</summary>
        public void BindBonus(string title, int claimedCount, int total, Sprite currencyIcon, int amount, bool isReady, bool isClaimed)
        {
            Quest = null;
            Draw(title, claimedCount, total, currencyIcon, amount, isReady, isClaimed);
        }

        private void Draw(string title, int progress, int goal, Sprite currencyIcon, int amount, bool isReady, bool isClaimed)
        {
            titleText.text = title;
            progressText.text = string.Format(ProgressFormat, progress, goal);
            barFill.anchorMax = new Vector2(goal > 0 ? (float)progress / goal : 0f, 1f);
            barFillImage.color = isClaimed ? LobbyStyle.Off : LobbyStyle.Ok;
            rewardIcon.sprite = currencyIcon;
            rewardText.text = LobbyStyle.Number(amount);
            group.alpha = isClaimed ? doneAlpha : 1f;
            claimButton.gameObject.SetActive(isReady);
            state.SetActive(!isReady);
            stateCheck.SetActive(isClaimed);
            stateText.text = isClaimed ? ClaimedText : InProgressText;
        }

        private void HandleClaimClicked() => OnClaimClicked?.Invoke(this);
    }
}
