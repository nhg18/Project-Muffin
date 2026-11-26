using System.Collections.Generic;
using System.Linq;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 순서에 값을 넣는다 (통합 화면 UI v3 .tord · 03-turn.md 7절). 순서는 좌석 순서(PlayerLook.OrderedActors)와 같다 — 봇 포함. 나는 「나」.
    /// 얼굴은 그 사람이 앉은 자리의 캐릭터 그림을 그대로 쓴다 (나 = 내 캐릭터, 상대 = 좌석 1 · 2 · 3). 턴 · HP · 찹츄 · 생사 · 방향은 GameScreenPresenter 가 연출 큐에서 넘긴다.
    /// 퇴장은 RoomEvents.OnPlayerLeft 로 받아 줄을 다시 만든다 — 큐 밖. 늦게 열렸을 때는 Start 에서 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(TurnOrderView))]
    public class TurnOrderPresenter : MonoBehaviour
    {
        private const string MeName = "나";

        [Tooltip("자리마다 캐릭터 그림 — 0 = 나, 1 · 2 · 3 = 상대 좌석 인덱스. 얼굴 원이 이 그림을 쓴다.")]
        [SerializeField] private Image[] characters = new Image[OpponentSeatsPresenter.SeatCount + 1];

        private TurnOrderView _view;
        private readonly List<int> _actors = new List<int>(); // 줄 순서 = 좌석 순서
        private readonly HashSet<int> _chapchuActors = new HashSet<int>();
        private readonly Dictionary<int, int> _hp = new Dictionary<int, int>();
        private readonly Dictionary<int, LifeState> _life = new Dictionary<int, LifeState>();
        private int _turnActor = -1;

        private void Awake()
        {
            _view = GetComponent<TurnOrderView>();
            if (characters == null || characters.Length != OpponentSeatsPresenter.SeatCount + 1 || characters.Any(image => image == null))
                Debug.LogError($"[{nameof(TurnOrderPresenter)}] {nameof(characters)} 는 나 + 좌석 {OpponentSeatsPresenter.SeatCount}개가 모두 연결돼야 합니다.", this);
        }

        private void OnEnable() => RoomEvents.OnPlayerLeft += HandlePlayerLeft;

        private void OnDisable() => RoomEvents.OnPlayerLeft -= HandlePlayerLeft;

        private void Start()
        {
            _view.SetReversed(false);
            _view.SetHalted(false);
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
        public void ShowDirection(bool reversed) => _view.SetReversed(reversed);

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

        /// <summary>생사 (OnDeathPending · OnDeathAvoided · OnPlayerDied) — 사망이면 흐리게, 누구든 사망 대기면 알약에 멈춤.</summary>
        public void ShowLife(int actorNumber, LifeState life)
        {
            _life[actorNumber] = life;
            int index = _actors.IndexOf(actorNumber);
            _view.SetDead(index, life == LifeState.Dead);
            RefreshHp(actorNumber);
            _view.SetHalted(_life.Values.Any(state => state == LifeState.DeathPending));
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
            _actors.AddRange(PlayerLook.OrderedActors());
            int[] seatIndices = OpponentSeatsPresenter.GetOpponentSeatIndices(_actors.Count - 1);

            var players = new List<(string, Sprite)>();
            for (int i = 0; i < _actors.Count; i++)
            {
                int seat = i == 0 ? 0 : i - 1 < seatIndices.Length ? seatIndices[i - 1] : -1;
                Sprite face = seat >= 0 && seat < characters.Length && characters[seat] != null ? characters[seat].sprite : null;
                players.Add((i == 0 ? MeName : PlayerLook.NameOf(_actors[i]), face));
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

        private void HandlePlayerLeft(Player player)
        {
            _life.Remove(player.ActorNumber);
            _hp.Remove(player.ActorNumber);
            _chapchuActors.Remove(player.ActorNumber);
            RebuildRows();
            _view.SetHalted(_life.Values.Any(state => state == LifeState.DeathPending));
        }
    }
}
