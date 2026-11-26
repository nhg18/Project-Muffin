using System.Collections;
using System.Collections.Generic;
using Chapchu.Account;
using UnityEngine;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 홈 ↔ 섹션(꾸미기 · 상점 · 뽑기) 이동과 섹션 안의 흐름 — 고르기 · 장착 · 구매 · 쿠폰 구매 · 충전 · 뽑기.
    /// 값은 모두 LobbyHubData 의 시안 값이다 (서버 없음 — 저장을 기다리지 않는다).
    /// 기준: 「통합 화면 UI v3」 로비 · docs/systems/17-lobby-ui-full.md 5-5 · 7-6 · 7-7
    /// </summary>
    public class LobbySectionPresenter : MonoBehaviour
    {
        private enum Screen { Home, Box, Shop, Gacha }

        // 섹션
        private const string BoxName = "꾸미기";
        private const string ShopName = "상점";
        private const string ReplayText = "재생";
        private const string RatesText = "확률 보기";
        private const string OwnedText = "보유";
        private const string EquippedText = "장착 중";
        private const string LockedText = "잠김";
        private const string OnlyGachaText = "뽑기 전용";
        private const string NoPickHint = "아직 고른 상품이 없어요.";
        private const string ChargeHint = "츄르는 뽑기에 쓰는 재화예요.";
        private const string PackSubText = "뽑기에 써요 · 스토어 결제 · ₩ 미정";
        private const string GachaSubFormat = "{0}% · {1}회 안에 1개 보장";
        private const string EquippedToast = "장착했어요.";
        private const string BoughtToast = "샀어요. 보관함에 들어갔어요.";
        private const string ChargedToastFormat = "츄르 +{0} 받았어요.";
        private const string CouponName = "닉네임 변경 쿠폰";
        private const string CouponSubFormat = "닉네임 1회 변경 · 보유 {0}장";
        private const string CouponBoughtToast = "닉네임 변경 쿠폰을 샀어요.";
        private const string LegendBurstText = "개판!";
        private const string RareBurstText = "희귀";

        // 확인 · 경고 창
        private const string CancelText = "취소";
        private const string CloseText = "닫기";
        private const string BuyTitle = "구매";
        private const string BuyMessageFormat = "「{0}」({1} · {2}) — {3} {4}에 살까요?";
        private const string CouponBuyMessageFormat = "닉네임 변경 쿠폰 1장을 {0} {1}으로 살까요?"; // 값 500 · 20 — 받침이 있다
        private const string ShortTitleFormat = "{0}가 모자라요"; // 뼈다귀 · 츄르 — 둘 다 받침이 없다
        private const string ShortMessageFormat = "필요한 {0} {1} · 지금 {2}";
        private const string GachaShortMessageFormat = "{0}회 뽑기에 필요한 츄르 {1} · 지금 {2}";
        private const string ChargeTitle = "츄르 충전";
        private const string ChargeMessageFormat = "츄르를 {0} 받아요. 실제 결제는 스토어 결제창에서 해요 — 시안이라 바로 들어와요.";
        private const string ChargeBonusFormat = "{0} + 보너스 {1}";
        private const string ChargeOkText = "충전";
        private const string ToQuestsText = "퀘스트 보기";

        // 판이 다 미끄러진 다음 프레임까지 버튼을 잠가 둔다 — 판 이동 시간(LobbySectionView)에 더하는 여유
        private const float MoveLockMarginSeconds = 0.01f;

        [SerializeField] private LobbyPresenter lobby;
        [SerializeField] private LobbyPopupPresenter popups;

        [Header("화면")]
        [SerializeField] private LobbyHomeView home;
        [SerializeField] private LobbyTopBarView topBar;
        [SerializeField] private LobbyHeroView hero;
        [SerializeField] private LobbySectionView section;
        [SerializeField] private LobbyItemPaneView itemPane;
        [SerializeField] private LobbyGachaPaneView gachaPane;
        [SerializeField] private LobbyGachaResultView gachaResult;
        [SerializeField] private LobbyModalView modal;
        [SerializeField] private LobbyFxView fx;

        [Tooltip("꾸미기 「재생」 버튼의 아이콘")]
        [SerializeField] private Sprite playIcon;

        [Header("연출")]
        [Tooltip("뽑기에서 개판 등급이 열릴 때 터짐 그림의 크기 (캔버스 px)")]
        [SerializeField] private float legendBurstSize = 300f;

        [Tooltip("뽑기에서 희귀 등급이 열릴 때 터짐 그림의 크기 (캔버스 px)")]
        [SerializeField] private float rareBurstSize = 192f;

        [Tooltip("터짐 그림이 카드 위 끝에서 아래로 내려오는 거리 (캔버스 px)")]
        [SerializeField] private float revealBurstDrop = 80f;

        private Screen _screen = Screen.Home;
        private HubSlot _boxTab = HubSlot.Character;
        private HubItem _boxSelected;      // 꾸미기에서 고른 아이템 — 장착하지 않고 탭 · 화면을 떠나면 버린다
        private bool _shopCharge;          // 상점의 「충전」 탭인지 (아니면 「아이템」)
        private LobbyShopItemTab _shopItemTab;
        private HubItem _shopSelected;
        private bool _couponSelected;      // 「기타」 의 쿠폰 칸을 골랐는지
        private HubPack _packSelected;
        private LobbyShopTab _lastShopTab = LobbyShopTab.Gacha;

        /// <summary>홈 ↔ 섹션 판이 미끄러지는 중인지 — 이 동안은 플레이 버튼도 눌리지 않는다.</summary>
        public bool IsMoving { get; private set; }

        private LobbyHubData Data => lobby.Data;

        private HubItem BoxItem => _boxSelected ?? Data.Equipped(_boxTab);

        private void Awake()
        {
            if (lobby == null || popups == null)
                Debug.LogError($"[{nameof(LobbySectionPresenter)}] 로비 참조가 비어 있습니다.", this);

            if (home == null || topBar == null || hero == null || section == null || itemPane == null || gachaPane == null
                || gachaResult == null || modal == null || fx == null || playIcon == null)
                Debug.LogError($"[{nameof(LobbySectionPresenter)}] 화면 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            home.OnDecoRequested += HandleDecoRequested;
            home.OnShopRequested += HandleShopRequested;
            hero.OnClicked += HandleHeroClicked;
            topBar.OnWalletRequested += HandleWalletRequested;

            section.OnBackRequested += GoHome;
            section.OnShopTabRequested += HandleShopTabRequested;
            section.OnInfoButtonClicked += HandleInfoButtonClicked;
            itemPane.OnTabSelected += HandlePaneTabSelected;
            itemPane.OnCellClicked += HandleCellClicked;
            itemPane.OnFootClicked += HandleFootClicked;
            itemPane.OnFootAltClicked += HandleFootAltClicked;
            gachaPane.OnRollRequested += HandleRollRequested;
            gachaResult.OnAgainRequested += HandleGachaAgainRequested;
            gachaResult.OnWearRequested += HandleGachaWearRequested;
            gachaResult.OnCloseRequested += HandleGachaCloseRequested;
            gachaResult.OnRevealed += HandleGachaRevealed;
        }

        private void OnDisable()
        {
            home.OnDecoRequested -= HandleDecoRequested;
            home.OnShopRequested -= HandleShopRequested;
            hero.OnClicked -= HandleHeroClicked;
            topBar.OnWalletRequested -= HandleWalletRequested;

            section.OnBackRequested -= GoHome;
            section.OnShopTabRequested -= HandleShopTabRequested;
            section.OnInfoButtonClicked -= HandleInfoButtonClicked;
            itemPane.OnTabSelected -= HandlePaneTabSelected;
            itemPane.OnCellClicked -= HandleCellClicked;
            itemPane.OnFootClicked -= HandleFootClicked;
            itemPane.OnFootAltClicked -= HandleFootAltClicked;
            gachaPane.OnRollRequested -= HandleRollRequested;
            gachaResult.OnAgainRequested -= HandleGachaAgainRequested;
            gachaResult.OnWearRequested -= HandleGachaWearRequested;
            gachaResult.OnCloseRequested -= HandleGachaCloseRequested;
            gachaResult.OnRevealed -= HandleGachaRevealed;
            IsMoving = false; // 꺼지면 잠금을 푸는 코루틴도 멈춘다
        }

        private void Start()
        {
            RefreshHero();
        }

        /// <summary>섹션을 닫고 홈으로 돌아간다. 이미 홈이면 아무것도 하지 않는다.</summary>
        public void GoHome() => Go(Screen.Home);

        #region 화면 이동

        private void Go(Screen screen)
        {
            if (IsMoving || screen == _screen)
                return;

            Screen from = _screen;
            _screen = screen;
            _boxSelected = null;
            _shopSelected = null;
            _couponSelected = false;
            if (from == Screen.Home || screen == Screen.Home)
            {
                IsMoving = true;
                StartCoroutine(EndMoveRoutine());
            }

            AnalyticsRecorder.RecordScreenViewed(ToScreenName(screen));
            home.SetVisible(screen == Screen.Home);
            section.SetOpen(screen != Screen.Home);
            if (screen == Screen.Home)
            {
                RefreshHero();
                return;
            }

            RefreshSection();
            if (from != Screen.Home)
                section.FlashContent();
        }

        // 상점 · 뽑기는 같은 「상점」 화면이다 — 뽑기는 상점의 탭 (27 5-5)
        private static AnalyticsRecorder.ScreenName ToScreenName(Screen screen)
        {
            switch (screen)
            {
                case Screen.Box:
                    return AnalyticsRecorder.ScreenName.Decorate;
                case Screen.Shop:
                case Screen.Gacha:
                    return AnalyticsRecorder.ScreenName.Shop;
                default:
                    return AnalyticsRecorder.ScreenName.Lobby;
            }
        }

        // 판 이동 연출(SetUpdate(true))과 같은 시계 · 같은 길이로 잠금을 푼다
        private IEnumerator EndMoveRoutine()
        {
            yield return new WaitForSecondsRealtime(section.SlideSeconds + MoveLockMarginSeconds);
            IsMoving = false;
        }

        private void HandleDecoRequested() => Go(Screen.Box);

        private void HandleHeroClicked()
        {
            if (_screen == Screen.Home)
                Go(Screen.Box);
        }

        private void HandleShopRequested() => OpenShop(_lastShopTab);

        private void HandleWalletRequested(HubCurrency currency) => OpenShop(LobbyShopTab.Charge);

        private void HandleShopTabRequested(LobbyShopTab tab) => OpenShop(tab);

        private void OpenShop(LobbyShopTab tab, LobbyShopItemTab? itemTab = null)
        {
            _lastShopTab = tab;
            if (tab == LobbyShopTab.Gacha)
            {
                Go(Screen.Gacha);
                return;
            }

            _shopCharge = tab == LobbyShopTab.Charge;
            _shopItemTab = itemTab ?? _shopItemTab;
            _packSelected = null;
            if (_screen != Screen.Shop)
            {
                Go(Screen.Shop);
                return;
            }

            _shopSelected = null;
            _couponSelected = false;
            RefreshSection();
            itemPane.ScrollToTop();
        }

        // 그 아이템의 슬롯 탭을 연 꾸미기로 데려가 골라 둔다
        private void Wear(HubItem item)
        {
            _boxTab = item.Slot;
            Go(Screen.Box);
            if (_screen != Screen.Box)
                return;

            _boxSelected = item;
            RefreshSection();
        }

        private void ShowQuestsFromHome()
        {
            Go(Screen.Home);
            popups.ShowQuests();
        }

        #endregion

        #region 섹션 그리기

        private void RefreshSection()
        {
            switch (_screen)
            {
                case Screen.Box:
                    DrawBox();
                    break;
                case Screen.Shop:
                    if (_shopCharge)
                        DrawCharge();
                    else
                        DrawShop();
                    break;
                case Screen.Gacha:
                    DrawGacha();
                    break;
            }

            RefreshHero();
        }

        private void DrawBox()
        {
            HubItem item = BoxItem;
            string state = !item.IsOwned ? LockedText : Data.Equipped(item.Slot) == item ? EquippedText : OwnedText;
            bool canReplay = item.Slot == HubSlot.Enter || item.Slot == HubSlot.Death || item.Slot == HubSlot.Card;

            section.SetHeader(BoxName, false, LobbyShopTab.Item);
            section.SetInfo(item.Name, item.Grade, $"{LobbyHubData.SlotName(item.Slot)} · {state}");
            section.SetInfoButton(canReplay ? ReplayText : null, playIcon);
            gachaPane.Hide();
            itemPane.ShowBox(Data, _boxTab, item);
        }

        private void DrawShop()
        {
            section.SetHeader(ShopName, true, LobbyShopTab.Item);
            section.SetInfoButton(null);
            gachaPane.Hide();
            if (_shopItemTab == LobbyShopItemTab.Etc)
            {
                DrawCoupon();
                return;
            }

            itemPane.ShowShop(Data, _shopItemTab, _shopSelected);

            HubItem item = _shopSelected;
            if (item == null)
            {
                section.SetInfoHint(NoPickHint);
                return;
            }

            string slot = LobbyHubData.SlotName(item.Slot);
            if (item.IsOwned)
                section.SetInfo(item.Name, item.Grade, $"{slot} · {OwnedText}");
            else if (LobbyHubData.TryGetPrice(item, out HubCurrency currency, out int amount))
                section.SetInfo(item.Name, item.Grade, $"{slot} ·", null, itemPane.CurrencyIcon(currency), LobbyStyle.Number(amount));
            else
                section.SetInfo(item.Name, item.Grade, $"{slot} · {OnlyGachaText}");
        }

        // 「기타」 — 닉네임 변경 쿠폰 한 칸 (17 7-7)
        private void DrawCoupon()
        {
            itemPane.ShowCoupon(Data, _couponSelected);
            if (_couponSelected)
                section.SetInfo(CouponName, null, string.Format(CouponSubFormat, Data.NicknameCoupons));
            else
                section.SetInfoHint(NoPickHint);
        }

        private void DrawCharge()
        {
            section.SetHeader(ShopName, true, LobbyShopTab.Charge);
            section.SetInfoButton(null);
            gachaPane.Hide();
            itemPane.ShowPacks(Data, _packSelected);

            HubPack pack = _packSelected;
            if (pack == null)
                section.SetInfoHint(ChargeHint);
            else
                section.SetInfo(pack.Bonus > 0 ? $"츄르 {pack.Churu} + {pack.Bonus}" : $"츄르 {pack.Churu}", null, PackSubText, itemPane.CurrencyIcon(HubCurrency.Churu));
        }

        private void DrawGacha()
        {
            section.SetHeader(ShopName, true, LobbyShopTab.Gacha);
            section.SetInfo(LobbyHubData.GachaName, HubGrade.Legend,
                string.Format(GachaSubFormat, LobbyHubData.GachaRate(HubGrade.Legend), LobbyHubData.GachaPityMax));
            section.SetInfoButton(RatesText);
            itemPane.Hide();
            gachaPane.Show(Data);
        }

        // 왼쪽 캐릭터 = 지금 입혀 보는 캐릭터. 그림이 있는 것은 기본 캐릭터 뿐이라 나머지는 실루엣
        private void RefreshHero()
        {
            HubItem preview = Data.Equipped(HubSlot.Character);
            if (_screen == Screen.Box && BoxItem.Slot == HubSlot.Character)
                preview = BoxItem;
            else if (_screen == Screen.Shop && _shopSelected != null && _shopSelected.Slot == HubSlot.Character)
                preview = _shopSelected;

            hero.SetSilhouette(_screen == Screen.Gacha || !preview.IsDefault);
        }

        // 지갑을 바꾸고 「+N」 을 띄운다
        private void Pay(HubCurrency currency, int amount)
        {
            Data.Add(currency, amount);
            lobby.RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), amount);
        }

        #endregion

        #region 꾸미기 · 상점

        private void HandlePaneTabSelected(int index)
        {
            if (_screen == Screen.Box)
            {
                if ((HubSlot)index == _boxTab)
                    return;

                _boxTab = (HubSlot)index;
                _boxSelected = null; // 장착하지 않고 다른 탭으로 가면 고른 것은 버린다
            }
            else
            {
                _shopItemTab = (LobbyShopItemTab)index;
                _shopSelected = null;
                _couponSelected = false;
            }

            RefreshSection();
            itemPane.ScrollToTop();
        }

        private void HandleCellClicked(string id)
        {
            if (_screen == Screen.Box)
                _boxSelected = Data.FindItem(id);
            else if (_shopCharge)
                _packSelected = _packSelected != null && _packSelected.Id == id ? null : Data.Packs.Find(pack => pack.Id == id);
            else if (_shopItemTab == LobbyShopItemTab.Etc)
                _couponSelected = !_couponSelected;
            else
                _shopSelected = _shopSelected != null && _shopSelected.Id == id ? null : Data.FindItem(id);

            RefreshSection();
        }

        // 주 버튼 — 「기타」 에서는 쿠폰을 츄르로
        private void HandleFootClicked()
        {
            if (_screen == Screen.Box)
                EquipOrGet(BoxItem);
            else if (_shopCharge)
                ConfirmCharge();
            else if (_shopItemTab == LobbyShopItemTab.Etc)
            {
                if (_couponSelected)
                    ConfirmCoupon(HubCurrency.Churu);
            }
            else if (_shopSelected != null)
                BuyOrMove(_shopSelected);
        }

        // 왼쪽 흰 버튼 — 쿠폰을 뼈다귀로 (「기타」 에서만 보인다)
        private void HandleFootAltClicked()
        {
            if (_screen == Screen.Shop && !_shopCharge && _shopItemTab == LobbyShopItemTab.Etc && _couponSelected)
                ConfirmCoupon(HubCurrency.Bone);
        }

        private void EquipOrGet(HubItem item)
        {
            if (item.IsOwned)
            {
                // 서버가 없어 저장을 기다리지 않는다 — 바로 입힌다
                Data.Equip(item);
                AnalyticsRecorder.RecordItemEquipped(item.Id, item.Slot);
                _boxSelected = item;
                RefreshSection();
                hero.Pop();
                fx.Toast(EquippedToast);
                return;
            }

            // 잠긴 아이템 = 얻는 곳으로 데려간다
            if (Data.SourceOf(item) == HubSource.Gacha)
            {
                OpenShop(LobbyShopTab.Gacha);
                return;
            }

            OpenShop(LobbyShopTab.Item, item.Slot == HubSlot.Character ? LobbyShopItemTab.Character : LobbyShopItemTab.Effect);
            if (_screen != Screen.Shop)
                return;

            _shopSelected = item;
            RefreshSection();
        }

        private void BuyOrMove(HubItem item)
        {
            if (item.IsOwned)
            {
                Wear(item);
                return;
            }

            if (!LobbyHubData.TryGetPrice(item, out HubCurrency currency, out int amount))
            {
                OpenShop(LobbyShopTab.Gacha);
                return;
            }

            string currencyName = LobbyHubData.CurrencyName(currency);
            if (Data.Balance(currency) < amount)
            {
                string message = string.Format(ShortMessageFormat, currencyName, LobbyStyle.Number(amount), LobbyStyle.Number(Data.Balance(currency)));
                if (currency == HubCurrency.Churu)
                    modal.ShowWarn(string.Format(ShortTitleFormat, currencyName), message, ChargeTitle, CloseText, () => OpenShop(LobbyShopTab.Charge));
                else
                    modal.ShowWarn(string.Format(ShortTitleFormat, currencyName), message, ToQuestsText, CloseText, ShowQuestsFromHome);
                return;
            }

            modal.ShowWarn(BuyTitle,
                string.Format(BuyMessageFormat, item.Name, LobbyHubData.GradeName(item.Grade), LobbyHubData.SlotName(item.Slot), currencyName, LobbyStyle.Number(amount)),
                BuyTitle, CancelText, () => Buy(item, currency, amount), LobbyWarnKind.Ask);
        }

        private void Buy(HubItem item, HubCurrency currency, int amount)
        {
            if (!Data.Buy(item))
                return;

            lobby.RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), -amount);
            RefreshSection();
            fx.Toast(BoughtToast);
        }

        // 쿠폰 사기 — 모자라면 경고(츄르는 「츄르 충전」 으로, 뼈다귀는 닫기만), 충분하면 확인 → 쿠폰 +1 (17 7-7 · 24 3-4)
        private void ConfirmCoupon(HubCurrency currency)
        {
            string currencyName = LobbyHubData.CurrencyName(currency);
            int price = LobbyHubData.CouponPrice(currency);
            if (Data.Balance(currency) < price)
            {
                string title = string.Format(ShortTitleFormat, currencyName);
                string message = string.Format(ShortMessageFormat, currencyName, LobbyStyle.Number(price), LobbyStyle.Number(Data.Balance(currency)));
                if (currency == HubCurrency.Churu)
                    modal.ShowWarn(title, message, ChargeTitle, CloseText, () => OpenShop(LobbyShopTab.Charge));
                else
                    modal.ShowWarn(title, message, CloseText);
                return;
            }

            modal.ShowWarn(BuyTitle, string.Format(CouponBuyMessageFormat, currencyName, LobbyStyle.Number(price)),
                BuyTitle, CancelText, () => BuyCoupon(currency, price), LobbyWarnKind.Ask);
        }

        private void BuyCoupon(HubCurrency currency, int price)
        {
            if (!Data.BuyCoupon(currency))
                return;

            lobby.RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), -price);
            RefreshSection();
            fx.Toast(CouponBoughtToast);
        }

        private void ConfirmCharge()
        {
            HubPack pack = _packSelected;
            if (pack == null)
                return;

            string amount = pack.Bonus > 0 ? string.Format(ChargeBonusFormat, pack.Churu, pack.Bonus) : pack.Churu.ToString();
            modal.ShowWarn(ChargeTitle, string.Format(ChargeMessageFormat, amount), ChargeOkText, CancelText, () =>
            {
                Pay(HubCurrency.Churu, pack.Churu + pack.Bonus);
                fx.Toast(string.Format(ChargedToastFormat, pack.Churu + pack.Bonus));
            }, LobbyWarnKind.Ask);
        }

        private void HandleInfoButtonClicked()
        {
            if (_screen == Screen.Gacha)
            {
                popups.ShowRates();
                return;
            }

            // 「재생」 — 연출 그림이 아직 없어 기본 연출로 대신 보인다
            HubItem item = BoxItem;
            if (item.Slot == HubSlot.Card)
                hero.PlayCard(item.IsOwned);
            else if (item.Slot == HubSlot.Enter)
                hero.PlayEnter();
            else
                hero.PlayDeath();
        }

        #endregion

        #region 뽑기

        private void HandleRollRequested(int count)
        {
            int cost = count == LobbyHubData.GachaTenCount ? LobbyHubData.GachaCostTen : LobbyHubData.GachaCostOne;
            int balance = Data.Balance(HubCurrency.Churu);
            if (balance < cost)
            {
                modal.ShowWarn(string.Format(ShortTitleFormat, LobbyHubData.CurrencyName(HubCurrency.Churu)),
                    string.Format(GachaShortMessageFormat, count, cost, balance), ChargeTitle, CloseText, () => OpenShop(LobbyShopTab.Charge));
                return;
            }

            Pay(HubCurrency.Churu, -cost);
            List<HubGachaResult> results = Data.Roll(count); // 중복은 뼈다귀로 — 지갑에 바로 들어온다
            lobby.RefreshWallet();
            gachaResult.Show(results);
            if (_screen == Screen.Gacha)
                gachaPane.Show(Data);
        }

        private void HandleGachaAgainRequested(int count)
        {
            gachaResult.Hide();
            HandleRollRequested(count);
        }

        private void HandleGachaWearRequested(HubItem item)
        {
            gachaResult.Hide();
            Wear(item);
        }

        private void HandleGachaCloseRequested()
        {
            gachaResult.Hide();
            if (_screen == Screen.Gacha)
                gachaPane.Show(Data);
        }

        // 희귀 · 개판이 열린 순간 — 카드 위쪽에 터짐
        private void HandleGachaRevealed(LobbyGachaCardView card)
        {
            var rect = (RectTransform)card.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 position = (corners[1] + corners[2]) * 0.5f + rect.TransformVector(0f, -revealBurstDrop, 0f);
            if (card.Result.Item.Grade == HubGrade.Legend)
                fx.Burst(position, LegendBurstText, legendBurstSize, LobbyStyle.Turn);
            else
                fx.Burst(position, RareBurstText, rareBurstSize, LobbyStyle.RareBurst);
        }

        #endregion
    }
}
