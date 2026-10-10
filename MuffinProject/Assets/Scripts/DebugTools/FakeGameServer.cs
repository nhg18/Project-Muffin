using System;
using System.Collections;
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
        [SerializeField]
        private List<int> playerList = new() { 0, 1, 2, 3 }; // playerActors
        
        [field: SerializeField]
        public int CurrentTurnActor { get; private set; } = -1;

        // 드로우마다 1씩 올려 카드를 구분한다. 진짜 서버는 덱 생성 때 부여한다 (09-network.md 10절).
        private int _nextCardInstanceId = 1;

        private void Start()
        {
            GameEvents.RaiseOnGameStarted(playerList.ToArray());
        }

        public void RequestDraw()
        {
            GameEvents.RaiseMyDrawn(_nextCardInstanceId++, 0); // 카드 종류 흉내는 없다 — 이벤트 흐름만 확인용. 가짜 서버는 늘 나에게 준다
        }

        public void RequestDiscard(int cardId)
        {
        }

        // 검사 없이 바로 승인한 것으로 흉내 낸다. 거절 흐름은 진짜 서버(TempGameScene)로 본다.
        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers)
        {
            GameEvents.RaiseCardUsed(CurrentTurnActor, cardInstanceId, 0, targetActorNumbers);
        }

        public void RequestSetTrap(int cardInstanceId, int slotIndex)
        {
            
        }

        // 검사 없이 바로 선언된 것으로 흉내 낸다. 턴 · 10장 검사는 진짜 서버(TempGameScene)로 본다.
        public void RequestDeclareChapChu()
        {
            GameEvents.RaiseChapChuChanged(CurrentTurnActor, true);
        }

        /// <summary>손패 흉내는 없다 — 항상 0. 이벤트 흐름만 확인용.</summary>
        public int GetCurrentHandCount(int actorNumber) => 0;
    }
}
