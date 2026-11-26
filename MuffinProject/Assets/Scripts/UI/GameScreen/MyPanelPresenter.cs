using Chapchu.Game;
using Chapchu.Network;
using Chapchu.UI.Fx;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 HUD 에 내 값을 넣는다 (통합 화면 UI v3 「내 HUD」 · 10-ui.md 7절). HP · 손패 수 · 내 턴 · 찹츄 상태 · 사망은 GameScreenPresenter 가 연출 큐에서 순서대로 넘긴다.
    /// 찹츄 버튼은 없다 — 선언은 방장이 자동으로 하고 (07-win-condition.md 4.1, 2026-11-14), 화면에는 「찹츄까지 n장」 만 보인다.
    /// 남은 초는 TurnTimerPresenter 가 센다. 늦게 열렸을 때는 Start 에서 CustomProperties 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(MyPanelView))]
    public class MyPanelPresenter : MonoBehaviour
    {
        private MyPanelView _view;
        private int _handCount;
        private bool _isChapchu;

        private void Awake()
        {
            _view = GetComponent<MyPanelView>();
        }

        private void Start()
        {
            ShowChapchu(false);
            ShowHandCount(_handCount);
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때 — 빌더가 넣은 자리값 그대로

            Player me = PhotonNetwork.LocalPlayer;
            _view.SetNickname(PlayerLook.NameOf(me));

            // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값. 이벤트가 먼저 왔다면 같은 값이다.
            if (me.CustomProperties.TryGetValue(PlayerProps.Hp, out object hp) && hp is int hpValue)
                ShowHp(Mathf.Clamp(hpValue, 0, GameServer.MaxHp)); // Player 속성은 남이 쓸 수 있다

            if (me.CustomProperties.TryGetValue(PlayerProps.HandCount, out object handCount) && handCount is int handCountValue)
                ShowHandCount(handCountValue);

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue)
                ShowTurn(turnActorValue == me.ActorNumber);

            if (me.CustomProperties.TryGetValue(PlayerProps.ChapChu, out object chapchu) && chapchu is bool chapchuValue)
                ShowChapchu(chapchuValue);

            if (me.CustomProperties.TryGetValue(PlayerProps.LifeState, out object life) && life is byte lifeValue && lifeValue != (byte)LifeState.Alive)
                ShowLife((LifeState)lifeValue);
        }

        /// <summary>HP — 막대는 최대 HP(GameServer.MaxHp) 대비 비율.</summary>
        public void ShowHp(int hp) => _view.SetHp(hp, GameServer.MaxHp);

        /// <summary>4 단계 공통 연출 자리 (ResultFx).</summary>
        public ResultFx.Seat FxSeat => _view.FxSeat;

        /// <summary>손패 장수 — 손패 카드 그림 · 「찹츄까지 n장」.</summary>
        public void ShowHandCount(int count)
        {
            _handCount = count;
            _view.SetHandCount(count);
        }

        /// <summary>턴이 바뀌었다 — 「나의 턴」 이름표 · 스톱워치.</summary>
        public void ShowTurn(bool isMyTurn) => _view.SetMyTurn(isMyTurn);

        /// <summary>내 찹츄 상태 (OnChapchuChanged) — 분홍 「찹츄!」 이름표. 손패가 10장이 아니어도 남고, 내 턴 시작 판정에서 무효가 되면 바로 돌아간다 (10-ui.md 7절).</summary>
        public void ShowChapchu(bool isChapchu)
        {
            _isChapchu = isChapchu;
            _view.SetChapchu(isChapchu);
        }

        /// <summary>내 생사 (OnDeathPending · OnDeathAvoided · OnPlayerDied) — HP 자리 「대기」 · 「사망」.</summary>
        public void ShowLife(LifeState life) => _view.SetLife(life);
    }
}
