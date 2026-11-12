using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 오른쪽 판의 뽑기 — 상자 이름 · 대표 아이템 다섯 · 등급 확률 띠 · 개판 보장까지 남은 횟수 · 1회 / 10회.
    /// 이름 · 확률 · 값은 빌더가 LobbyHubData 의 값으로 적어 두고, 여기는 바뀌는 것(보유 · 남은 횟수)만 고친다.
    /// 기준: 「통합 화면 UI v2」 로비 gachaPane
    /// </summary>
    public class LobbyGachaPaneView : MonoBehaviour
    {
        private const string PityFormat = "개판까지 최대 {0}회";

        [SerializeField] private GameObject content;
        [SerializeField] private GameObject[] featuredOwned;
        [SerializeField] private TMP_Text pityText;
        [SerializeField] private RectTransform pityFill;
        [SerializeField] private Button oneButton;
        [SerializeField] private Button tenButton;

        public event Action<int> OnRollRequested;

        private void Awake()
        {
            if (content == null || featuredOwned == null || featuredOwned.Length != LobbyHubData.FeaturedIds.Length
                || pityText == null || pityFill == null || oneButton == null || tenButton == null)
                Debug.LogError($"[{nameof(LobbyGachaPaneView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            oneButton.onClick.AddListener(HandleOneClicked);
            tenButton.onClick.AddListener(HandleTenClicked);
        }

        private void OnDisable()
        {
            oneButton.onClick.RemoveListener(HandleOneClicked);
            tenButton.onClick.RemoveListener(HandleTenClicked);
        }

        public void Show(LobbyHubData data)
        {
            content.SetActive(true);
            for (int i = 0; i < featuredOwned.Length; i++)
                featuredOwned[i].SetActive(data.FindItem(LobbyHubData.FeaturedIds[i]).IsOwned);

            pityText.text = string.Format(PityFormat, LobbyHubData.GachaPityMax - data.GachaPity);
            pityFill.anchorMax = new Vector2((float)data.GachaPity / LobbyHubData.GachaPityMax, 1f);
        }

        public void Hide() => content.SetActive(false);

        private void HandleOneClicked() => OnRollRequested?.Invoke(1);

        private void HandleTenClicked() => OnRollRequested?.Invoke(10);
    }
}
