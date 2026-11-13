using System.Collections.Generic;
using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 판 효과 줄에 값을 넣는다 (프로토타입 v2 「판 전체에 걸린 효과」, 11-card-list 112 · 121 · 123). 순서는 시안대로 — 기다려! → … → 피폭.
    /// 개판 오분전은 시안에서 줄 밖의 자명종이다 — 그 연출 전까지 이 줄의 피폭 앞에 둔다 (사용자 2026-11-13).
    /// 값은 GameScreenPresenter 가 연출 큐 순서로 넘긴다. 늦게 열렸을 때는 Start 에서 방 속성 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(BoardEffectsView))]
    public class BoardEffectsPresenter : MonoBehaviour
    {
        private const string WaitLabel = "기다려!";
        private const string WaitFormat = "{0} 차례까지 승리 없음";
        private const string GaepanLabel = "개판 오분전";
        private const string GaepanFormat = "{0}바퀴 남음";
        private const string FalloutLabel = "피폭";
        private const string FalloutFormat = "턴이 끝날 때마다 모두 −{0}";

        private BoardEffectsView _view;
        private int[] _waitCasters = System.Array.Empty<int>();
        private int _gaepanLeft;   // 0 이면 개판 없음
        private int _falloutN;     // 0 이면 피폭 없음
        private int _falloutTurns; // k — 다음 턴 끝의 피해는 (k+1)^n

        private void Awake()
        {
            _view = GetComponent<BoardEffectsView>();
        }

        private void Start()
        {
            if (PhotonNetwork.InRoom)
            {
                Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
                if (room.TryGetValue(RoomProps.WaitCasters, out object wait) && wait is int[] casters)
                    _waitCasters = casters;
                if (room.TryGetValue(RoomProps.Gaepan, out object gaepan) && gaepan is int[] gp && gp.Length == 2)
                    _gaepanLeft = gp[1];
                if (room.TryGetValue(RoomProps.Fallout, out object fallout) && fallout is int[] values && values.Length == 2)
                {
                    _falloutN = values[0];
                    _falloutTurns = values[1];
                }
            }

            Refresh();
        }

        /// <summary>「기다려」 를 건 사람들 (OnWaitChanged, 123).</summary>
        public void ShowWait(int[] casters)
        {
            _waitCasters = casters ?? System.Array.Empty<int>();
            Refresh();
        }

        /// <summary>피폭 (OnFalloutChanged, 112) — n 이 0 이면 없음.</summary>
        public void ShowFallout(int n, int turns)
        {
            _falloutN = n;
            _falloutTurns = turns;
            Refresh();
        }

        /// <summary>개판 오분전 남은 바퀴 (OnGaepanChanged, 121) — 0 이면 없음.</summary>
        public void ShowGaepan(int left)
        {
            _gaepanLeft = left;
            Refresh();
        }

        /// <summary>새 판 (OnGameRestarted) — 대소동 외의 판 효과는 모두 처음으로. 피폭은 뒤따르는 OnFalloutChanged 가 넣는다.</summary>
        public void ShowRestarted()
        {
            _waitCasters = System.Array.Empty<int>();
            _gaepanLeft = 0;
            Refresh();
        }

        private void Refresh()
        {
            var effects = new List<(string, string)>();
            foreach (int caster in _waitCasters)
                effects.Add((WaitLabel, string.Format(WaitFormat, PlayerLook.NameOf(caster))));
            if (_gaepanLeft > 0)
                effects.Add((GaepanLabel, string.Format(GaepanFormat, _gaepanLeft)));
            if (_falloutN > 0)
                effects.Add((FalloutLabel, string.Format(FalloutFormat, GameServer.FalloutDamage(_falloutTurns + 1, _falloutN))));
            _view.Set(effects);
        }
    }
}
