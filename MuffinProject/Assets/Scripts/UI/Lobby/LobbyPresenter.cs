using Chapchu.Account;
using Chapchu.Network;
using UnityEngine;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 허브의 뼈대 — 시안 데이터 · 지갑 · 배지 · 뒤로가기 · 초대 배너. 화면 영역마다의 흐름은 옆 파일에 있다:
    /// LobbySectionPresenter(홈 ↔ 꾸미기 · 상점 · 뽑기) · LobbyPopupPresenter(친구 · 프로필 · 설정 · 퀘스트 창) ·
    /// LobbyRoomRequester(방 요청) · LobbyAccountPresenter(닉네임 변경 · 계정 삭제). 서버에 요청을 보내는 것은 뒤의 두 파일뿐이다.
    /// 진짜인 것: 닉네임 · 변경 쿠폰(서버 저장) · 방 만들기 / 참가 / 랜덤 매칭 / 혼자 하기 · 설정 스위치(기기 저장).
    /// 서버가 없어 비어 있는 것: 친구 · 요청 · 차단 · 퀘스트 · 재화(0) · 전적 · 레벨 · 친구 코드. 상점 · 뽑기의 아이템 목록 · 값은 시안 값 (LobbyHubData).
    /// 기준: 「통합 화면 UI v2」 로비(게시본 1794316285-7080) · docs/systems/17-lobby-ui-full.md · 14-lobby-ui.md
    /// </summary>
    public class LobbyPresenter : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private LobbyHomeView home;
        [SerializeField] private LobbyTopBarView topBar;
        [SerializeField] private LobbyInviteBannerView invite;
        [SerializeField] private LobbyFxView fx;

        [Header("뒤로가기가 닫는 것 — 위층부터")]
        [SerializeField] private LobbyGachaResultView gachaResult;
        [SerializeField] private LobbyModalView modal;
        [SerializeField] private LobbyPopupPresenter popups;
        [SerializeField] private LobbySectionPresenter sections;

        /// <summary>이 로비가 쓰는 시안 데이터 — 친구 · 재화 · 퀘스트 · 아이템. 로비의 다른 컴포넌트도 이것 하나를 본다.</summary>
        public LobbyHubData Data { get; private set; }

        private void Awake()
        {
            if (home == null || topBar == null || invite == null || fx == null)
                Debug.LogError($"[{nameof(LobbyPresenter)}] 화면 참조가 비어 있습니다.", this);

            if (gachaResult == null || modal == null || popups == null || sections == null)
                Debug.LogError($"[{nameof(LobbyPresenter)}] 뒤로가기 참조가 비어 있습니다.", this);

            Data = new LobbyHubData();
        }

        private void OnEnable()
        {
            invite.OnAccepted += RefreshBadges;
            invite.OnDeclined += RefreshBadges;
            invite.OnExpired += RefreshBadges;
        }

        private void OnDisable()
        {
            invite.OnAccepted -= RefreshBadges;
            invite.OnDeclined -= RefreshBadges;
            invite.OnExpired -= RefreshBadges;
        }

        private void Start()
        {
            AnalyticsRecorder.RecordScreenViewed(AnalyticsRecorder.ScreenName.Lobby);
            home.SetProfile(NetworkManager.Nickname);
            home.ClearLevel();
            RefreshWallet();
            RefreshBadges();
        }

        private void Update()
        {
            // Android 뒤로가기 (17 3절 #5)
            if (Input.GetKeyDown(KeyCode.Escape))
                Back();

            // 창이 떠 있는 동안 알림은 위쪽 HUD 줄 가운데 — 창 내용을 가리지 않게 (통합 v3.25)
            fx.SetToastOverHud(gachaResult.IsOpen || modal.IsAnyOpen || popups.IsOpen);
        }

        /// <summary>윗줄의 재화 숫자를 데이터에 맞춘다.</summary>
        public void RefreshWallet() => topBar.SetWallet(Data.Balance(HubCurrency.Bone), Data.Balance(HubCurrency.Churu));

        /// <summary>친구 · 퀘스트 버튼의 빨간 숫자를 데이터에 맞춘다.</summary>
        public void RefreshBadges()
        {
            topBar.SetFriendsBadge(Data.RequestsIn.Count + (invite.IsShown ? 1 : 0));
            home.SetQuestBadge(Data.ReadyCount(true) + Data.ReadyCount(false));
        }

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
            else if (!popups.Back())
                sections.GoHome();
        }
    }
}
