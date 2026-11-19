using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.Network
{
    /// <summary>
    /// 요청 빈도 제한 (09-network.md 13절) — 보낸 사람마다 Burst 개까지 몰아서, 그 뒤엔 초당 PerSecond 개. 넘친 요청은 답 없이 버린다.
    /// 거절도 방장이 답을 보내므로, 요청을 쏟아 방장 · 방 전원의 대역을 채우지 못하게 한다. 요청 RPC 를 받는 곳(PunGameServer · PunResultServer)이 하나씩 갖는다.
    /// </summary>
    internal sealed class RequestLimiter
    {
        private const float Burst = 10f;
        private const float PerSecond = 5f;

        private readonly string _owner; // 경고 로그에 찍을 이름
        private readonly Dictionary<int, (float tokens, float at)> _budget = new Dictionary<int, (float tokens, float at)>();
        private readonly HashSet<int> _throttled = new HashSet<int>(); // 지금 막혀 있는 보낸 사람 — 경고를 한 번만

        public RequestLimiter(string owner) => _owner = owner;

        /// <summary>이 요청을 처리해도 되는지. 이미 나간 사람의 요청도 버린다.</summary>
        public bool Allow(PhotonMessageInfo info)
        {
            if (info.Sender == null)
                return false; // 이미 나간 사람

            float now = Time.unscaledTime;
            int actor = info.Sender.ActorNumber;
            float tokens = _budget.TryGetValue(actor, out (float tokens, float at) budget)
                ? Mathf.Min(Burst, budget.tokens + (now - budget.at) * PerSecond)
                : Burst;

            bool allowed = tokens >= 1f;
            _budget[actor] = (allowed ? tokens - 1f : tokens, now);

            // 경고는 막히기 시작할 때 한 번만 — 쏟아붓는 요청마다 로그를 쓰지 않게
            if (allowed)
                _throttled.Remove(actor);
            else if (_throttled.Add(actor))
                Debug.LogWarning($"[{_owner}] P{actor} 요청이 너무 잦다 — 다시 허용될 때까지 버린다");
            return allowed;
        }
    }
}
