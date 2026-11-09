using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 패널 · 찹츄 버튼에 내 값을 넣는다 (10-ui.md 2-1 「내 패널」 · 「찹츄 버튼」 · 7절). HP · 손패 수 · 내 턴은 GameScreenPresenter 가 연출 큐에서 순서대로 넘긴다.
    /// 남은 초는 턴 순서 띠(TurnStripPresenter)가 센다. 늦게 열렸을 때는 Start 에서 CustomProperties 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(MyPanelView))]
    public class MyPanelPresenter : MonoBehaviour
    {
        private const int ChapchuHandCount = 10; // 07-win-condition.md 4.1 — 선언 조건은 손패 정확히 10장

        [Tooltip("우하단 찹츄 버튼 — 손패 수에 따라 상태가 바뀐다.")]
        [SerializeField] private ChapchuButtonView chapchuButton;

        private MyPanelView _view;

        private void Awake()
        {
            _view = GetComponent<MyPanelView>();

            if (chapchuButton == null)
                Debug.LogError($"[{nameof(MyPanelPresenter)}] {nameof(chapchuButton)} 참조가 비어 있습니다.", this);
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
            _view.SetChapchu(false);
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
        }

        /// <summary>HP — 뼈다귀는 최대 HP(GameServer.MaxHp) 대비 비율.</summary>
        public void ShowHp(int hp) => _view.SetHp(hp, GameServer.MaxHp);

        /// <summary>손패 장수와 그에 따른 찹츄 버튼 상태. 선언 뒤 상태(Declared)는 찹츄 요청 API 가 생기면 (기능 8).</summary>
        public void ShowHandCount(int count)
        {
            _view.SetHandCount(count);
            chapchuButton.SetState(count == ChapchuHandCount ? ChapchuButtonState.Ready : ChapchuButtonState.Unavailable, count, ChapchuHandCount);
        }

        /// <summary>턴이 바뀌었다 — 머리 띠 색 · 상황 문구.</summary>
        public void ShowTurn(bool isMyTurn) => _view.SetMyTurn(isMyTurn);

        private void HandleChapchuClicked() => Debug.LogWarning($"[{nameof(MyPanelPresenter)}] 찹츄 선언 요청은 아직 없다 (기능 8).", this);
    }
}
