using Chapchu.Game;
using Chapchu.Network;
using Chapchu.UI.Popups;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 패널 · 찹츄 버튼에 내 값을 넣는다 (10-ui.md 2-1 「내 패널」 · 「찹츄 버튼」 · 7절). HP · 손패 수 · 내 턴 · 찹츄 상태는 GameScreenPresenter 가 연출 큐에서 순서대로 넘긴다.
    /// 찹츄 버튼 상태(내 턴 · 정확히 10장 · 선언 여부)는 여기 한 곳에서 계산하고, 누르면 선언을 요청한다 — 판정은 방장 (07-win-condition.md 4.1 · 10절).
    /// 남은 초는 턴 순서 띠(TurnStripPresenter)가 센다. 늦게 열렸을 때는 Start 에서 CustomProperties 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(MyPanelView))]
    public class MyPanelPresenter : MonoBehaviour
    {
        private const string NotTenCardsToastFormat = "손패가 정확히 10장일 때 외칠 수 있어요. 지금 {0}장"; // 10-ui.md 「찹츄 버튼 상태」

        [SerializeField] private PunGameServer server;
        [Tooltip("우하단 찹츄 버튼 — 손패 수 · 내 턴 · 찹츄 상태에 따라 모양이 바뀐다.")]
        [SerializeField] private ChapchuButtonView chapchuButton;

        private MyPanelView _view;
        private int _handCount;
        private bool _isMyTurn;
        private bool _isChapchu;

        private void Awake()
        {
            _view = GetComponent<MyPanelView>();

            if (server == null || chapchuButton == null)
                Debug.LogError($"[{nameof(MyPanelPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            chapchuButton.ChapchuClicked += HandleChapchuClicked;
        }

        private void OnDisable()
        {
            chapchuButton.ChapchuClicked -= HandleChapchuClicked;
        }

        private void Start()
        {
            _view.SetColor(PlayerLook.MeColor);
            ShowChapchu(false);
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때 — 빌더가 넣은 자리값 그대로

            Player me = PhotonNetwork.LocalPlayer;
            _view.SetNickname(PlayerLook.NameOf(me));

            // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값. 이벤트가 먼저 왔다면 같은 값이다.
            if (me.CustomProperties.TryGetValue(PlayerProps.Hp, out object hp) && hp is int hpValue)
                ShowHp(hpValue);

            if (me.CustomProperties.TryGetValue(PlayerProps.HandCount, out object handCount) && handCount is int handCountValue)
                ShowHandCount(handCountValue);

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue)
                ShowTurn(turnActorValue == me.ActorNumber);

            if (me.CustomProperties.TryGetValue(PlayerProps.ChapChu, out object chapchu) && chapchu is bool chapchuValue)
                ShowChapchu(chapchuValue);
        }

        /// <summary>HP — 뼈다귀는 최대 HP(GameServer.MaxHp) 대비 비율.</summary>
        public void ShowHp(int hp) => _view.SetHp(hp, GameServer.MaxHp);

        /// <summary>손패 장수 — 찹츄 버튼의 점 · 상태도 같이 바뀐다.</summary>
        public void ShowHandCount(int count)
        {
            _handCount = count;
            _view.SetHandCount(count);
            RefreshChapchuButton();
        }

        /// <summary>턴이 바뀌었다 — 머리 띠 색 · 상황 문구. 찹츄 버튼은 내 턴에만 활성.</summary>
        public void ShowTurn(bool isMyTurn)
        {
            _isMyTurn = isMyTurn;
            _view.SetMyTurn(isMyTurn);
            RefreshChapchuButton();
        }

        /// <summary>내 찹츄 상태 (OnChapchuChanged) — 왕관 · 분홍 머리 띠 · 버튼 「찹츄 완료」. 해제되면 바로 돌아간다.</summary>
        public void ShowChapchu(bool isChapchu)
        {
            _isChapchu = isChapchu;
            _view.SetChapchu(isChapchu);
            RefreshChapchuButton();
        }

        // 활성 = 내 턴 · 정확히 10장 · 아직 선언 안 함 (10-ui.md 「찹츄 버튼 상태」). 표시일 뿐 — 선언할 수 있는지는 방장이 본다.
        private ChapchuButtonState GetChapchuState()
        {
            if (_isChapchu)
                return ChapchuButtonState.Declared;

            return _isMyTurn && _handCount == GameServer.ChapchuHandCount ? ChapchuButtonState.Ready : ChapchuButtonState.Unavailable;
        }

        private void RefreshChapchuButton() => chapchuButton.SetState(GetChapchuState(), _handCount, GameServer.ChapchuHandCount);

        // 활성이면 선언 요청 — 결과는 OnChapchuChanged 또는 거절 토스트로 온다. 비활성이면 요청하지 않고 이유를 토스트로.
        private void HandleChapchuClicked()
        {
            ChapchuButtonState state = GetChapchuState();
            if (state == ChapchuButtonState.Ready)
            {
                server?.RequestDeclareChapchu();
                return;
            }

            if (state != ChapchuButtonState.Unavailable || PopupManager.Instance == null) return;

            PopupManager.Instance.ShowToast(_handCount == GameServer.ChapchuHandCount
                ? RejectText.Get(RejectCode.NotYourTurn) // 10장인데 남의 턴
                : string.Format(NotTenCardsToastFormat, _handCount));
        }
    }
}
