using System.Collections.Generic;
using System.Linq;
using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 판 효과 줄에 값을 넣는다 (프로토타입 v2 「판 전체에 걸린 효과」, 11-card-list 1008 · 1012 · 1023 · 1067). 순서는 시안대로 — 기다려! → 영역전개 → GOAT → 피폭.
    /// 개판 오분전(1021)은 이 줄이 아니라 왼쪽 위 자명종이다 (GaepanPresenter, 사용자 2026-11-13).
    /// 값은 GameScreenPresenter 가 연출 큐 순서로 넘긴다. 늦게 열렸을 때는 Start 에서 방 속성 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(BoardEffectsView))]
    public class BoardEffectsPresenter : MonoBehaviour
    {
        private const string WaitLabel = "기다려!";
        private const string WaitFormat = "{0}의 턴까지 승리 없음";
        private const string DomainLabel = "영역전개";
        private const string DomainFormat = "{0}의 턴 시작마다 {1}장 뺏김 · −{2} (남은 {3}번)";
        private const string GoatLabel = "GOAT";
        private const string GoatFormat = "{0} — 주는 피해 2배";
        private const string FalloutLabel = "피폭";
        private const string FalloutFormat = "턴이 끝날 때마다 모두 −{0}";

        private BoardEffectsView _view;
        private int[] _waitCasters = System.Array.Empty<int>();
        private int _domainOwner = -1; // 영역의 주인 — 없으면 -1
        private int _domainLeft;
        private int _domainTake;
        private int _domainDamage;
        private int[] _goatActors = System.Array.Empty<int>();
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
                    _waitCasters = Known(casters);
                if (room.TryGetValue(RoomProps.Domain, out object domain) && domain is int[] d && d.Length == 4 && d[1] > 0 && d[2] >= 0 && d[3] >= 0
                    && PlayerLook.OrderedActors().Contains(d[0])) // 남이 잠깐 바꾼 값은 거른다 (09-network.md 13절 #8)
                {
                    _domainOwner = d[0];
                    _domainLeft = d[1];
                    _domainTake = d[2];
                    _domainDamage = d[3];
                }
                if (room.TryGetValue(RoomProps.Goat, out object goat) && goat is int[] goats)
                    _goatActors = Known(goats);
                if (room.TryGetValue(RoomProps.Fallout, out object fallout) && fallout is int[] values && values.Length == 2
                    && values[0] >= 1 && values[1] >= 0) // 남이 잠깐 바꾼 값은 거른다 (09-network.md 13절 #8)
                {
                    _falloutN = values[0];
                    _falloutTurns = values[1];
                }
            }

            Refresh();
        }

        /// <summary>「기다려」 를 건 사람들 (OnWaitChanged, 1023).</summary>
        public void ShowWait(int[] casters)
        {
            _waitCasters = Known(casters);
            Refresh();
        }

        // 판에 있는 사람만 · 겹치지 않게 — 방 속성은 남이 쓸 수 있어 알약이 끝없이 늘지 않게 (09-network.md 13절)
        private static int[] Known(int[] casters)
        {
            if (casters == null)
                return System.Array.Empty<int>();

            List<int> actors = PlayerLook.OrderedActors();
            return casters.Where(actors.Contains).Distinct().ToArray();
        }

        /// <summary>영역 (OnDomainChanged, 1008) — left 가 0 이면 없음.</summary>
        public void ShowDomain(int owner, int left, int take, int damage)
        {
            _domainOwner = left > 0 ? owner : -1;
            _domainLeft = left;
            _domainTake = take;
            _domainDamage = damage;
            Refresh();
        }

        /// <summary>GOAT 인 사람들 (OnGoatChanged, 1067).</summary>
        public void ShowGoat(int[] actors)
        {
            _goatActors = Known(actors);
            Refresh();
        }

        /// <summary>피폭 (OnFalloutChanged, 1012) — n 이 0 이면 없음.</summary>
        public void ShowFallout(int n, int turns)
        {
            _falloutN = n;
            _falloutTurns = turns;
            Refresh();
        }

        /// <summary>새 판 (OnGameRestarted) — 대폭발 외의 판 효과는 모두 처음으로. 피폭은 뒤따르는 OnFalloutChanged 가 넣는다.</summary>
        public void ShowRestarted()
        {
            _waitCasters = System.Array.Empty<int>();
            _domainOwner = -1;
            _goatActors = System.Array.Empty<int>();
            Refresh();
        }

        private void Refresh()
        {
            var effects = new List<(string, string)>();
            foreach (int caster in _waitCasters)
                effects.Add((WaitLabel, string.Format(WaitFormat, PlayerLook.NameOf(caster))));
            if (_domainOwner != -1)
                effects.Add((DomainLabel, string.Format(DomainFormat, PlayerLook.NameOf(_domainOwner), _domainTake, _domainDamage, _domainLeft)));
            foreach (int actor in _goatActors)
                effects.Add((GoatLabel, string.Format(GoatFormat, PlayerLook.NameOf(actor))));
            if (_falloutN > 0)
                effects.Add((FalloutLabel, string.Format(FalloutFormat, GameServer.FalloutDamage(_falloutTurns + 1, _falloutN))));
            _view.Set(effects);
        }
    }
}
