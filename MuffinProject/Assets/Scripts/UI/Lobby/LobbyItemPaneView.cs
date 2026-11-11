using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 오른쪽 판의 아이템 격자 — 꾸미기(슬롯 탭 5) · 상점 아이템(캐릭터 · 이펙트) · 츄르 충전. 머리 띠 탭 · 격자 · 아래 버튼.
    /// 무엇을 골랐는지는 프레젠터가 들고, 여기는 받은 대로 그린다.
    /// 기준: 「통합 화면 UI v2」 로비 renderPane · shopPane · boxGetBtn
    /// </summary>
    public class LobbyItemPaneView : MonoBehaviour
    {
        private const string EquipText = "장착";
        private const string EquippedText = "장착 중";
        private const string GetText = "얻으러 가기";
        private const string ShopNoteText = "상점 ·";
        private const string GachaOnlyNoteText = "뽑기에서만 나와요";
        private const string BuyText = "구매";
        private const string BuyPriceFormat = "구매 ·";
        private const string WearText = "꾸미기에서 장착";
        private const string ToGachaText = "뽑기로";
        private const string ChargeText = "충전";
        private const string NoneCaption = "없음";
        private const string DefaultCaption = "기본";

        private static readonly string[] ShopTabNames = { "캐릭터", "이펙트" };
        private static readonly Vector2 BoxCellSize = new Vector2(196f, 196f);
        private static readonly Vector2 ShopCellSize = new Vector2(196f, 248f);

        [SerializeField] private GameObject content;
        [SerializeField] private LobbyTabView[] tabs;
        [SerializeField] private GameObject tabRow;
        [SerializeField] private GameObject chargeLabel;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LobbyItemCellView[] cells;

        [Header("아래 줄")]
        [SerializeField] private GameObject footNote;
        [SerializeField] private TMP_Text footNoteText;
        [SerializeField] private Image footNoteIcon;
        [SerializeField] private TMP_Text footNoteAmount;
        [SerializeField] private Button footButton;
        [SerializeField] private Image footIcon;
        [SerializeField] private TMP_Text footLabel;
        [SerializeField] private Image footPriceIcon;
        [SerializeField] private TMP_Text footPriceText;

        [Header("아이콘")]
        [SerializeField] private Sprite boneIcon;
        [SerializeField] private Sprite churuIcon;
        [SerializeField] private Sprite shopIcon;
        [SerializeField] private Sprite giftIcon;
        [SerializeField] private Sprite shirtIcon;
        [SerializeField] private Sprite enterIcon;
        [SerializeField] private Sprite ghostIcon;
        [SerializeField] private Sprite cardIcon;
        [SerializeField] private Sprite sparkIcon;
        [SerializeField] private Sprite noneIcon;

        public event Action<int> OnTabSelected;
        public event Action<string> OnCellClicked;
        public event Action OnFootClicked;

        private void Awake()
        {
            if (content == null || tabs == null || tabs.Length == 0 || tabRow == null || chargeLabel == null || grid == null || scroll == null || cells == null || cells.Length == 0)
                Debug.LogError($"[{nameof(LobbyItemPaneView)}] 탭 · 격자 참조가 비어 있습니다.", this);

            if (footNote == null || footNoteText == null || footNoteIcon == null || footNoteAmount == null
                || footButton == null || footIcon == null || footLabel == null || footPriceIcon == null || footPriceText == null)
                Debug.LogError($"[{nameof(LobbyItemPaneView)}] 아래 줄 참조가 비어 있습니다.", this);

            if (boneIcon == null || churuIcon == null || shopIcon == null || giftIcon == null || shirtIcon == null
                || enterIcon == null || ghostIcon == null || cardIcon == null || sparkIcon == null || noneIcon == null)
                Debug.LogError($"[{nameof(LobbyItemPaneView)}] 아이콘 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            foreach (LobbyTabView tab in tabs)
                tab.OnClicked += HandleTabClicked;
            foreach (LobbyItemCellView cell in cells)
                cell.OnClicked += HandleCellClicked;
            footButton.onClick.AddListener(HandleFootClicked);
        }

        private void OnDisable()
        {
            foreach (LobbyTabView tab in tabs)
                tab.OnClicked -= HandleTabClicked;
            foreach (LobbyItemCellView cell in cells)
                cell.OnClicked -= HandleCellClicked;
            footButton.onClick.RemoveListener(HandleFootClicked);
        }

        public void Hide() => content.SetActive(false);

        public Sprite CurrencyIcon(HubCurrency currency) => currency == HubCurrency.Bone ? boneIcon : churuIcon;

        public Sprite SlotIcon(HubSlot slot) => slot switch
        {
            HubSlot.Enter => enterIcon,
            HubSlot.Death => ghostIcon,
            HubSlot.Card  => cardIcon,
            _             => sparkIcon,
        };

        /// <summary>꾸미기 — 슬롯 탭 다섯. selected = 지금 미리 보는 아이템 (고르지 않았으면 장착한 것).</summary>
        public void ShowBox(LobbyHubData data, HubSlot slot, HubItem selected)
        {
            ShowTabs(5, (int)slot, index => LobbyHubData.SlotName((HubSlot)index));
            grid.cellSize = BoxCellSize;

            List<HubItem> items = data.ItemsOf(slot);
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].gameObject.SetActive(i < items.Count);
                if (i >= items.Count)
                    continue;

                HubItem item = items[i];
                bool isNone = item.Slot == HubSlot.Aura && item.IsDefault;
                string caption = isNone ? NoneCaption : item.IsDefault ? DefaultCaption : null;
                cells[i].BindBox(item, data.Equipped(slot) == item, item == selected, isNone ? noneIcon : SlotIcon(slot), caption);
            }

            if (selected.IsOwned)
            {
                bool isEquipped = data.Equipped(slot) == selected;
                SetFoot(isEquipped ? EquippedText : EquipText, !isEquipped);
                return;
            }

            // 잠긴 아이템 = 어디서 얻는지 보여 주고 바로 데려간다
            if (data.SourceOf(selected) == HubSource.Shop && LobbyHubData.TryGetPrice(selected, out HubCurrency currency, out int amount))
            {
                SetFoot(GetText, true, shopIcon);
                SetFootNote(ShopNoteText, CurrencyIcon(currency), LobbyStyle.Number(amount));
            }
            else
            {
                SetFoot(GetText, true, giftIcon);
                SetFootNote(GachaOnlyNoteText, null, null);
            }
        }

        /// <summary>상점 아이템 — 캐릭터 · 이펙트 탭. selected 가 없으면 「구매」 가 꺼져 있다.</summary>
        public void ShowShop(LobbyHubData data, bool effects, HubItem selected)
        {
            ShowTabs(ShopTabNames.Length, effects ? 1 : 0, index => ShopTabNames[index]);
            grid.cellSize = ShopCellSize;

            List<HubItem> items = data.ShopItems(effects);
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].gameObject.SetActive(i < items.Count);
                if (i >= items.Count)
                    continue;

                HubItem item = items[i];
                bool hasPrice = LobbyHubData.TryGetPrice(item, out HubCurrency currency, out int amount);
                cells[i].BindShop(item, item == selected, SlotIcon(item.Slot), hasPrice ? CurrencyIcon(currency) : null, LobbyStyle.Number(amount));
            }

            if (selected == null)
                SetFoot(BuyText, false);
            else if (selected.IsOwned)
                SetFoot(WearText, true, shirtIcon);
            else if (!LobbyHubData.TryGetPrice(selected, out HubCurrency priceCurrency, out int price))
                SetFoot(ToGachaText, true, giftIcon);
            else
                SetFoot(BuyPriceFormat, true, null, CurrencyIcon(priceCurrency), LobbyStyle.Number(price));
        }

        /// <summary>츄르 충전 — 탭 대신 안내 한 줄.</summary>
        public void ShowPacks(LobbyHubData data, HubPack selected)
        {
            content.SetActive(true);
            tabRow.SetActive(false);
            chargeLabel.SetActive(true);
            grid.cellSize = ShopCellSize;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].gameObject.SetActive(i < data.Packs.Count);
                if (i < data.Packs.Count)
                    cells[i].BindPack(data.Packs[i], data.Packs[i] == selected, churuIcon);
            }

            SetFoot(ChargeText, selected != null);
        }

        /// <summary>격자를 맨 위로 — 탭을 바꿨을 때.</summary>
        public void ScrollToTop() => scroll.verticalNormalizedPosition = 1f;

        private void ShowTabs(int count, int current, Func<int, string> nameOf)
        {
            content.SetActive(true);
            tabRow.SetActive(true);
            chargeLabel.SetActive(false);
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].gameObject.SetActive(i < count);
                if (i >= count)
                    continue;

                tabs[i].SetLabel(nameOf(i));
                tabs[i].SetPressed(i == current);
            }
        }

        private void SetFoot(string label, bool isEnabled, Sprite icon = null, Sprite priceIcon = null, string price = null)
        {
            footNote.SetActive(false);
            footLabel.text = label;
            footIcon.gameObject.SetActive(icon != null);
            footIcon.sprite = icon;
            footPriceIcon.gameObject.SetActive(priceIcon != null);
            footPriceIcon.sprite = priceIcon;
            footPriceText.gameObject.SetActive(price != null);
            footPriceText.text = price;
            LobbyStyle.SetEnabled(footButton, isEnabled);
        }

        private void SetFootNote(string text, Sprite icon, string amount)
        {
            footNote.SetActive(true);
            footNoteText.text = text;
            footNoteIcon.gameObject.SetActive(icon != null);
            footNoteIcon.sprite = icon;
            footNoteAmount.gameObject.SetActive(amount != null);
            footNoteAmount.text = amount;
        }

        private void HandleTabClicked(LobbyTabView tab) => OnTabSelected?.Invoke(Array.IndexOf(tabs, tab));

        private void HandleCellClicked(LobbyItemCellView cell) => OnCellClicked?.Invoke(cell.Id);

        private void HandleFootClicked() => OnFootClicked?.Invoke();
    }
}
