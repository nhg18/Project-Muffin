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

        private void Start()
        {
            Debug.Log("Starting game server");
            GameEvents.RaiseOnGameStarted(playerList.ToArray());
        }

        public void RequestDraw()
        {
            Debug.Log("[FakeGameServer] Request Draw");
            GameEvents.RaiseDrawn(CurrentTurnActor, 0, 0); // 카드 종류 흉내는 없다 — 이벤트 흐름만 확인용
        }

        public void RequestDiscard(int cardId)
        {
            Debug.Log($"[FakeGameServer] Request Discard {cardId}");
        }

        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers)
        {
            
        }

        public void RequestSetTrap(int cardInstanceId, int slotIndex)
        {
            
        }

        public void RequestDeclareChapChu()
        {
            
        }

        public void RequestEndTurn()
        {
            Debug.Log("[FakeGameServer] Request End Turn");
            GameEvents.RaiseTurnChanged(CurrentTurnActor);
        }
    }
}
