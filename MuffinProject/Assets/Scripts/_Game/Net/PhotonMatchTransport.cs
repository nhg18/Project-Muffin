using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Cards;
using Chapchu.Game.Core;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Chapchu.Game.Net
{
    /// <summary>
    /// 멀티 전달 계층 (Photon). 방장만 MatchHost 를 들고 판정한다.
    /// 요청은 방장에게 RPC 로 보내고, 방장의 결과는 공개면 전원 · 비공개면 본인에게만 RPC 로 보낸다.
    /// 이 기기 화면은 IMatchTransport 로 요청하고 결과를 받는다 — 화면은 멀티인지 모른다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PhotonMatchTransport : MonoBehaviourPun, IMatchTransport
    {
        [SerializeField] private DeckData deck;

        private readonly List<IMatchEvents> _listeners = new List<IMatchEvents>();
        private MatchHost _host;

        public int LocalPlayerId => PhotonNetwork.LocalPlayer.ActorNumber;

        public void AddListener(IMatchEvents listener) => _listeners.Add(listener);

        public void Draw() => photonView.RPC(nameof(RPC_RequestDraw), RpcTarget.MasterClient);

        // 화면들이 Start 에서 AddListener 하므로, 한 프레임 뒤에 시작해야 방장 자신의 화면도 첫 결과를 받는다.
        private IEnumerator Start()
        {
            if (!PhotonNetwork.IsMasterClient)
                yield break;

            yield return null;

            _host = new MatchHost(new Outbox(this), () => PhotonNetwork.Time);
            int[] playerIds = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToArray();
            _host.Start(playerIds, deck.CreateCards(), UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }

        // 턴 마감 판정은 방장만 한다 (09-network.md 8절). 방장이 아니면 _host 가 없다.
        private void Update() => _host?.Tick();

        // ---------- 클라 → 방장 ----------

        [PunRPC]
        private void RPC_RequestDraw(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _host?.Draw(info.Sender.ActorNumber);
        }

        // ---------- 방장 → 클라 ----------

        [PunRPC] private void RPC_GameStarted(int[] turnOrder) => Notify(l => l.OnGameStarted(turnOrder));
        [PunRPC] private void RPC_TurnChanged(int playerId, double deadline) => Notify(l => l.OnTurnChanged(playerId, deadline));
        [PunRPC] private void RPC_HpChanged(int playerId, int hp) => Notify(l => l.OnHpChanged(playerId, hp));
        [PunRPC] private void RPC_HandCountChanged(int playerId, int handCount) => Notify(l => l.OnHandCountChanged(playerId, handCount));
        [PunRPC] private void RPC_PileCountChanged(int drawCount, int discardCount) => Notify(l => l.OnPileCountChanged(drawCount, discardCount));
        [PunRPC] private void RPC_CardDrawn(int playerId, int instanceId, int cardId) => Notify(l => l.OnCardDrawn(playerId, instanceId, cardId));
        [PunRPC] private void RPC_CardPlayed(int playerId, int instanceId, int cardId, int[] targets) => Notify(l => l.OnCardPlayed(playerId, instanceId, cardId, targets));
        [PunRPC] private void RPC_ReactionStarted(double deadline) => Notify(l => l.OnReactionStarted(deadline));
        [PunRPC] private void RPC_CardResolved(int instanceId, bool isNegated) => Notify(l => l.OnCardResolved(instanceId, isNegated));
        [PunRPC] private void RPC_Rejected(int playerId, int code) => Notify(l => l.OnRejected(playerId, code));

        private void Notify(Action<IMatchEvents> call)
        {
            foreach (IMatchEvents listener in _listeners)
                call(listener);
        }

        // 방장의 MatchHost 결과를 RPC 로 바꾼다. 공개 정보는 전원, 비공개 정보는 그 플레이어에게만 (09-network.md 3절).
        private class Outbox : IMatchEvents
        {
            private readonly PhotonMatchTransport _owner;

            public Outbox(PhotonMatchTransport owner) => _owner = owner;

            public void OnGameStarted(IReadOnlyList<int> turnOrder) => ToAll(nameof(RPC_GameStarted), turnOrder.ToArray());
            public void OnTurnChanged(int playerId, double deadline) => ToAll(nameof(RPC_TurnChanged), playerId, deadline);
            public void OnHpChanged(int playerId, int hp) => ToAll(nameof(RPC_HpChanged), playerId, hp);
            public void OnHandCountChanged(int playerId, int handCount) => ToAll(nameof(RPC_HandCountChanged), playerId, handCount);
            public void OnPileCountChanged(int drawCount, int discardCount) => ToAll(nameof(RPC_PileCountChanged), drawCount, discardCount);
            public void OnCardDrawn(int playerId, int instanceId, int cardId) => ToPlayer(playerId, nameof(RPC_CardDrawn), playerId, instanceId, cardId);
            public void OnCardPlayed(int playerId, int instanceId, int cardId, IReadOnlyList<int> targets) => ToAll(nameof(RPC_CardPlayed), playerId, instanceId, cardId, targets.ToArray());
            public void OnReactionStarted(double deadline) => ToAll(nameof(RPC_ReactionStarted), deadline);
            public void OnCardResolved(int instanceId, bool isNegated) => ToAll(nameof(RPC_CardResolved), instanceId, isNegated);
            public void OnRejected(int playerId, int code) => ToPlayer(playerId, nameof(RPC_Rejected), playerId, code);

            private void ToAll(string rpc, params object[] args) => _owner.photonView.RPC(rpc, RpcTarget.All, args);

            // 나간 플레이어에게는 보낼 곳이 없으므로 건너뛴다.
            private void ToPlayer(int playerId, string rpc, params object[] args)
            {
                Player target = PhotonNetwork.CurrentRoom.GetPlayer(playerId);
                if (target != null)
                    _owner.photonView.RPC(rpc, target, args);
            }
        }
    }
}
