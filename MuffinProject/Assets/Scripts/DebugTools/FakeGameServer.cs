using System;
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
        private readonly List<int> _playerList = new() { 0, 1, 2, 3 }; // actorNumber
        
        [field: SerializeField]
        public int CurrentTurnActor { get; private set; } = -1;

        private void Start()
        {
            Debug.Log("Starting game server");
        }

        public void RequestDraw()
        {
            
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
