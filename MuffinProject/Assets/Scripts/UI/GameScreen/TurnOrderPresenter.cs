using System.Collections.Generic;
using System.Linq;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 순서에 값을 넣는다 (통합 화면 UI v3 .tord · 03-turn.md 6 · 7절). 순서는 방장이 섞은 실제 턴 순서(방 속성 turnOrder, PlayerLook.TurnOrderActors) — 봇 포함. 나는 「나」.
    /// 얼굴은 그 사람의 캐릭터 모습(OpponentSeatsPresenter.LookOf — 봇은 봇마다 다르다). 턴 · HP · 찹츄 · 생사 · 방향은 GameScreenPresenter 가 연출 큐에서 넘긴다.
    /// 순서가 오거나(시작 · 대소동 새 판) 누가 나가면 줄을 다시 만든다 — 큐 밖. 늦게 열렸을 때는 Start 에서 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(TurnOrderView))]
    public class TurnOrderPresenter : MonoBehaviour
    {
        private const string MeName = "나";

        [Tooltip("사람마다의 캐릭터 모습을 주는 곳 — 얼굴 원이 이 그림을 쓴다.")]
        [SerializeField] private OpponentSeatsPresenter seats;

        private TurnOrderView _view;
        private readonly List<int> _actors = new List<int>(); // 줄 순서 = 턴 순서
        private readonly HashSet<int> _chapchuActors = new HashSet<int>();
        private readonly Dictionary<int, int> _hp = new Dictionary<int, int>();
        private readonly Dictionary<int, LifeState> _life = new Dictionary<int, LifeState>();
        private int _turnActor = -1;
        private bool _reversed;

        private void Awake()
        {
            _view = GetComponent<TurnOrderView>();
            if (seats == null)
                Debug.LogError($"[{nameof(TurnOrderPresenter)}] {nameof(seats)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
            RoomEvents.OnRoomPropsChanged += HandleRoomPropsChanged;
        }

        private void OnDisable()
        {
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
            RoomEvents.OnRoomPropsChanged -= HandleRoomPropsChanged;
        }

        private void Start()
        {
            _view.SetReversed(false);
            RebuildRows();
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때

            // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값. 봇의 값은 방 속성에 있다 (18-bot.md 9절).
            foreach (int actor in _actors)
            {
                if (BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object hp) && hp is int hpValue)
                    ShowHp(actor, hpValue);
                if (BotRoster.TryGetPublic(actor, PlayerProps.ChapChu, out object chapchu) && chapchu is bool chapchuValue)
                    ShowChapchu(actor, chapchuValue);
                if (BotRoster.TryGetPublic(actor, PlayerProps.LifeState, out object life) && life is byte lifeValue)
                    ShowLife(actor, (LifeState)lifeValue);
            }

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue)
                ShowTurn(turnActorValue);
            ShowDirection(room.TryGetValue(RoomProps.TurnDirection, out object reversed) && reversed is bool reversedValue && reversedValue);
        }

        /// <summary>턴이 바뀌었다 (OnTurnStarted).</summary>
        public void ShowTurn(int actorNumber)
        {
            _turnActor = actorNumber;
            _view.SetCurrent(_actors.IndexOf(actorNumber));
        }

        /// <summary>턴 진행 방향 (OnTurnDirectionChanged) — 역방향이면 알약 · 화살표 · 꺾쇠가 뒤집힌다.</summary>
        public void ShowDirection(bool reversed)
        {
            _reversed = reversed;
            _view.SetReversed(reversed);
        }

        /// <summary>
        /// 「내 턴이 아니에요」 안내 (10-ui.md 8-2) — 지금 턴인 사람의 이름과, 그 사람 다음부터 내 앞까지 남은 사람 수(사망은 건너뛴다).
        /// 지금 턴인 줄을 톡 튀운다. 지금 턴을 모르거나 내 턴이면 false.
        /// </summary>
        public bool TryDescribeWait(int me, out string turnName, out int before)
        {
            turnName = null;
            before = 0;
            int current = _actors.IndexOf(_turnActor);
            int mine = _actors.IndexOf(me);
            if (current < 0 || mine < 0 || current == mine) return false;

            int step = _reversed ? -1 : 1;
            int count = _actors.Count;
            for (int i = (current + step + count) % count; i != mine; i = (i + step + count) % count)
            {
                if (!_life.TryGetValue(_actors[i], out LifeState life) || life != LifeState.Dead)
                    before++;
            }

            turnName = PlayerLook.NameOf(_turnActor);
            _view.Ping(current);
            return true;
        }

        /// <summary>찹츄 상태 (OnChapchuChanged) — 지금 턴 꼬리표의 형광펜이 분홍 + 왕관.</summary>
        public void ShowChapchu(int actorNumber, bool isChapchu)
        {
            if (isChapchu)
                _chapchuActors.Add(actorNumber);
            else
                _chapchuActors.Remove(actorNumber);

            _view.SetChapchu(_actors.IndexOf(actorNumber), isChapchu);
        }

        /// <summary>HP (OnHpChanged) — 얼굴 원의 고리.</summary>
        public void ShowHp(int actorNumber, int hp)
        {
            _hp[actorNumber] = Mathf.Clamp(hp, 0, GameServer.MaxHp); // 방 속성은 남이 쓸 수 있다
            RefreshHp(actorNumber);
        }

        /// <summary>생사 (OnDeathAvoided · OnPlayerDied) — 사망이면 흐리게.</summary>
        public void ShowLife(int actorNumber, LifeState life)
        {
            _life[actorNumber] = life;
            int index = _actors.IndexOf(actorNumber);
            _view.SetDead(index, life == LifeState.Dead);
            RefreshHp(actorNumber);
        }

        private void RefreshHp(int actorNumber)
        {
            bool alive = !_life.TryGetValue(actorNumber, out LifeState life) || life == LifeState.Alive;
            int hp = _hp.TryGetValue(actorNumber, out int value) ? value : GameServer.MaxHp;
            _view.SetHp(_actors.IndexOf(actorNumber), alive ? (float)hp / GameServer.MaxHp : 0f);
        }

        private void RebuildRows()
        {
            _actors.Clear();
            _actors.AddRange(PlayerLook.TurnOrderActors());
            int me = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

            var players = new List<(string, Sprite, Sprite)>();
            foreach (int actor in _actors)
            {
                OpponentSeatsPresenter.CharacterLook look = seats.LookOf(actor);
                players.Add((actor == me ? MeName : PlayerLook.NameOf(actor), look.body, look.accessory));
            }

            _view.SetPlayers(players);
            for (int i = 0; i < _actors.Count; i++)
            {
                int actor = _actors[i];
                RefreshHp(actor);
                _view.SetDead(i, _life.TryGetValue(actor, out LifeState life) && life == LifeState.Dead);
                _view.SetChapchu(i, _chapchuActors.Contains(actor));
            }
            _view.SetCurrent(_actors.IndexOf(_turnActor));
        }

        // 턴 순서가 왔다 (게임 시작 · 대소동 새 판) — 좌석 순서로 그려 둔 줄을 실제 순서로
        private void HandleRoomPropsChanged(Hashtable changed)
        {
            if (changed.ContainsKey(RoomProps.TurnOrder))
                RebuildRows();
        }

        private void HandlePlayerLeft(Player player)
        {
            _life.Remove(player.ActorNumber);
            _hp.Remove(player.ActorNumber);
            _chapchuActors.Remove(player.ActorNumber);
            RebuildRows();
        }
    }
}
