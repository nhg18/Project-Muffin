using System.Linq;
using Chapchu.Core;
using Chapchu.Game;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// <see cref="GameServer"/> 의 Photon 전송 층. 규칙(검증 · 계산)은 쓰지 않는다.
    /// 요청: IGameRequests → RPC_Request* → (방장) GameServer.
    /// 결과: GameServer → <see cref="IServerOutbox"/> → CustomProperties · RPC_Reject* → (각 클라) GameEvents.
    /// </summary>
    // ── 요청 하나 추가하는 법 (예: 카드 뽑기) ──
    // 1. 보내기 (IGameRequests 구현):
    //        public void RequestDraw() => photonView.RPC(nameof(RPC_RequestDraw), RpcTarget.MasterClient);
    // 2. 받기 (방장). 요청자는 인자로 받지 말고 info.Sender 를 쓴다:
    //        [PunRPC] private void RPC_RequestDraw(PhotonMessageInfo info)
    //        {
    //            if (!PhotonNetwork.IsMasterClient) return;
    //            _server.Draw(info.Sender.ActorNumber);
    //        }
    // 3. 결과 받기 → GameEvents:
    //    · 모두 보는 값(턴 · HP · 장수): OnRoomPropertiesUpdate / OnPlayerPropertiesUpdate 에 한 줄
    //        if (changedProps.TryGetValue(RoomProps.DeckCount, out object d)) GameEvents.RaiseDeckCountChanged((int)d);
    //    · 한 사람만 보는 값(뽑은 카드 · 함정): IServerOutbox 에 메서드 추가 → 여기서 구현(대상 지정 RPC) → RPC_OnX 에서 Raise
    //        void IServerOutbox.SendDrawn(int actor, int id, int cardId)
    //            => photonView.RPC(nameof(RPC_OnDrawn), PhotonNetwork.CurrentRoom.GetPlayer(actor), id, cardId);
    //        [PunRPC] private void RPC_OnDrawn(int id, int cardId)
    //            => GameEvents.RaiseDrawn(PhotonNetwork.LocalPlayer.ActorNumber, cardId);
    // 규칙(검증 · 계산)은 여기 쓰지 않는다. GameServer 에만.
    [RequireComponent(typeof(PhotonView))]
    public class PunGameServer : MonoBehaviourPunCallbacks, IGameRequests, IGameState, IServerOutbox
    {
        // 모든 클라가 만들지만 방장에서만 쓰인다.
        private GameServer _server;

        public int CurrentTurnActor
        {
            get
            {
                if (!PhotonNetwork.InRoom) return -1;
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                return props.TryGetValue(RoomProps.TurnActor, out object actor) ? (int)actor : -1;
            }
        }

        private void Awake()
        {
            _server = new GameServer(this);
        }

        // 이 씬은 방에서 LoadLevel 로 넘어오므로(Room → Game / TmpGameScene) 방장이 바로 시작한다.
        private void Start()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _server.StartGame(PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToArray());
        }

        #region IGameRequests (UI → 방장)
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

        public void RequestEndTurn() => photonView.RPC(nameof(RPC_RequestEndTurn), RpcTarget.MasterClient);
        #endregion

        #region 방장 — 요청 받기 (요청자 = info.Sender)
        [PunRPC]
        private void RPC_RequestEndTurn(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _server.EndTurn(info.Sender.ActorNumber);
        }
        #endregion

        #region IServerOutbox (방장 → 클라). UI 가 부르지 못하게 명시적 구현.
        void IServerOutbox.SetRoomState(string key, object value)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [key] = value });
        }

        void IServerOutbox.SetPlayerState(int actorNumber, string key, object value)
        {
            PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)?.SetCustomProperties(new Hashtable { [key] = value });
        }

        void IServerOutbox.Reject(int actorNumber, string reason)
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null) return;
            photonView.RPC(nameof(RPC_RejectRequest), player, reason);
        }
        #endregion

        #region 각 클라 — 결과 받기 → GameEvents
        public override void OnRoomPropertiesUpdate(Hashtable changedProps)
        {
            if (changedProps.TryGetValue(RoomProps.TurnActor, out object actor))
                GameEvents.RaiseTurnChanged((int)actor);
        }

        [PunRPC]
        private void RPC_RejectRequest(string reason)
        {
            GameEvents.RaiseRequestRejected(PhotonNetwork.LocalPlayer.ActorNumber, reason);
        }
        #endregion
    }
}
