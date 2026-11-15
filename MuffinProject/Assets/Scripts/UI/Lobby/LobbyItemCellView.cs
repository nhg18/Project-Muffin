using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 격자 칸 한 개 — 꾸미기 칸(정사각) · 상점 칸(아래에 이름 + 값) · 충전 칸. 틀 색 = 등급.
    /// 기준: 「통합 화면 UI v2」 로비 .cell · .cell.shopc · .cell.pack
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LobbyItemCellView : MonoBehaviour
    {
        private const string OnlyGachaText = "뽑기 전용";
        private const string PackPriceText = "₩ 미정";

        [SerializeField] private Image frame;
        [SerializeField] private GameObject selection;
        [SerializeField] private RectTransform picture;
        [SerializeField] private CanvasGroup pictureGroup;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image cardBackImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text captionText;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject equippedStrip;
        [SerializeField] private GameObject ownedStrip;
        [SerializeField] private GameObject shopInfo;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private RoundBox priceBox;
        [SerializeField] private Image priceIcon;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private GameObject bonusTag;
        [SerializeField] private TMP_Text bonusText;

        [Tooltip("상점 칸에서 그림 자리 아래를 비우는 높이 (이름 + 값, 캔버스 px)")]
        [SerializeField] private float shopInfoHeight = 108f;

        [Tooltip("아이콘 크기 — 꾸미기 칸 (캔버스 px)")]
        [SerializeField] private float boxIconSize = 80f;

        [Tooltip("아이콘 크기 — 상점 아이템 칸 (캔버스 px)")]
        [SerializeField] private float shopIconSize = 72f;

        [Tooltip("아이콘 크기 — 충전 상품 칸 (캔버스 px)")]
        [SerializeField] private float packIconSize = 92f;

        [Header("연출")]
        [Tooltip("꾸미기에서 잠긴 아이템 그림의 불투명도 (0 ~ 1)")]
        [SerializeField] private float lockedAlpha = 0.3f;

        [Tooltip("상점에서 이미 가진 아이템 그림의 불투명도 (0 ~ 1)")]
        [SerializeField] private float ownedShopAlpha = 0.35f;

        private Button _button;

        /// <summary>아이템 ID 또는 충전 묶음 ID</summary>
        public string Id { get; private set; }

        public event Action<LobbyItemCellView> OnClicked;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (frame == null || selection == null || picture == null || pictureGroup == null || characterImage == null || cardBackImage == null
                || iconImage == null || captionText == null || lockIcon == null || equippedStrip == null || ownedStrip == null)
                Debug.LogError($"[{nameof(LobbyItemCellView)}] 그림 참조가 비어 있습니다.", this);

            if (shopInfo == null || nameText == null || priceBox == null || priceIcon == null || priceText == null
                || bonusTag == null || bonusText == null)
                Debug.LogError($"[{nameof(LobbyItemCellView)}] 상점 칸 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => _button.onClick.AddListener(HandleClicked);

        private void OnDisable() => _button.onClick.RemoveListener(HandleClicked);

        /// <summary>꾸미기 칸 — 잠긴 것은 흐리게 + 자물쇠, 장착한 것은 아래에 「장착 중」.</summary>
        public void BindBox(HubItem item, bool isEquipped, bool isSelected, Sprite slotIcon, string caption)
        {
            Begin(item.Id, LobbyStyle.GradeColor(item.Grade), isSelected, false);
            ShowPicture(item.Slot == HubSlot.Character, item.Slot == HubSlot.Card && item.IsDefault, slotIcon, caption, boxIconSize, !item.IsOwned);
            pictureGroup.alpha = !item.IsOwned && item.Slot != HubSlot.Character ? lockedAlpha : 1f;
            lockIcon.SetActive(!item.IsOwned);
            equippedStrip.SetActive(isEquipped);
        }

        /// <summary>상점 칸 — 가진 것은 위에 「보유」. priceIcon 이 없으면 뽑기에서만 나오는 아이템.</summary>
        public void BindShop(HubItem item, bool isSelected, Sprite slotIcon, Sprite currencyIcon, string price)
        {
            Begin(item.Id, LobbyStyle.GradeColor(item.Grade), isSelected, true);
            ShowPicture(item.Slot == HubSlot.Character, false, slotIcon, null, shopIconSize, true);
            pictureGroup.alpha = item.IsOwned ? ownedShopAlpha : 1f;
            ownedStrip.SetActive(item.IsOwned);
            nameText.text = item.Slot == HubSlot.Character ? item.Name : LobbyHubData.SlotName(item.Slot);
            SetPrice(currencyIcon, currencyIcon != null ? price : OnlyGachaText);
        }

        /// <summary>충전 칸 — 보너스가 있으면 오른쪽 위 꼬리표.</summary>
        public void BindPack(HubPack pack, bool isSelected, Sprite churuIcon)
        {
            Begin(pack.Id, LobbyStyle.PackFrame, isSelected, true);
            ShowPicture(false, false, churuIcon, null, packIconSize, false);
            iconImage.color = Color.white; // 재화 그림은 색이 박혀 있다
            nameText.text = $"츄르 {pack.Churu}";
            SetPrice(null, PackPriceText);
            bonusTag.SetActive(pack.Bonus > 0);
            bonusText.text = $"+{pack.Bonus}";
        }

        private void Begin(string id, Color frameColor, bool isSelected, bool isShop)
        {
            Id = id;
            frame.color = frameColor;
            selection.SetActive(isSelected);
            picture.offsetMin = new Vector2(picture.offsetMin.x, isShop ? shopInfoHeight : 0f);
            pictureGroup.alpha = 1f;
            shopInfo.SetActive(isShop);
            lockIcon.SetActive(false);
            equippedStrip.SetActive(false);
            ownedStrip.SetActive(false);
            bonusTag.SetActive(false);
        }

        // 그림이 있는 것은 기본 캐릭터 · 기본 뒷면 뿐 — 나머지 캐릭터는 실루엣, 이펙트는 슬롯 아이콘
        private void ShowPicture(bool isCharacter, bool isCardBack, Sprite icon, string caption, float iconSize, bool silhouette)
        {
            characterImage.gameObject.SetActive(isCharacter);
            characterImage.color = silhouette ? LobbyStyle.Silhouette : Color.white;
            cardBackImage.gameObject.SetActive(isCardBack);
            iconImage.gameObject.SetActive(!isCharacter && !isCardBack);
            iconImage.sprite = icon;
            iconImage.color = LobbyStyle.Ink;
            iconImage.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            captionText.gameObject.SetActive(!isCharacter && !isCardBack && !string.IsNullOrEmpty(caption));
            captionText.text = caption;
        }

        private void SetPrice(Sprite currencyIcon, string text)
        {
            bool hasPrice = currencyIcon != null;
            priceIcon.gameObject.SetActive(hasPrice);
            priceIcon.sprite = currencyIcon;
            priceText.text = text;
            priceText.color = hasPrice ? LobbyStyle.Ink : LobbyStyle.Ink2;
            priceBox.BorderColor = hasPrice ? LobbyStyle.Ink : LobbyStyle.Ink2;
        }

        private void HandleClicked() => OnClicked?.Invoke(this);
    }
}
