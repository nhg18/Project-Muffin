using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 확률 보기 창 — 등급마다 확률 · 아이템 수 · 아이템 1개 확률 (확률형 아이템 정보 공개). 줄은 높은 등급부터.
    /// 기준: 「통합 화면 UI v2」 로비 ratesBody
    /// </summary>
    public class LobbyRatesPopupView : MonoBehaviour
    {
        private const string PercentFormat = "{0}%";
        private const string PerItemFormat = "{0:0.000}%";
        private const string NoItemText = "—";

        [SerializeField] private GameObject content;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text[] rateTexts;
        [SerializeField] private TMP_Text[] countTexts;
        [SerializeField] private TMP_Text[] perItemTexts;

        public bool IsOpen => content.activeSelf;

        public event Action OnCloseRequested;

        private void Awake()
        {
            int rows = LobbyHubData.GradesHighToLow.Length;
            if (content == null || closeButton == null || rateTexts == null || rateTexts.Length != rows
                || countTexts == null || countTexts.Length != rows || perItemTexts == null || perItemTexts.Length != rows)
                Debug.LogError($"[{nameof(LobbyRatesPopupView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => closeButton.onClick.AddListener(HandleCloseClicked);

        private void OnDisable() => closeButton.onClick.RemoveListener(HandleCloseClicked);

        public void Show(LobbyHubData data)
        {
            content.SetActive(true);
            for (int i = 0; i < LobbyHubData.GradesHighToLow.Length; i++)
            {
                HubGrade grade = LobbyHubData.GradesHighToLow[i];
                int rate = LobbyHubData.GachaRate(grade);
                int count = data.GachaPoolCount(grade);
                rateTexts[i].text = string.Format(PercentFormat, rate);
                countTexts[i].text = count.ToString();
                perItemTexts[i].text = count > 0 ? string.Format(PerItemFormat, (float)rate / count) : NoItemText;
            }
        }

        public void Hide() => content.SetActive(false);

        private void HandleCloseClicked() => OnCloseRequested?.Invoke();
    }
}
