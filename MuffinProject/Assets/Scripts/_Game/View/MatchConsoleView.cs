using System.Collections.Generic;
using Chapchu.Game.Core;
using UnityEngine;

namespace Chapchu.Game.View
{
    /// <summary>
    /// 확인용 화면. 결과를 Console 에 찍고, D 키로 드로우를 요청한다.
    /// 전달 계층(IMatchTransport)만 알아서 멀티 · 로컬 어디서나 그대로 쓴다. 실제 화면이 붙으면 지운다.
    /// </summary>
    public class MatchConsoleView : MonoBehaviour, IMatchEvents
    {
        [SerializeField] private MonoBehaviour transport; // IMatchTransport 를 구현한 컴포넌트

        private IMatchTransport _transport;
        private readonly List<int> _myHand = new List<int>();   // 내 손패의 장 번호 (확인용)
        private readonly List<int> _players = new List<int>();

        private void Awake()
        {
            _transport = transport as IMatchTransport;
            if (_transport == null)
                Debug.LogError("[Match] transport 칸에 IMatchTransport 구현 컴포넌트를 넣어야 한다", this);
        }

        private void Start() => _transport?.AddListener(this);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D))
                _transport?.Draw();

            // 확인용: 내 손패 첫 장을 나 말고 첫 번째 플레이어에게 쓴다. 대상을 고르지 않는 카드면 방장이 대상을 정한다.
            if (Input.GetKeyDown(KeyCode.A) && _myHand.Count > 0)
            {
                int target = _players.Find(id => id != _transport.LocalPlayerId);
                _transport.PlayCard(_myHand[0], new[] { target });
            }
        }

        public void OnGameStarted(IReadOnlyList<int> turnOrder)
        {
            _players.Clear();
            _players.AddRange(turnOrder);
            Log($"시작 — 턴 순서 {string.Join(", ", turnOrder)} / 나 {_transport.LocalPlayerId}");
        }

        public void OnTurnChanged(int playerId, double deadline) =>
            Log($"턴 — {Who(playerId)} (마감 {deadline:F1})");

        public void OnHpChanged(int playerId, int hp) => Log($"HP — {Who(playerId)} {hp}");

        public void OnHandCountChanged(int playerId, int handCount) => Log($"손패 — {Who(playerId)} {handCount}장");

        public void OnPileCountChanged(int drawCount, int discardCount) => Log($"더미 — 덱 {drawCount} / 버림 {discardCount}");

        public void OnCardDrawn(int playerId, int instanceId, int cardId)
        {
            _myHand.Add(instanceId);
            Log($"드로우 — {Who(playerId)} #{instanceId} (카드 {cardId})");
        }

        public void OnCardPlayed(int playerId, int instanceId, int cardId, IReadOnlyList<int> targets)
        {
            _myHand.Remove(instanceId);
            Log($"사용 — {Who(playerId)} #{instanceId} (카드 {cardId}) → 대상 [{string.Join(", ", targets)}]");
        }

        public void OnReactionStarted(double deadline) => Log($"반응 대기 — 마감 {deadline:F1}");

        public void OnCardResolved(int instanceId, bool isNegated) => Log($"처리 — #{instanceId}{(isNegated ? " (무효화)" : "")}");

        public void OnRejected(int playerId, int code) => Log($"거절 — {Who(playerId)} 코드 {code}");

        private string Who(int playerId) => playerId == _transport.LocalPlayerId ? $"{playerId}(나)" : playerId.ToString();

        private static void Log(string message) => Debug.Log("[Match] " + message);
    }
}
