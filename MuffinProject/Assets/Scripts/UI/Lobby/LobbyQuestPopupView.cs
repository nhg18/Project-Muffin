using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 퀘스트 창 — 일일 · 주간 탭, 새로 바뀌기까지 남은 시간, 줄마다 「받기」, 아래 「모두 받기」.
    /// 받을 수 있는 것 → 진행 중 → 받은 것 순. 일일 탭 끝에는 「일일 퀘스트 다 끝내기」 보너스 줄.
    /// 기준: 「통합 화면 UI v2」 로비 questBody
    /// </summary>
    public class LobbyQuestPopupView : MonoBehaviour
    {
        private const float TimeRefreshSeconds = 20f;
        private const string DailyText = "일일";
        private const string WeeklyText = "주간";
        private const string BonusTitle = "일일 퀘스트 다 끝내기";
        private const string ClaimAllText = "모두 받기";
        private const string DaysFormat = "{0}일 {1}시간";
        private const string HoursFormat = "{0}시간 {1}분";

        [SerializeField] private GameObject content;
        [SerializeField] private Button closeButton;
        [SerializeField] private LobbyTabView dailyTab;
        [SerializeField] private LobbyTabView weeklyTab;
        [SerializeField] private TMP_Text timeLeftText;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LobbyQuestRowView[] rows;
        [SerializeField] private LobbyQuestRowView bonusRow;
        [SerializeField] private Button claimAllButton;
        [SerializeField] private TMP_Text claimAllLabel;
        [SerializeField] private Sprite boneIcon;
        [SerializeField] private Sprite churuIcon;

        private LobbyHubData _data;
        private Coroutine _clock;

        public bool IsOpen => content.activeSelf;

        public bool IsDaily { get; private set; } = true;

        public event Action OnCloseRequested;
        public event Action<HubQuest> OnClaimRequested;
        public event Action OnBonusRequested;
        public event Action OnClaimAllRequested;

        private void Awake()
        {
            if (content == null || closeButton == null || dailyTab == null || weeklyTab == null || timeLeftText == null || scroll == null
                || rows == null || rows.Length == 0 || bonusRow == null || claimAllButton == null || claimAllLabel == null || boneIcon == null || churuIcon == null)
                Debug.LogError($"[{nameof(LobbyQuestPopupView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            claimAllButton.onClick.AddListener(HandleClaimAllClicked);
            dailyTab.OnClicked += HandleTabClicked;
            weeklyTab.OnClicked += HandleTabClicked;
            bonusRow.OnClaimClicked += HandleBonusClicked;
            foreach (LobbyQuestRowView row in rows)
                row.OnClaimClicked += HandleClaimClicked;
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            claimAllButton.onClick.RemoveListener(HandleClaimAllClicked);
            dailyTab.OnClicked -= HandleTabClicked;
            weeklyTab.OnClicked -= HandleTabClicked;
            bonusRow.OnClaimClicked -= HandleBonusClicked;
            foreach (LobbyQuestRowView row in rows)
                row.OnClaimClicked -= HandleClaimClicked;
            _clock = null; // 꺼지면 코루틴도 멈춘다
        }

        public void Show(LobbyHubData data)
        {
            _data = data;
            content.SetActive(true);
            Refresh();
            if (_clock == null)
                _clock = StartCoroutine(ClockRoutine());
        }

        public void Hide()
        {
            content.SetActive(false);
            if (_clock != null)
                StopCoroutine(_clock);

            _clock = null;
        }

        public void Refresh()
        {
            if (!IsOpen)
                return;

            int dailyReady = _data.ReadyCount(true);
            int weeklyReady = _data.ReadyCount(false);
            dailyTab.SetLabel(dailyReady > 0 ? $"{DailyText} {dailyReady}" : DailyText);
            weeklyTab.SetLabel(weeklyReady > 0 ? $"{WeeklyText} {weeklyReady}" : WeeklyText);
            dailyTab.SetPressed(IsDaily);
            weeklyTab.SetPressed(!IsDaily);
            timeLeftText.text = TimeLeft();

            var quests = new List<HubQuest>(IsDaily ? _data.Daily : _data.Weekly);
            quests.Sort((a, b) => Order(a).CompareTo(Order(b)));
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].gameObject.SetActive(i < quests.Count);
                if (i >= quests.Count)
                    continue;

                rows[i].transform.SetSiblingIndex(i);
                rows[i].Bind(quests[i], quests[i].RewardCurrency == HubCurrency.Bone ? boneIcon : churuIcon);
            }

            bonusRow.gameObject.SetActive(IsDaily);
            if (IsDaily)
            {
                bonusRow.transform.SetSiblingIndex(quests.Count);
                bonusRow.BindBonus(BonusTitle, _data.DailyClaimedCount, _data.Daily.Count, churuIcon, LobbyHubData.DailyBonusChuru,
                    _data.IsDailyBonusReady, _data.IsDailyBonusClaimed);
            }

            int ready = IsDaily ? dailyReady : weeklyReady;
            claimAllLabel.text = ready > 0 ? $"{ClaimAllText} {ready}" : ClaimAllText;
            LobbyStyle.SetEnabled(claimAllButton, ready > 0);
        }

        // 받을 수 있는 것 0 · 진행 중 1 · 받은 것 2
        private static int Order(HubQuest quest) => quest.IsClaimed ? 2 : quest.IsReady ? 0 : 1;

        // 일일 = 오늘 자정까지, 주간 = 다음 월요일 0시까지 (기기 시각)
        private string TimeLeft()
        {
            DateTime now = DateTime.Now;
            DateTime reset = now.Date.AddDays(1);
            if (!IsDaily)
            {
                int days = (8 - (int)now.DayOfWeek) % 7;
                reset = now.Date.AddDays(days == 0 ? 7 : days);
            }

            TimeSpan left = reset - now;
            return left.Days > 0 ? string.Format(DaysFormat, left.Days, left.Hours) : string.Format(HoursFormat, left.Hours, left.Minutes);
        }

        private IEnumerator ClockRoutine()
        {
            var wait = new WaitForSecondsRealtime(TimeRefreshSeconds);
            while (true)
            {
                yield return wait;
                timeLeftText.text = TimeLeft();
            }
        }

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();

        private void HandleClaimAllClicked() => OnClaimAllRequested?.Invoke();

        private void HandleBonusClicked(LobbyQuestRowView row) => OnBonusRequested?.Invoke();

        private void HandleClaimClicked(LobbyQuestRowView row) => OnClaimRequested?.Invoke(row.Quest);

        private void HandleTabClicked(LobbyTabView tab)
        {
            IsDaily = tab == dailyTab;
            scroll.verticalNormalizedPosition = 1f;
            Refresh();
        }
    }
}
