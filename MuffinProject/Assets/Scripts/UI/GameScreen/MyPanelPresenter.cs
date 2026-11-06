using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 패널 뷰에 내 값을 넣는다 (10-ui.md 2-1 「내 패널」). HP · 손패 수 · 내 턴은 GameScreenPresenter 가 연출 큐에서 순서대로 넘기고,
    /// 남은 초만 여기서 턴 마감 시각으로부터 매 프레임 계산한다 (09-network.md 8절 — 표시 전용, 판정 없음).
    /// 늦게 열렸을 때는 Start 에서 CustomProperties 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(MyPanelView))]
    public class MyPanelPresenter : MonoBehaviour
    {
        private const int ChapchuHandCount = 10; // 07-win-condition.md 4.1 — 선언 조건은 손패 정확히 10장
        private const string NicknameFallbackFormat = "P{0}"; // 닉네임이 비었을 때 — 로그의 P{actorNumber} 와 같은 꼴

        private MyPanelView _view;
        private double _turnDeadline = -1; // 서버 시각(PhotonNetwork.Time, 초). 음수면 아직 턴이 없다
        private int _shownSeconds = -1;

        private void Awake()
        {
            _view = GetComponent<MyPanelView>();
        }

        private void OnEnable()
        {
            _view.ChapchuClicked += HandleChapchuClicked;
        }

        private void OnDisable()
        {
            _view.ChapchuClicked -= HandleChapchuClicked;
        }

        private void Start()
        {
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때 — 빌더가 넣은 자리값 그대로

            Player me = PhotonNetwork.LocalPlayer;
            _view.SetNickname(string.IsNullOrEmpty(me.NickName) ? string.Format(NicknameFallbackFormat, me.ActorNumber) : me.NickName);

            // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값. 이벤트가 먼저 왔다면 같은 값이다.
            if (me.CustomProperties.TryGetValue(PlayerProps.Hp, out object hp) && hp is int hpValue)
                ShowHp(hpValue);

            if (me.CustomProperties.TryGetValue(PlayerProps.HandCount, out object handCount) && handCount is int handCountValue)
                ShowHandCount(handCountValue);

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue
                && room.TryGetValue(RoomProps.TurnDeadline, out object deadline) && deadline is double deadlineValue)
                ShowTurn(turnActorValue == me.ActorNumber, deadlineValue);
        }

        private void Update()
        {
            if (_turnDeadline < 0) return;

            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(_turnDeadline - PhotonNetwork.Time)));
            if (seconds == _shownSeconds) return;

            _shownSeconds = seconds;
            _view.SetRemainingSeconds(seconds);
        }

        /// <summary>HP — 바는 최대 HP(GameServer.MaxHp) 대비 비율.</summary>
        public void ShowHp(int hp) => _view.SetHp(hp, GameServer.MaxHp);

        /// <summary>손패 장수와 그에 따른 찹츄 버튼 상태. 선언 뒤 상태(Declared)는 찹츄 요청 API 가 생기면 (기능 8).</summary>
        public void ShowHandCount(int count)
        {
            _view.SetHandCount(count, ChapchuHandCount);
            _view.SetChapchu(count == ChapchuHandCount ? ChapchuButtonState.Ready : ChapchuButtonState.Unavailable, count, ChapchuHandCount);
        }

        /// <summary>턴이 바뀌었다. 남은 초는 내 턴에만 센다 — 남의 턴엔 숨긴다 (03-turn.md 7절, 2026-11-03 사용자 결정).</summary>
        public void ShowTurn(bool isMyTurn, double deadline)
        {
            _view.SetMyTurn(isMyTurn);
            _turnDeadline = isMyTurn ? deadline : -1;
            _shownSeconds = -1;
        }

        private void HandleChapchuClicked() => Debug.LogWarning($"[{nameof(MyPanelPresenter)}] 찹츄 선언 요청은 아직 없다 (기능 8).", this);
    }
}
