using System.Collections.Generic;
using Chapchu.Game;
using UnityEngine;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 로컬 모의 마스터. Photon 없이 에디터 1개로 4인 상황을 흉내 낸다 (plan-b-ui.md B1-1).
    /// <see cref="IGameRequests"/> 를 받아 <see cref="GameEvents"/> 를 올리는 것이 전부다.
    /// 규칙 판정은 흉내만 낸다 — 진짜 규칙을 여기 구현하지 않는다. 규칙이 두 벌이 되면 반드시 갈라진다.
    /// A 의 실제 구현체가 머지되면 씬의 server 참조만 교체한다 (동기화 지점 S2).
    /// </summary>
    public class FakeGameServer : MonoBehaviour, IGameRequests, IGameState
    {
        // 게임 규칙 수치는 GameStatus(코드) 가 원본이다. 여기 흩어두지 않는다.
        [Header("모의 플레이어")]
        [SerializeField] private int playerCount = 4;
        [SerializeField] private int localActorNumber = 1;
        [Tooltip("켜져 있으면 Start 에서 시작 상태(HP · 손패 장수 · 첫 턴)를 전파한다.")]
        [SerializeField] private bool startOnPlay = true;

        private readonly List<int> _actors = new List<int>();
        private int _turnIndex = -1;

        public int CurrentTurnActor => _turnIndex < 0 ? -1 : _actors[_turnIndex];

        private void Start()
        {
            for (int i = 1; i <= playerCount; i++)
                _actors.Add(i);

            if (startOnPlay)
                StartGame();
        }

        /// <summary>01-game-flow.md 3절 시작 시퀀스의 결과만 흉내 낸다. 셔플 · 배분은 하지 않는다.</summary>
        public void StartGame()
        {
            foreach (int actor in _actors)
            {
                GameEvents.RaiseHpChanged(actor, GameStatus.Instance != null ? GameStatus.Instance.MaxHp : 100);
                GameEvents.RaiseHandCountChanged(actor, GameStatus.Instance != null ? GameStatus.Instance.StartHandCount : 5);
                GameEvents.RaiseLifeStateChanged(actor, LifeState.Alive);
            }

            _turnIndex = 0;
            GameEvents.RaiseTurnChanged(CurrentTurnActor);
        }

        public void RequestEndTurn()
        {
            if (CurrentTurnActor != localActorNumber)
            {
                GameEvents.RaiseRequestRejected(localActorNumber, "내 턴이 아닙니다.");
                return;
            }

            _turnIndex = (_turnIndex + 1) % _actors.Count;
            GameEvents.RaiseTurnChanged(CurrentTurnActor);
        }

        // 아래 요청은 아직 흉내 내지 않는다. 필요한 화면을 만들 때 이벤트 발행만 추가한다.
        public void RequestDraw() => Reject(nameof(RequestDraw));
        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers) => Reject(nameof(RequestPlayCard));
        public void RequestSetTrap(int cardInstanceId, int slotIndex) => Reject(nameof(RequestSetTrap));
        public void RequestDeclareChapChu() => Reject(nameof(RequestDeclareChapChu));

        private void Reject(string request)
        {
            Debug.Log($"[{nameof(FakeGameServer)}] {request} — 모의 서버가 아직 흉내 내지 않는 요청");
            GameEvents.RaiseRequestRejected(localActorNumber, $"{request} 미구현 (모의 서버)");
        }

#if UNITY_EDITOR
        // 동작 확인용: Play 중 Hierarchy 의 FakeGameServer 선택 → 인스펙터 컴포넌트 ⋮ 메뉴
        [ContextMenu("Debug/다른 플레이어 턴 넘기기 (내 턴이 될 때까지)")]
        private void DebugAdvanceToLocalTurn()
        {
            if (_turnIndex < 0) return;
            do
            {
                _turnIndex = (_turnIndex + 1) % _actors.Count;
            } while (CurrentTurnActor != localActorNumber);
            GameEvents.RaiseTurnChanged(CurrentTurnActor);
        }
#endif
    }
}
