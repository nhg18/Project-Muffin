using System.Collections.Generic;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 순서 띠에 값을 넣는다 (10-ui.md 2-1 HUD v2 「턴 순서 띠」 · 03-turn.md 7절). 순서는 좌석 순서(PlayerLook.OrderedPlayers)와 같다.
    /// 턴 주인은 GameScreenPresenter 가 연출 큐에서 넘긴다. 남은 초는 여기 없다 — 캐릭터 옆 스톱워치(TurnTimerPresenter, 2026-11-08). 멈춤(체인 · 사망 대기)은 기능 5 · 7 에서.
    /// 퇴장은 RoomEvents.OnPlayerLeft 로 받아 칩을 다시 만든다 — 큐 밖. 늦게 열렸을 때는 Start 에서 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(TurnStripView))]
    public class TurnStripPresenter : MonoBehaviour
    {
        private TurnStripView _view;
        private readonly List<int> _actors = new List<int>(); // 칩 순서 = 좌석 순서
        private int _turnActor = -1;
        private readonly HashSet<int> _chapchuActors = new HashSet<int>(); // 칩을 다시 만들 때 분홍 테를 되살린다

        private void Awake()
        {
            _view = GetComponent<TurnStripView>();
        }

        private void OnEnable()
        {
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
        }

        private void Start()
        {
            RebuildChips();
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때

            foreach (Player player in PhotonNetwork.PlayerList)
                if (player.CustomProperties.TryGetValue(PlayerProps.ChapChu, out object chapchu) && chapchu is bool chapchuValue && chapchuValue)
                    ShowChapchu(player.ActorNumber, true);

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue)
                ShowTurn(turnActorValue);
            ShowDirection(room.TryGetValue(RoomProps.TurnDirection, out object reversed) && reversed is bool reversedValue && reversedValue);
        }

        /// <summary>턴이 바뀌었다 (OnTurnStarted).</summary>
        public void ShowTurn(int actorNumber)
        {
            _turnActor = actorNumber;

            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            _view.SetCurrent(_actors.IndexOf(actorNumber));
            _view.SetNow(isMe ? TurnNowState.Me : TurnNowState.Other, PlayerLook.NameOf(actorNumber));
        }

        /// <summary>턴 진행 방향이 바뀌었다 (OnTurnDirectionChanged) — 역방향이면 화살표를 뒤집는다.</summary>
        public void ShowDirection(bool reversed) => _view.SetReversed(reversed);

        /// <summary>찹츄 상태가 바뀌었다 (OnChapchuChanged) — 그 사람 칩에 분홍 테.</summary>
        public void ShowChapchu(int actorNumber, bool isChapchu)
        {
            if (isChapchu)
                _chapchuActors.Add(actorNumber);
            else
                _chapchuActors.Remove(actorNumber);

            _view.SetChapchu(_actors.IndexOf(actorNumber), isChapchu);
        }

        private void RebuildChips()
        {
            _actors.Clear();
            var players = new List<(string, Color)>();
            foreach (Player player in PlayerLook.OrderedPlayers())
            {
                _actors.Add(player.ActorNumber);
                players.Add((PlayerLook.NameOf(player), PlayerLook.ColorOf(player.ActorNumber)));
            }

            _view.SetPlayers(players);
            _view.SetCurrent(_actors.IndexOf(_turnActor));
            for (int i = 0; i < _actors.Count; i++)
                _view.SetChapchu(i, _chapchuActors.Contains(_actors[i]));
        }

        private void HandlePlayerLeft(Player player) => RebuildChips();
    }
}
