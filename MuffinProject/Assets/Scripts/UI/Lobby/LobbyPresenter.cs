using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chapchu.Account;
using Chapchu.Core;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.Title;
using UnityEngine;
using UnityEngine.SceneManagement;
using ErrorCode = Photon.Realtime.ErrorCode; // Photon.Realtime 에도 AccountService 가 있어 통째로 가져오지 않는다

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 허브의 흐름 — 홈 ↔ 섹션(꾸미기 · 상점 · 뽑기) · 창 · 방 요청. Scripts/UI/Lobby 에서 네트워크 · 계정을 아는 유일한 파일이다.
    /// 진짜인 것: 닉네임(변경은 서버 저장) · 방 만들기 / 참가 / 랜덤 매칭 / 혼자 하기 · 설정 스위치(기기 저장).
    /// 임시인 것: 친구 · 재화 · 퀘스트 · 상점 · 뽑기 — LobbyHubData 의 시안 값, 이 기기 메모리에만 있다.
    /// 기준: 「통합 화면 UI v2」 로비(게시본 1794316285-7080) · docs/systems/17-lobby-ui-full.md · 14-lobby-ui.md · 08-room.md 7절
    /// </summary>
    public class LobbyPresenter : MonoBehaviour
    {
        private enum Screen { Home, Box, Shop, Gacha }

        private enum RoomRequest { Random, Create, Solo, Join }

        // 방 요청 (08-room 7절 · 22-popup-ui 3절 — 확정 문구)
        private const string SearchingText = "방을 찾고 있어요";
        private const string CreatingText = "방을 만들고 있어요";
        private const string JoiningText = "방에 들어가고 있어요";
        private const string CreateFailedTitle = "방 생성 실패";
        private const string JoinFailedTitle = "방 참가 실패";
        private const string RandomMatchFailedTitle = "랜덤 매칭 실패";
        private const string RoomNotFoundText = "방을 찾을 수 없습니다.";
        private const string RoomFullText = "방이 가득 찼습니다.";
        private const string RoomClosedText = "이미 시작된 방입니다.";
        private const string NoRandomRoomText = "참가할 수 있는 방이 없습니다.";
        private const string NoRoomCodeText = "방 코드가 없습니다.";
        private const string RetryLaterText = "잠시 후 다시 시도해 주세요.";

        // 섹션
        private const string BoxName = "꾸미기";
        private const string ShopName = "상점";
        private const string ReplayText = "재생";
        private const string RatesText = "확률 보기";
        private const string OwnedText = "보유";
        private const string EquippedText = "장착 중";
        private const string LockedText = "잠김";
        private const string OnlyGachaText = "뽑기 전용";
        private const string NoPickHint = "아직 고른 상품이 없어요";
        private const string ChargeHint = "츄르는 뽑기에 쓰는 재화예요";
        private const string PackSubText = "뽑기에 써요 · 스토어 결제 · ₩ 미정";
        private const string GachaSubFormat = "{0}% · {1}회 안에 1개 보장";
        private const string EquippedToast = "장착했어요.";
        private const string BoughtToast = "샀어요. 보관함에 들어갔어요.";
        private const string ChargedToastFormat = "츄르 +{0} 받았어요.";
        private const string ClaimedAllToast = "보상을 모두 받았어요.";

        // 확인 · 경고 창
        private const string OkText = "확인";
        private const string CancelText = "취소";
        private const string CloseText = "닫기";
        private const string BuyTitle = "구매";
        private const string BuyMessageFormat = "'{0}' ({1} · {2}) 을 {3} {4}으로 살까요?";
        private const string ShortTitleFormat = "{0}가 모자라요";
        private const string ShortMessageFormat = "필요한 {0} {1} · 지금 {2}";
        private const string GachaShortMessageFormat = "{0}회 뽑기에 필요한 츄르 {1} · 지금 {2}";
        private const string ChargeTitle = "츄르 충전";
        private const string ChargeMessageFormat = "츄르 {0}을 받아요. 실제 결제는 스토어 결제창에서 해요 — 시안이라 바로 들어와요.";
        private const string ChargeBonusFormat = "{0} + 보너스 {1}";
        private const string ChargeOkText = "충전";
        private const string ToQuestsText = "퀘스트 보기";
        private const string UnfriendTitle = "친구 삭제";
        private const string UnfriendMessageFormat = "{0} 님을 친구에서 삭제할까요?";
        private const string BlockTitle = "차단";
        private const string BlockMessageFormat = "{0} 님을 차단할까요? 요청 · 초대가 막히고 목록에서 사라져요.";
        private const string UnblockTitle = "차단 해제";
        private const string UnblockMessage = "차단을 해제할까요? 친구로 돌아오지는 않아요.";
        private const string DeleteTitle = "계정 삭제";
        private const string DeleteMessage = "계정을 삭제할까요? 친구 · 통계 · 보유 아이템이 모두 지워지고 되돌릴 수 없어요.";
        private const string DeleteWord = "삭제";
        private const string DeleteWordError = "'삭제' 와 똑같이 입력해야 지워져요.";

        // 프로필 · 설정
        private const string GuestText = "게스트";
        private const string AccountText = "계정";
        private const string LoginNoteFormat = "{0} 로 로그인";
        private const string LoginNameFormat = "{0} 로그인";
        private const string RenamingText = "바꾸고 있어요";
        private const string RenamedToast = "닉네임을 바꿨어요.";
        private const string RenameFailedTitle = "닉네임 변경 실패";
        private const string CopiedToast = "친구 코드를 복사했어요.";
        private const string RequestToastFormat = "{0} 님이 친구 요청을 보냈어요.";

        // 화면을 오갈 때 버튼이 다시 눌리지 않는 시간 (판이 미끄러지는 0.25초) · 초대 배너 수명 · 손맛 크기 (캔버스 px)
        private const float MoveLockSeconds = 0.26f;
        private const int InviteSeconds = 30;
        private const float LegendBurstSize = 300f;
        private const float RareBurstSize = 192f;
        private const float RevealBurstDrop = 80f;

        // 앱이 뒤로 갔다 돌아온 프레임의 큰 시간은 세지 않는다 (TitlePresenter 와 같다)
        private const float MaxCountedFrameSeconds = 0.1f;
        private const float RoomRequestTimeoutSeconds = 10f; // 14-lobby-ui 6절

        [Header("화면")]
        [SerializeField] private LobbyHomeView home;
        [SerializeField] private LobbyTopBarView topBar;
        [SerializeField] private LobbyHeroView hero;
        [SerializeField] private LobbySectionView section;
        [SerializeField] private LobbyItemPaneView itemPane;
        [SerializeField] private LobbyGachaPaneView gachaPane;

        [Header("창")]
        [SerializeField] private GameObject popupDimmer;
        [SerializeField] private LobbyFriendsPopupView friends;
        [SerializeField] private LobbyProfilePopupView profile;
        [SerializeField] private LobbySettingsPopupView settings;
        [SerializeField] private LobbyQuestPopupView quests;
        [SerializeField] private LobbyRatesPopupView rates;
        [SerializeField] private LobbyModalView modal;
        [SerializeField] private LobbyInviteBannerView invite;
        [SerializeField] private LobbyGachaResultView gachaResult;
        [SerializeField] private LobbyFxView fx;

        [SerializeField] private Sprite playIcon;

        private LobbyHubData _data;
        private Screen _screen = Screen.Home;
        private HubSlot _boxTab = HubSlot.Character;
        private HubItem _boxSelected;      // 꾸미기에서 고른 아이템 — 장착하지 않고 탭 · 화면을 떠나면 버린다
        private bool _shopCharge;          // 상점의 「충전」 탭인지 (아니면 「아이템」)
        private bool _shopEffects;         // 아이템 탭 안의 「이펙트」 인지 (아니면 「캐릭터」)
        private HubItem _shopSelected;
        private HubPack _packSelected;
        private LobbyShopTab _lastShopTab = LobbyShopTab.Gacha;
        private bool _isMoving;
        private RoomRequest _request;
        private Coroutine _roomTimeout; // 방 요청의 응답을 기다리는 동안만 있다

        private void Awake()
        {
            if (home == null || topBar == null || hero == null || section == null || itemPane == null || gachaPane == null)
                Debug.LogError($"[{nameof(LobbyPresenter)}] 화면 참조가 비어 있습니다.", this);

            if (popupDimmer == null || friends == null || profile == null || settings == null || quests == null || rates == null
                || modal == null || invite == null || gachaResult == null || fx == null || playIcon == null)
                Debug.LogError($"[{nameof(LobbyPresenter)}] 창 참조가 비어 있습니다.", this);

            _data = new LobbyHubData();
        }

        private void OnEnable()
        {
            home.OnProfileRequested += HandleProfileRequested;
            home.OnQuestsRequested += HandleQuestsRequested;
            home.OnDecoRequested += HandleDecoRequested;
            home.OnShopRequested += HandleShopRequested;
            home.OnSoloRequested += HandleSoloRequested;
            home.OnCreateRequested += HandleCreateRoomRequested;
            home.OnCodeRequested += HandleJoinRoomRequested;
            home.OnRandomRequested += HandleRandomMatchRequested;
            hero.OnClicked += HandleHeroClicked;
            topBar.OnWalletRequested += HandleWalletRequested;
            topBar.OnFriendsRequested += HandleFriendsRequested;
            topBar.OnSettingsRequested += HandleSettingsRequested;

            section.OnBackRequested += HandleSectionBackRequested;
            section.OnShopTabRequested += HandleShopTabRequested;
            section.OnInfoButtonClicked += HandleInfoButtonClicked;
            itemPane.OnTabSelected += HandlePaneTabSelected;
            itemPane.OnCellClicked += HandleCellClicked;
            itemPane.OnFootClicked += HandleFootClicked;
            gachaPane.OnRollRequested += HandleRollRequested;
            gachaResult.OnAgainRequested += HandleGachaAgainRequested;
            gachaResult.OnWearRequested += HandleGachaWearRequested;
            gachaResult.OnCloseRequested += HandleGachaCloseRequested;
            gachaResult.OnRevealed += HandleGachaRevealed;

            friends.OnCloseRequested += ClosePopups;
            friends.OnCopyRequested += HandleCopyRequested;
            friends.OnAcceptRequested += HandleFriendAccepted;
            friends.OnDeclineRequested += HandleFriendDeclined;
            friends.OnCancelRequested += HandleFriendRequestCanceled;
            friends.OnFriendRequestRequested += HandleFriendRequestRequested;
            friends.OnDeleteRequested += HandleUnfriendRequested;
            friends.OnBlockRequested += HandleBlockRequested;
            profile.OnCloseRequested += ClosePopups;
            profile.OnChangeRequested += HandleNicknameChangeRequested;
            profile.OnCopyRequested += HandleCopyRequested;
            settings.OnCloseRequested += ClosePopups;
            settings.OnSfxChanged += HandleSfxChanged;
            settings.OnBgmChanged += HandleBgmChanged;
            settings.OnInviteChanged += HandleInviteNoticeChanged;
            settings.OnBlockListRequested += HandleBlockListRequested;
            settings.OnBackRequested += ShowSettings;
            settings.OnUnblockRequested += HandleUnblockRequested;
            settings.OnDeleteAccountRequested += HandleDeleteAccountRequested;
            settings.OnPrivacyRequested += HandlePrivacyRequested;
            settings.OnContactRequested += HandleContactRequested;
            quests.OnCloseRequested += ClosePopups;
            quests.OnClaimRequested += HandleQuestClaimRequested;
            quests.OnBonusRequested += HandleQuestBonusRequested;
            quests.OnClaimAllRequested += HandleQuestClaimAllRequested;
            rates.OnCloseRequested += ClosePopups;
            modal.OnInputSubmitted += HandleInputSubmitted;
            invite.OnAccepted += HandleInviteAccepted;
            invite.OnDeclined += RefreshBadges;
            invite.OnExpired += RefreshBadges;

            RoomEvents.OnJoinedRoom += HandleJoinedRoom;
            RoomEvents.OnCreateRoomFailed += HandleCreateRoomFailed;
            RoomEvents.OnJoinRoomFailed += HandleJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed += HandleJoinRandomFailed;
        }

        private void OnDisable()
        {
            home.OnProfileRequested -= HandleProfileRequested;
            home.OnQuestsRequested -= HandleQuestsRequested;
            home.OnDecoRequested -= HandleDecoRequested;
            home.OnShopRequested -= HandleShopRequested;
            home.OnSoloRequested -= HandleSoloRequested;
            home.OnCreateRequested -= HandleCreateRoomRequested;
            home.OnCodeRequested -= HandleJoinRoomRequested;
            home.OnRandomRequested -= HandleRandomMatchRequested;
            hero.OnClicked -= HandleHeroClicked;
            topBar.OnWalletRequested -= HandleWalletRequested;
            topBar.OnFriendsRequested -= HandleFriendsRequested;
            topBar.OnSettingsRequested -= HandleSettingsRequested;

            section.OnBackRequested -= HandleSectionBackRequested;
            section.OnShopTabRequested -= HandleShopTabRequested;
            section.OnInfoButtonClicked -= HandleInfoButtonClicked;
            itemPane.OnTabSelected -= HandlePaneTabSelected;
            itemPane.OnCellClicked -= HandleCellClicked;
            itemPane.OnFootClicked -= HandleFootClicked;
            gachaPane.OnRollRequested -= HandleRollRequested;
            gachaResult.OnAgainRequested -= HandleGachaAgainRequested;
            gachaResult.OnWearRequested -= HandleGachaWearRequested;
            gachaResult.OnCloseRequested -= HandleGachaCloseRequested;
            gachaResult.OnRevealed -= HandleGachaRevealed;

            friends.OnCloseRequested -= ClosePopups;
            friends.OnCopyRequested -= HandleCopyRequested;
            friends.OnAcceptRequested -= HandleFriendAccepted;
            friends.OnDeclineRequested -= HandleFriendDeclined;
            friends.OnCancelRequested -= HandleFriendRequestCanceled;
            friends.OnFriendRequestRequested -= HandleFriendRequestRequested;
            friends.OnDeleteRequested -= HandleUnfriendRequested;
            friends.OnBlockRequested -= HandleBlockRequested;
            profile.OnCloseRequested -= ClosePopups;
            profile.OnChangeRequested -= HandleNicknameChangeRequested;
            profile.OnCopyRequested -= HandleCopyRequested;
            settings.OnCloseRequested -= ClosePopups;
            settings.OnSfxChanged -= HandleSfxChanged;
            settings.OnBgmChanged -= HandleBgmChanged;
            settings.OnInviteChanged -= HandleInviteNoticeChanged;
            settings.OnBlockListRequested -= HandleBlockListRequested;
            settings.OnBackRequested -= ShowSettings;
            settings.OnUnblockRequested -= HandleUnblockRequested;
            settings.OnDeleteAccountRequested -= HandleDeleteAccountRequested;
            settings.OnPrivacyRequested -= HandlePrivacyRequested;
            settings.OnContactRequested -= HandleContactRequested;
            quests.OnCloseRequested -= ClosePopups;
            quests.OnClaimRequested -= HandleQuestClaimRequested;
            quests.OnBonusRequested -= HandleQuestBonusRequested;
            quests.OnClaimAllRequested -= HandleQuestClaimAllRequested;
            rates.OnCloseRequested -= ClosePopups;
            modal.OnInputSubmitted -= HandleInputSubmitted;
            invite.OnAccepted -= HandleInviteAccepted;
            invite.OnDeclined -= RefreshBadges;
            invite.OnExpired -= RefreshBadges;

            RoomEvents.OnJoinedRoom -= HandleJoinedRoom;
            RoomEvents.OnCreateRoomFailed -= HandleCreateRoomFailed;
            RoomEvents.OnJoinRoomFailed -= HandleJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed -= HandleJoinRandomFailed;
            _isMoving = false; // 꺼지면 잠금을 푸는 코루틴도 멈춘다
        }

        private void Start()
        {
            // 돌아갈 씬 · 시작할 게임 씬은 로비에 들어온 순간 정한다. 입장 콜백(OnJoinedRoom)에서 정하면 안 된다 —
            // 이미 있는 방에 들어가면 PUN 씬 동기화(AutomaticallySyncScene)가 콜백보다 먼저 RoomScene 을 불러
            // 이 오브젝트가 사라지고, 값이 기본값(Lobby)으로 남는다.
            SceneFlow.ReturnSceneAfterRoom = ScenePaths.Lobby;
            SceneFlow.GameSceneAfterRoom = ScenePaths.Game;

            home.SetProfile(NetworkManager.Nickname, _data.MyCode);
            RefreshWallet();
            RefreshBadges();
            RefreshHero();
        }

        private void Update()
        {
            // Android 뒤로가기 (17 3절 #5)
            if (Input.GetKeyDown(KeyCode.Escape))
                Back();
        }

        #region 홈 · 화면 이동

        // 뒤로가기 한 번에 한 겹 — 뽑기 결과 → 위층 창 → 줄 메뉴 → 창 → 섹션
        private void Back()
        {
            if (gachaResult.IsOpen)
                gachaResult.Back();
            else if (modal.IsLoading)
                return;
            else if (modal.IsWarnOpen)
                modal.HideWarn();
            else if (modal.IsInputOpen)
                modal.HideInput();
            else if (friends.IsOpen && friends.CloseMenu())
                return;
            else if (settings.IsBlockListOpen)
                ShowSettings();
            else if (IsPopupOpen)
                ClosePopups();
            else if (_screen != Screen.Home)
                Go(Screen.Home);
        }

        private void Go(Screen screen)
        {
            if (_isMoving || screen == _screen)
                return;

            Screen from = _screen;
            _screen = screen;
            _boxSelected = null;
            _shopSelected = null;
            if (from == Screen.Home || screen == Screen.Home)
            {
                _isMoving = true;
                StartCoroutine(EndMoveRoutine());
            }

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

        // 판 이동 연출(SetUpdate(true))과 같은 시계로 잠금을 푼다
        private IEnumerator EndMoveRoutine()
        {
            yield return new WaitForSecondsRealtime(MoveLockSeconds);
            _isMoving = false;
        }

        private void HandleDecoRequested() => Go(Screen.Box);

        private void HandleHeroClicked()
        {
            if (_screen == Screen.Home)
                Go(Screen.Box);
        }

        private void HandleShopRequested() => OpenShop(_lastShopTab);

        private void HandleWalletRequested(HubCurrency currency) => OpenShop(LobbyShopTab.Charge);

        private void HandleSectionBackRequested() => Go(Screen.Home);

        private void HandleShopTabRequested(LobbyShopTab tab) => OpenShop(tab);

        private void OpenShop(LobbyShopTab tab, bool? effects = null)
        {
            _lastShopTab = tab;
            if (tab == LobbyShopTab.Gacha)
            {
                Go(Screen.Gacha);
                return;
            }

            _shopCharge = tab == LobbyShopTab.Charge;
            _shopEffects = effects ?? _shopEffects;
            _packSelected = null;
            if (_screen != Screen.Shop)
            {
                Go(Screen.Shop);
                return;
            }

            _shopSelected = null;
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

        #endregion

        #region 섹션 그리기

        private HubItem BoxItem => _boxSelected ?? _data.Equipped(_boxTab);

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
            string state = !item.IsOwned ? LockedText : _data.Equipped(item.Slot) == item ? EquippedText : OwnedText;
            bool canReplay = item.Slot == HubSlot.Enter || item.Slot == HubSlot.Death || item.Slot == HubSlot.Card;

            section.SetHeader(BoxName, false, LobbyShopTab.Item);
            section.SetInfo(item.Name, item.Grade, $"{LobbyHubData.SlotName(item.Slot)} · {state}");
            section.SetInfoButton(canReplay ? ReplayText : null, playIcon);
            gachaPane.Hide();
            itemPane.ShowBox(_data, _boxTab, item);
        }

        private void DrawShop()
        {
            section.SetHeader(ShopName, true, LobbyShopTab.Item);
            section.SetInfoButton(null);
            gachaPane.Hide();
            itemPane.ShowShop(_data, _shopEffects, _shopSelected);

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

        private void DrawCharge()
        {
            section.SetHeader(ShopName, true, LobbyShopTab.Charge);
            section.SetInfoButton(null);
            gachaPane.Hide();
            itemPane.ShowPacks(_data, _packSelected);

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
            gachaPane.Show(_data);
        }

        // 왼쪽 캐릭터 = 지금 입혀 보는 캐릭터. 그림이 있는 것은 기본 캐릭터 뿐이라 나머지는 실루엣
        private void RefreshHero()
        {
            HubItem preview = _data.Equipped(HubSlot.Character);
            if (_screen == Screen.Box && BoxItem.Slot == HubSlot.Character)
                preview = BoxItem;
            else if (_screen == Screen.Shop && _shopSelected != null && _shopSelected.Slot == HubSlot.Character)
                preview = _shopSelected;

            hero.SetSilhouette(_screen == Screen.Gacha || !preview.IsDefault);
        }

        private void RefreshWallet() => topBar.SetWallet(_data.Balance(HubCurrency.Bone), _data.Balance(HubCurrency.Churu));

        private void RefreshBadges()
        {
            topBar.SetFriendsBadge(_data.RequestsIn.Count + (invite.IsShown ? 1 : 0));
            home.SetQuestBadge(_data.ReadyCount(true) + _data.ReadyCount(false));
        }

        // 지갑을 바꾸고 「+N」 을 띄운다
        private void Pay(HubCurrency currency, int amount)
        {
            _data.Add(currency, amount);
            RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), amount);
        }

        #endregion

        #region 꾸미기 · 상점 · 뽑기

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
                _shopEffects = index == 1;
                _shopSelected = null;
            }

            RefreshSection();
            itemPane.ScrollToTop();
        }

        private void HandleCellClicked(string id)
        {
            if (_screen == Screen.Box)
                _boxSelected = _data.FindItem(id);
            else if (_shopCharge)
                _packSelected = _packSelected != null && _packSelected.Id == id ? null : _data.Packs.Find(pack => pack.Id == id);
            else
                _shopSelected = _shopSelected != null && _shopSelected.Id == id ? null : _data.FindItem(id);

            RefreshSection();
        }

        private void HandleFootClicked()
        {
            if (_screen == Screen.Box)
                EquipOrGet(BoxItem);
            else if (_shopCharge)
                ConfirmCharge();
            else if (_shopSelected != null)
                BuyOrMove(_shopSelected);
        }

        private void EquipOrGet(HubItem item)
        {
            if (item.IsOwned)
            {
                // 서버가 없어 저장을 기다리지 않는다 — 바로 입힌다
                _data.Equip(item);
                _boxSelected = item;
                RefreshSection();
                hero.Pop();
                fx.Toast(EquippedToast);
                return;
            }

            // 잠긴 아이템 = 얻는 곳으로 데려간다
            if (_data.SourceOf(item) == HubSource.Gacha)
            {
                OpenShop(LobbyShopTab.Gacha);
                return;
            }

            OpenShop(LobbyShopTab.Item, item.Slot != HubSlot.Character);
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
            if (_data.Balance(currency) < amount)
            {
                string message = string.Format(ShortMessageFormat, currencyName, LobbyStyle.Number(amount), LobbyStyle.Number(_data.Balance(currency)));
                if (currency == HubCurrency.Churu)
                    modal.ShowWarn(string.Format(ShortTitleFormat, currencyName), message, ChargeTitle, CloseText, () => OpenShop(LobbyShopTab.Charge));
                else
                    modal.ShowWarn(string.Format(ShortTitleFormat, currencyName), message, ToQuestsText, CloseText, ShowQuestsFromHome);
                return;
            }

            modal.ShowWarn(BuyTitle,
                string.Format(BuyMessageFormat, item.Name, LobbyHubData.GradeName(item.Grade), LobbyHubData.SlotName(item.Slot), currencyName, LobbyStyle.Number(amount)),
                BuyTitle, CancelText, () => Buy(item, currency, amount));
        }

        private void Buy(HubItem item, HubCurrency currency, int amount)
        {
            if (!_data.Buy(item))
                return;

            RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), -amount);
            RefreshSection();
            fx.Toast(BoughtToast);
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
            });
        }

        private void ShowQuestsFromHome()
        {
            Go(Screen.Home);
            HandleQuestsRequested();
        }

        private void HandleInfoButtonClicked()
        {
            if (_screen == Screen.Gacha)
            {
                ClosePopups();
                rates.Show(_data);
                popupDimmer.SetActive(true);
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

        private void HandleRollRequested(int count)
        {
            int cost = count == 10 ? LobbyHubData.GachaCostTen : LobbyHubData.GachaCostOne;
            int balance = _data.Balance(HubCurrency.Churu);
            if (balance < cost)
            {
                modal.ShowWarn(string.Format(ShortTitleFormat, LobbyHubData.CurrencyName(HubCurrency.Churu)),
                    string.Format(GachaShortMessageFormat, count, cost, balance), ChargeTitle, CloseText, () => OpenShop(LobbyShopTab.Charge));
                return;
            }

            Pay(HubCurrency.Churu, -cost);
            List<HubGachaResult> results = _data.Roll(count); // 중복은 뼈다귀로 — 지갑에 바로 들어온다
            RefreshWallet();
            gachaResult.Show(results);
            if (_screen == Screen.Gacha)
                gachaPane.Show(_data);
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
                gachaPane.Show(_data);
        }

        // 희귀 · 개판이 열린 순간 — 카드 위쪽에 터짐
        private void HandleGachaRevealed(LobbyGachaCardView card)
        {
            var rect = (RectTransform)card.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 position = (corners[1] + corners[2]) * 0.5f + rect.TransformVector(0f, -RevealBurstDrop, 0f);
            if (card.Result.Item.Grade == HubGrade.Legend)
                fx.Burst(position, "개판!", LegendBurstSize, LobbyStyle.Turn);
            else
                fx.Burst(position, "희귀", RareBurstSize, LobbyStyle.RareBurst);
        }

        #endregion

        #region 창 — 친구 · 프로필 · 설정 · 퀘스트

        private bool IsPopupOpen => friends.IsOpen || profile.IsOpen || settings.IsOpen || quests.IsOpen || rates.IsOpen;

        private void ClosePopups()
        {
            friends.Hide();
            profile.Hide();
            settings.Hide();
            quests.Hide();
            rates.Hide();
            popupDimmer.SetActive(false);
        }

        private void HandleFriendsRequested()
        {
            ClosePopups();
            friends.Show(_data);
            popupDimmer.SetActive(true);
        }

        private void HandleProfileRequested()
        {
            ClosePopups();
            profile.Show(NetworkManager.Nickname, _data, IsAccountLogin ? string.Format(LoginNoteFormat, AccountText) : GuestText);
            popupDimmer.SetActive(true);
        }

        private void HandleSettingsRequested() => ShowSettings();

        private void HandleQuestsRequested()
        {
            ClosePopups();
            quests.Show(_data);
            popupDimmer.SetActive(true);
        }

        private void ShowSettings()
        {
            ClosePopups();
            settings.ShowSettings(ReadSwitch(PlayerPrefsKeys.SfxOn), ReadSwitch(PlayerPrefsKeys.BgmOn), ReadSwitch(PlayerPrefsKeys.InviteNoticeOn),
                _data.Blocked.Count, IsAccountLogin ? string.Format(LoginNameFormat, AccountText) : GuestText);
            popupDimmer.SetActive(true);
        }

        // 계정(Google · Apple)으로 들어왔는지 — 어느 수단인지는 아직 남기지 않아 「계정」 으로만 보인다
        private static bool IsAccountLogin => PlayerPrefs.GetInt(PlayerPrefsKeys.AccountLogin, 0) == 1;

        private static bool ReadSwitch(string key) => PlayerPrefs.GetInt(key, 1) == 1;

        private static void SaveSwitch(string key, bool isOn)
        {
            PlayerPrefs.SetInt(key, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void HandleSfxChanged(bool isOn) => SaveSwitch(PlayerPrefsKeys.SfxOn, isOn);

        private void HandleBgmChanged(bool isOn) => SaveSwitch(PlayerPrefsKeys.BgmOn, isOn);

        private void HandleInviteNoticeChanged(bool isOn) => SaveSwitch(PlayerPrefsKeys.InviteNoticeOn, isOn);

        private void HandleBlockListRequested()
        {
            ClosePopups();
            settings.ShowBlockList(_data);
            popupDimmer.SetActive(true);
        }

        private void HandleUnblockRequested(HubPerson person)
        {
            modal.ShowWarn(UnblockTitle, UnblockMessage, OkText, CancelText, () =>
            {
                _data.Blocked.Remove(person);
                if (settings.IsBlockListOpen)
                    settings.ShowBlockList(_data);
            });
        }

        // 주소가 아직 없다 (17 미정) — 정해지면 Application.OpenURL
        private void HandlePrivacyRequested() => Debug.LogWarning($"[{nameof(LobbyPresenter)}] 개인정보처리방침 주소가 아직 정해지지 않아 열지 않는다.");

        private void HandleContactRequested() => Debug.LogWarning($"[{nameof(LobbyPresenter)}] 문의 주소가 아직 정해지지 않아 열지 않는다.");

        private void HandleCopyRequested()
        {
            GUIUtility.systemCopyBuffer = _data.MyCode;
            fx.Toast(CopiedToast);
        }

        private void HandleFriendAccepted(HubPerson request)
        {
            _data.Accept(request);
            RefreshBadges();
            friends.ShowTab(LobbyFriendsTab.Friends);
            friends.Flash(request.Nickname);
        }

        private void HandleFriendDeclined(HubPerson request)
        {
            _data.RequestsIn.Remove(request);
            RefreshBadges();
            friends.Refresh();
        }

        private void HandleFriendRequestCanceled(HubPerson request)
        {
            _data.RequestsOut.Remove(request);
            friends.Refresh();
        }

        private void HandleFriendRequestRequested(HubSearchResult result)
        {
            _data.Request(result);
            RefreshBadges();
            friends.Refresh();
        }

        private void HandleUnfriendRequested(HubPerson friend)
        {
            modal.ShowWarn(UnfriendTitle, string.Format(UnfriendMessageFormat, friend.Nickname), OkText, CancelText, () =>
            {
                _data.Friends.Remove(friend);
                friends.Refresh();
            });
        }

        private void HandleBlockRequested(HubPerson friend)
        {
            modal.ShowWarn(BlockTitle, string.Format(BlockMessageFormat, friend.Nickname), OkText, CancelText, () =>
            {
                _data.Block(friend);
                if (invite.Inviter == friend.Nickname)
                    invite.Hide(); // 차단한 친구의 초대 배너도 닫힌다

                RefreshBadges();
                friends.Refresh();
            });
        }

        private void HandleQuestClaimRequested(HubQuest quest)
        {
            if (!_data.Claim(quest))
                return;

            AfterQuestReward(quest.RewardCurrency, quest.RewardAmount);
        }

        private void HandleQuestBonusRequested()
        {
            if (_data.ClaimDailyBonus())
                AfterQuestReward(HubCurrency.Churu, LobbyHubData.DailyBonusChuru);
        }

        private void HandleQuestClaimAllRequested()
        {
            (int bone, int churu) = _data.ClaimAll(quests.IsDaily);
            if (bone == 0 && churu == 0)
                return;

            RefreshWallet();
            fx.Gain(topBar.CoinRect(HubCurrency.Bone), bone);
            fx.Gain(topBar.CoinRect(HubCurrency.Churu), churu);
            fx.Toast(ClaimedAllToast);
            quests.Refresh();
            RefreshBadges();
        }

        private void AfterQuestReward(HubCurrency currency, int amount)
        {
            RefreshWallet();
            fx.Gain(topBar.CoinRect(currency), amount);
            quests.Refresh();
            RefreshBadges();
        }

        #endregion

        #region 닉네임 변경 · 계정 삭제

        private void HandleNicknameChangeRequested() => modal.ShowInput(LobbyInputKind.Nickname, NetworkManager.Nickname);

        private void HandleDeleteAccountRequested()
            => modal.ShowWarn(DeleteTitle, DeleteMessage, OkText, CancelText, () => modal.ShowInput(LobbyInputKind.DeleteAccount));

        private void HandleInputSubmitted(LobbyInputKind kind, string value)
        {
            switch (kind)
            {
                case LobbyInputKind.RoomCode:
                    modal.HideInput();
                    if (string.IsNullOrEmpty(value))
                        modal.ShowWarn(JoinFailedTitle, NoRoomCodeText);
                    else
                        RequestRoom(RoomRequest.Join, value);
                    break;

                case LobbyInputKind.Nickname:
                    SubmitNickname(value);
                    break;

                case LobbyInputKind.DeleteAccount:
                    SubmitDeleteAccount(value);
                    break;
            }
        }

        // 타이틀과 같은 검사 (12-title-ui 4-2 · 금칙어 23 8절) → 통과하면 서버 저장 (17 5-2)
        private void SubmitNickname(string nickname)
        {
            NicknameValidationResult result = NicknameValidator.Validate(nickname);
            if (result == NicknameValidationResult.Valid && AccountService.IsInitialized && ServerConfig.HasValues
                && NicknameValidator.ContainsBannedWord(nickname, ServerConfig.BannedWords, ServerConfig.AllowedWords))
                result = NicknameValidationResult.BannedWord;

            if (result != NicknameValidationResult.Valid)
            {
                modal.SetInputError(NicknameValidator.GetErrorMessage(result));
                return;
            }

            modal.HideInput();
            if (nickname != NetworkManager.Nickname)
                StartCoroutine(RenameRoutine(nickname));
        }

        private IEnumerator RenameRoutine(string nickname)
        {
            modal.ShowLoading(RenamingText);
            Task save = AccountService.IsSignedIn ? AccountService.SaveNicknameAsync(nickname) : Task.FromException(new System.InvalidOperationException("로그인하지 않음"));
            float waited = 0f;
            while (!save.IsCompleted && waited < AccountService.RequestTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, MaxCountedFrameSeconds);
                yield return null;
            }

            modal.HideLoading();
            if (!save.IsCompleted || save.IsFaulted || save.IsCanceled)
            {
                // 시간이 넘은 요청이 나중에 실패해도 처리되지 않은 예외로 남지 않게
                save.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                Debug.LogWarning($"[{nameof(LobbyPresenter)}] 닉네임 저장 실패: {save.Exception?.GetBaseException().Message ?? "시간 초과"}");
                modal.ShowWarn(RenameFailedTitle, RetryLaterText);
                yield break;
            }

            NetworkManager.Instance.SetNickname(nickname);
            home.SetProfile(nickname, _data.MyCode);
            if (profile.IsOpen)
                profile.SetNickname(nickname);

            fx.Toast(RenamedToast);
        }

        // 「삭제」 를 똑같이 입력해야 넘어간다 (23 6-4). 서버의 계정 삭제 요청이 아직 없어, 문서의 실패 갈래(계정은 그대로)로 끝난다
        private void SubmitDeleteAccount(string typed)
        {
            if (typed != DeleteWord)
            {
                modal.SetInputError(DeleteWordError);
                return;
            }

            modal.HideInput();
            Debug.LogWarning($"[{nameof(LobbyPresenter)}] 계정 삭제 요청(AccountService)이 아직 없어 삭제하지 않는다 — 23-login 6-4.");
            modal.ShowWarn(DeleteTitle, RetryLaterText);
        }

        #endregion

        #region 방 요청 — 결과는 RoomEvents 로 온다

        private bool CanPlay => !invite.IsShown && !_isMoving && !modal.IsLoading; // 초대 배너가 떠 있는 동안 플레이 버튼은 눌리지 않는다 (17 5-1)

        private void HandleRandomMatchRequested()
        {
            if (CanPlay)
                RequestRoom(RoomRequest.Random);
        }

        private void HandleCreateRoomRequested()
        {
            if (CanPlay)
                RequestRoom(RoomRequest.Create);
        }

        private void HandleSoloRequested()
        {
            if (CanPlay)
                RequestRoom(RoomRequest.Solo);
        }

        private void HandleJoinRoomRequested()
        {
            if (CanPlay)
                modal.ShowInput(LobbyInputKind.RoomCode);
        }

        private void RequestRoom(RoomRequest request, string roomCode = null)
        {
            if (modal.IsLoading)
                return;

            _request = request;
            // 서버가 새 요청을 받을 수 있다 = 포기한 옛 요청은 끝났다. 받을 수 없으면(옛 요청이 아직 처리 중) 표시를 그대로 둔다 —
            // 새 요청은 그 자리에서 실패하고, 옛 요청의 늦은 입장은 계속 막는다
            if (NetworkManager.IsReady)
                SceneFlow.IsRoomRequestAbandoned = false;
            // 요청이 부르는 자리에서 바로 실패할 수 있어, 요청보다 먼저 센다
            _roomTimeout = StartCoroutine(RoomTimeoutRoutine());
            switch (request)
            {
                case RoomRequest.Random:
                    modal.ShowLoading(SearchingText);
                    NetworkManager.Instance.JoinRandomRoom();
                    break;
                case RoomRequest.Create:
                    modal.ShowLoading(CreatingText);
                    NetworkManager.Instance.CreateRoom();
                    break;
                case RoomRequest.Solo:
                    modal.ShowLoading(CreatingText);
                    NetworkManager.Instance.CreateSoloRoom();
                    break;
                default:
                    modal.ShowLoading(JoiningText);
                    NetworkManager.Instance.JoinRoom(roomCode);
                    break;
            }
        }

        private void HandleJoinedRoom()
        {
            if (_roomTimeout == null) // 10초가 지나 이미 실패로 알린 요청의 늦은 입장 — 방으로 가지 않고 나간다
            {
                SceneFlow.IsRoomRequestAbandoned = false; // 포기한 요청이 끝났다
                NetworkManager.Instance.LeaveRoom();
                return;
            }

            SceneManager.LoadScene(ScenePaths.Room);
        }

        private void HandleCreateRoomFailed(short code, string message) => ShowRoomError(CreateFailedTitle, code, message);

        private void HandleJoinRoomFailed(short code, string message) => ShowRoomError(JoinFailedTitle, code, message);

        // 랜덤 매칭이 방을 못 찾아 새로 만들다 실패한 경우도 여기로 온다
        private void HandleJoinRandomFailed(short code, string message)
            => ShowRoomError(_request == RoomRequest.Random ? RandomMatchFailedTitle : JoinFailedTitle, code, message);

        private void ShowRoomError(string title, short code, string message)
        {
            if (_roomTimeout == null) // 10초가 지나 이미 실패로 알린 요청의 늦은 응답 — 포기한 요청이 끝났다
            {
                SceneFlow.IsRoomRequestAbandoned = false;
                return;
            }

            StopCoroutine(_roomTimeout);
            _roomTimeout = null;
            modal.HideLoading();
            modal.ShowWarn(title, GetFailureMessage(code, message));
        }

        // 10초 안에 응답이 없으면 실패로 알린다 (14-lobby-ui 6절)
        private IEnumerator RoomTimeoutRoutine()
        {
            float waited = 0f;
            while (waited < RoomRequestTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, MaxCountedFrameSeconds);
                yield return null;
            }

            _roomTimeout = null;
            // 이미 있는 방의 늦은 입장은 씬 동기화가 먼저 방 씬을 열어 HandleJoinedRoom 이 불리지 않는다 — 방 화면이 보고 나간다
            SceneFlow.IsRoomRequestAbandoned = true;
            modal.HideLoading();
            modal.ShowWarn(_request switch
            {
                RoomRequest.Random => RandomMatchFailedTitle,
                RoomRequest.Join   => JoinFailedTitle,
                _                  => CreateFailedTitle,
            }, RetryLaterText);
        }

        // 08-room.md 7절. Photon 원문(message)은 우리 코드가 직접 만든 사유(InvalidOperation)일 때만 그대로 쓴다.
        private static string GetFailureMessage(short code, string message) => code switch
        {
            ErrorCode.GameDoesNotExist   => RoomNotFoundText,
            ErrorCode.GameFull           => RoomFullText,
            ErrorCode.GameClosed         => RoomClosedText,
            ErrorCode.NoRandomMatchFound => NoRandomRoomText,
            ErrorCode.InvalidOperation   => message,
            _                            => RetryLaterText,
        };

        #endregion

        #region 초대 · 친구 요청 — 친구 서버가 없어 인스펙터 메뉴로만 띄운다

        private void HandleInviteAccepted()
        {
            RefreshBadges();
            // 초대한 친구의 방이 실제로는 없다 — 방이 사라졌을 때의 문구로 끝난다 (17 8절)
            modal.ShowWarn(JoinFailedTitle, RoomNotFoundText);
        }

        /// <summary>확인용 — 온라인 친구 한 명이 방으로 초대한다. 초대 알림이 꺼져 있으면 아무것도 뜨지 않는다.</summary>
        [ContextMenu("확인용 — 초대 받기")]
        private void DebugReceiveInvite()
        {
            HubPerson inviter = _data?.PickInviter();
            if (!Application.isPlaying || inviter == null || modal.IsLoading || !ReadSwitch(PlayerPrefsKeys.InviteNoticeOn))
                return;

            invite.Show(inviter.Nickname, InviteSeconds);
            topBar.SetFriendsBadge(_data.RequestsIn.Count + 1, true);
            topBar.ShakeFriends();
        }

        /// <summary>확인용 — 누군가 친구 요청을 보낸다.</summary>
        [ContextMenu("확인용 — 친구 요청 받기")]
        private void DebugReceiveFriendRequest()
        {
            HubPerson request = Application.isPlaying ? _data?.ReceiveRequest() : null;
            if (request == null)
                return;

            topBar.SetFriendsBadge(_data.RequestsIn.Count + (invite.IsShown ? 1 : 0), true);
            friends.Refresh();
            fx.Toast(string.Format(RequestToastFormat, request.Nickname));
        }

        #endregion
    }
}
