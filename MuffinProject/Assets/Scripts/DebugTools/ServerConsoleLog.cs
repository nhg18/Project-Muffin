using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Presentation;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 방장 콘솔 로그. GameServer 의 출구(IServerOutbox)를 감싸서, 방장이 내보내는 결과를 읽기 좋게 찍고 그대로 넘긴다.
    /// 서버는 방장에서만 돌므로 방장 콘솔 하나로 판 전체(남의 손패 · 거절 포함)를 본다.
    /// 장수 · 체력 같은 공개값은 줄마다 찍지 않고, 턴이 바뀔 때 상태 요약 한 줄로 보여 준다.
    /// </summary>
    public class ServerConsoleLog : IServerOutbox
    {
        private readonly IServerOutbox _inner;
        private readonly Func<GameServer> _server; // 턴 요약용. GameServer 가 이 객체를 받아 만들어지므로 나중에 읽는다
        private int _turnCount;
        private readonly List<string> _chain = new List<string>(); // 체인 한 줄 표시용 — "카드 1(P1→P2)"

        public ServerConsoleLog(IServerOutbox inner, Func<GameServer> server)
        {
            _inner = inner;
            _server = server;
        }

        public void SetRoomState(string key, object value)
        {
            _inner.SetRoomState(key, value);

            if (key == RoomProps.TurnActor)
            {
                _turnCount++;
                Debug.Log($"<b>── 턴 {_turnCount} · P{value} 차례 ──</b>  {_server().DebugState()}");
            }

            // 카드가 체인에 올라갈 때마다 체인 전체를 한 줄로. 0 은 체인 끝
            if (key == RoomProps.ReactionDeadline)
            {
                double deadline = (double)value;
                if (deadline > 0)
                    Debug.Log($"⛓ 체인: {string.Join(" ← ", _chain)} · 반응 {deadline - PhotonNetwork.Time:0.0}초");
                else
                    _chain.Clear();
            }
        }

        public void SetPlayerState(int actorNumber, string key, object value) => _inner.SetPlayerState(actorNumber, key, value);

        public void Reject(int actorNumber, int code)
        {
            _inner.Reject(actorNumber, code);
            Debug.LogWarning($"✖ P{actorNumber} 거절 — {RejectText.Get(code)} (코드 {code})");
        }

        public void SendDrawnCard(int actorNumber, int cardInstanceId, int cardId)
        {
            _inner.SendDrawnCard(actorNumber, cardInstanceId, cardId);

            // 시작 배분(첫 턴 전)은 첫 턴 요약의 손패로 보여 준다
            if (_turnCount > 0)
                Debug.Log($"+ P{actorNumber} 뽑음 — 카드 {cardId} (#{cardInstanceId})");
        }

        public void SendCardUsed(int actorNumber, int cardInstanceId, int cardId, int[] targetActorNumbers)
        {
            _inner.SendCardUsed(actorNumber, cardInstanceId, cardId, targetActorNumbers);

            // 줄은 이어지는 반응 마감(SetRoomState) 때 체인 전체로 찍는다
            string targets = targetActorNumbers.Length == 0 ? "" : "→" + string.Join(",", targetActorNumbers.Select(a => $"P{a}"));
            _chain.Add($"카드 {cardId}(P{actorNumber}{targets})");
        }

        public void SendCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] affectedActorNumbers, bool negated)
        {
            _inner.SendCardResolved(actorNumber, cardInstanceId, cardId, affectedActorNumbers, negated);

            if (negated)
            {
                Debug.Log($"⊘ 무효 — 카드 {cardId} (P{actorNumber}, #{cardInstanceId})");
                return;
            }

            string affected = affectedActorNumbers.Length == 0 ? "대상 없음" : string.Join(", ", affectedActorNumbers.Select(a => $"P{a}"));
            Debug.Log($"✔ 처리 — 카드 {cardId} (P{actorNumber}, #{cardInstanceId}) → {affected}");
        }

        public void SendDeckRefilled(int deckCount)
        {
            _inner.SendDeckRefilled(deckCount);
            Debug.Log($"↻ 덱 재생성 — 버림 더미를 섞어 {deckCount}장");
        }
    }
}
