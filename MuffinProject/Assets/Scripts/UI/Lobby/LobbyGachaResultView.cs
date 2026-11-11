using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 뽑기 결과 — 카드를 한 장씩 누르거나 「모두 열기」 로 뒤집는다. 다 열리면 「N회 더」 · 「꾸미기에서 보기」 · 「확인」.
    /// 손맛(터짐)은 희귀 · 개판에만, 모두 열기에서는 가장 좋은 한 장에만.
    /// 기준: 「통합 화면 UI v2」 로비 gresHTML · openCard · openAll
    /// </summary>
    public class LobbyGachaResultView : MonoBehaviour
    {
        private const float OpenAllStepSeconds = 0.11f;
        private const string TitleFormat = "{0} · {1}회";
        private const string AgainFormat = "{0}회 더";
        private const string RefundFormat = "+{0}";

        private static readonly Vector2 CardSize = new Vector2(196f, 272f);
        private static readonly Vector2 SingleCardSize = new Vector2(240f, 332f);

        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform frame;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private LobbyGachaCardView[] cards;
        [SerializeField] private Button openAllButton;
        [SerializeField] private GameObject doneRow;
        [SerializeField] private GameObject refund;
        [SerializeField] private TMP_Text refundText;
        [SerializeField] private Button againButton;
        [SerializeField] private TMP_Text againLabel;
        [SerializeField] private Button wearButton;
        [SerializeField] private Button okButton;
        [SerializeField] private Sprite enterIcon;
        [SerializeField] private Sprite ghostIcon;
        [SerializeField] private Sprite cardIcon;
        [SerializeField] private Sprite sparkIcon;

        private int _count;
        private Coroutine _openAll;

        public bool IsOpen => content.activeSelf;

        public event Action<int> OnAgainRequested;

        /// <summary>「꾸미기에서 보기」 — 새로 얻은 것 중 가장 높은 등급 아이템.</summary>
        public event Action<HubItem> OnWearRequested;

        public event Action OnCloseRequested;

        /// <summary>희귀 · 개판 카드가 열렸다 — 터짐을 띄울 자리.</summary>
        public event Action<LobbyGachaCardView> OnRevealed;

        private void Awake()
        {
            if (content == null || frame == null || titleText == null || grid == null || cards == null || cards.Length != 10 || openAllButton == null
                || doneRow == null || refund == null || refundText == null || againButton == null || againLabel == null || wearButton == null || okButton == null)
                Debug.LogError($"[{nameof(LobbyGachaResultView)}] 참조가 비어 있습니다.", this);

            if (enterIcon == null || ghostIcon == null || cardIcon == null || sparkIcon == null)
                Debug.LogError($"[{nameof(LobbyGachaResultView)}] 아이콘 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            openAllButton.onClick.AddListener(HandleOpenAllClicked);
            againButton.onClick.AddListener(HandleAgainClicked);
            wearButton.onClick.AddListener(HandleWearClicked);
            okButton.onClick.AddListener(HandleOkClicked);
            foreach (LobbyGachaCardView card in cards)
                card.OnClicked += HandleCardClicked;
        }

        private void OnDisable()
        {
            openAllButton.onClick.RemoveListener(HandleOpenAllClicked);
            againButton.onClick.RemoveListener(HandleAgainClicked);
            wearButton.onClick.RemoveListener(HandleWearClicked);
            okButton.onClick.RemoveListener(HandleOkClicked);
            foreach (LobbyGachaCardView card in cards)
                card.OnClicked -= HandleCardClicked;
            _openAll = null; // 꺼지면 코루틴도 멈춘다
        }

        public void Show(List<HubGachaResult> results)
        {
            _count = results.Count;
            content.SetActive(true);
            titleText.text = string.Format(TitleFormat, LobbyHubData.GachaName, _count);
            grid.cellSize = _count == 1 ? SingleCardSize : CardSize;
            grid.constraintCount = _count == 1 ? 1 : 5;

            int totalRefund = 0;
            bool hasNew = false;
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].gameObject.SetActive(i < _count);
                if (i >= _count)
                    continue;

                cards[i].Bind(results[i], SlotIcon(results[i].Item.Slot));
                totalRefund += results[i].Refund;
                hasNew |= !results[i].IsDuplicate;
            }

            openAllButton.gameObject.SetActive(true);
            doneRow.SetActive(false);
            refund.SetActive(totalRefund > 0);
            refundText.text = string.Format(RefundFormat, LobbyStyle.Number(totalRefund));
            againLabel.text = string.Format(AgainFormat, _count);
            wearButton.gameObject.SetActive(hasNew);
        }

        public void Hide()
        {
            if (_openAll != null)
                StopCoroutine(_openAll);

            _openAll = null;
            content.SetActive(false);
        }

        /// <summary>뒤로가기 — 덜 열렸으면 모두 열고, 다 열렸으면 닫기를 알린다.</summary>
        public void Back()
        {
            if (AllOpen())
                OnCloseRequested?.Invoke();
            else
                OpenAll();
        }

        private Sprite SlotIcon(HubSlot slot) => slot switch
        {
            HubSlot.Enter => enterIcon,
            HubSlot.Death => ghostIcon,
            HubSlot.Card  => cardIcon,
            _             => sparkIcon,
        };

        private bool AllOpen()
        {
            for (int i = 0; i < _count; i++)
            {
                if (!cards[i].IsOpen)
                    return false;
            }

            return true;
        }

        private void OpenCard(LobbyGachaCardView card, bool withFx)
        {
            if (card.IsOpen)
                return;

            card.Open();
            HubGrade grade = card.Result.Item.Grade;
            if (withFx && grade >= HubGrade.Rare)
            {
                OnRevealed?.Invoke(card);
                if (grade == HubGrade.Legend)
                    UIJuice.Shake(frame, 16f, 0.42f);
            }

            if (AllOpen())
            {
                openAllButton.gameObject.SetActive(false);
                doneRow.SetActive(true);
            }
        }

        private void OpenAll()
        {
            if (_openAll == null)
                _openAll = StartCoroutine(OpenAllRoutine());
        }

        // 차례로 뒤집고, 손맛은 가장 좋은 한 장에만 (개판이 있으면 그 장)
        private IEnumerator OpenAllRoutine()
        {
            LobbyGachaCardView best = null;
            for (int i = 0; i < _count; i++)
            {
                HubGrade grade = cards[i].Result.Item.Grade;
                if (!cards[i].IsOpen && grade >= HubGrade.Rare && (best == null || grade > best.Result.Item.Grade))
                    best = cards[i];
            }

            var wait = new WaitForSecondsRealtime(OpenAllStepSeconds);
            for (int i = 0; i < _count; i++)
            {
                if (cards[i].IsOpen)
                    continue;

                OpenCard(cards[i], cards[i] == best);
                yield return wait;
            }

            _openAll = null;
        }

        private void HandleCardClicked(LobbyGachaCardView card) => OpenCard(card, true);

        private void HandleOpenAllClicked() => OpenAll();

        private void HandleAgainClicked() => OnAgainRequested?.Invoke(_count);

        private void HandleOkClicked() => OnCloseRequested?.Invoke();

        private void HandleWearClicked()
        {
            HubItem best = null;
            for (int i = 0; i < _count; i++)
            {
                HubGachaResult result = cards[i].Result;
                if (!result.IsDuplicate && (best == null || result.Item.Grade > best.Grade))
                    best = result.Item;
            }

            if (best != null)
                OnWearRequested?.Invoke(best);
        }
    }
}
