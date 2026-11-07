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
    /// 턴 주인은 GameScreenPresenter 가 연출 큐에서 넘기고, 남은 초만 여기서 턴 마감 시각으로부터 매 프레임 계산한다 (09-network.md 8절 — 표시 전용, 판정 없음).
    /// 남은 초는 누구 턴이든 보인다 (2026-11-07 사용자 결정 — 「내 턴에만」은 폐기). 멈춤(체인 · 사망 대기)은 기능 5 · 7 에서.
    /// 퇴장은 RoomEvents.OnPlayerLeft 로 받아 칩을 다시 만든다 — 큐 밖. 늦게 열렸을 때는 Start 에서 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(TurnStripView))]
    public class TurnStripPresenter : MonoBehaviour
    {
        private TurnStripView _view;
        private readonly List<int> _actors = new List<int>(); // 칩 순서 = 좌석 순서
        private int _turnActor = -1;
        private double _turnDeadline = -1; // 서버 시각(PhotonNetwork.Time, 초). 음수면 아직 턴이 없다
        private int _shownSeconds = -1;

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
            _view.SetTimerVisible(false);
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue
                && room.TryGetValue(RoomProps.TurnDeadline, out object deadline) && deadline is double deadlineValue)
                ShowTurn(turnActorValue, deadlineValue);
        }

        private void Update()
        {
            if (_turnDeadline < 0) return;

            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(_turnDeadline - PhotonNetwork.Time)));
            if (seconds == _shownSeconds) return;

            _shownSeconds = seconds;
            _view.SetTimer(seconds, false);
        }

        /// <summary>턴이 바뀌었다 (OnTurnStarted).</summary>
        public void ShowTurn(int actorNumber, double deadline)
        {
            _turnActor = actorNumber;
            _turnDeadline = deadline;
            _shownSeconds = -1;

            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            _view.SetCurrent(_actors.IndexOf(actorNumber));
            _view.SetNow(isMe ? TurnNowState.Me : TurnNowState.Other, PlayerLook.NameOf(actorNumber));
            _view.SetTimerVisible(true);
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
        }

        private void HandlePlayerLeft(Player player) => RebuildChips();
    }
}
