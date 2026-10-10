using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.DebugTools;
using Chapchu.Game;
using Chapchu.Game.Cards;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// <see cref="GameServer"/> 의 Photon 전송 층. 규칙(검증 · 계산)은 쓰지 않는다.
    /// 요청: IGameRequests → RPC_Request* → (방장) GameServer.
    /// 결과: GameServer → <see cref="IServerOutbox"/> → CustomProperties · RPC_Reject* → (각 클라) GameEvents.
    /// </summary>
    // ── 요청 하나 추가하는 법 (예: 함정 설치, 아직 구현 전) ──
    // 1. 보내기 (IGameRequests 구현):
    //        public void RequestSetTrap(int cardInstanceId, int slotIndex) => photonView.RPC(nameof(RPC_RequestSetTrap), RpcTarget.MasterClient, cardInstanceId, slotIndex);
    // 2. 받기 (방장). 요청자는 인자로 받지 말고 info.Sender 를 쓴다:
    //        [PunRPC] private void RPC_RequestSetTrap(int cardInstanceId, int slotIndex, PhotonMessageInfo info)
    //        {
    //            if (!PhotonNetwork.IsMasterClient) return;
    //            _server.SetTrap(info.Sender.ActorNumber, cardInstanceId, slotIndex);
    //        }
    // 3. 결과 받기 → GameEvents:
    //    · 모두 보는 값(턴 · HP · 덱 잔여 · 함정 개수): OnRoomPropertiesUpdate / OnPlayerPropertiesUpdate 에 한 줄
    //        if (changedProps.TryGetValue(RoomProps.DeckCount, out object d)) GameEvents.RaiseDeckCountChanged((int)d);
    //    · 한 사람만 보는 값(뽑은 카드 · 함정 종류): IServerOutbox 에 메서드 추가 → 여기서 구현(대상 지정 RPC) → RPC_OnX 에서 Raise
    //        (실제 예시: SendDrawnCard · RPC_OnDrawn 를 참고)
    // 규칙(검증 · 계산)은 여기 쓰지 않는다. GameServer 에만.
    [RequireComponent(typeof(PhotonView))]
    public class PunGameServer : MonoBehaviourPunCallbacks, IGameRequests, IGameState, IServerOutbox
    {
        // 모든 클라가 만들지만 방장에서만 쓰인다.
        private GameServer _server;

        // 이번 게임의 덱 구성 (05-deck.md 2절 · 8절). 덱 구성은 미정이라 임시 에셋(Deck_Default)을 쓴다.
        [SerializeField] private DeckData startingDeckRecipe;

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
            // 방장 콘솔 로그(ServerConsoleLog)가 서버의 출구를 감싸서 결과를 찍고 그대로 이 객체로 넘긴다.
            _server = new GameServer(new ServerConsoleLog(this, () => _server), () => PhotonNetwork.Time);
        }

        // 이 씬은 방에서 LoadLevel 로 넘어오므로(Room → Game / TempGameScene) 방장이 바로 시작한다.
        private void Start()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToArray();
            List<Card> deck = BuildDeck();
            Debug.Log($"<b>════ 게임 시작 ════</b>  참가자 {string.Join(", ", actors.Select(a => $"P{a}"))} · 덱 {deck.Count}장");

            // 01-game-flow.md 3절 순서: 체력 → 덱 → 5장씩 → 턴 순서
            _server.StartGame(actors);
            _server.InitDeck(deck);
            _server.DealInitialHands(actors);
            _server.StartFirstTurn();
        }

        // 에셋(DeckData · CardData) → 서버용 Card. 게임 안의 카드는 전부 여기서, 방장이, 게임 시작 때 한 번 만든다.
        // 서버 어셈블리는 UnityEngine 을 못 보므로 에셋을 읽는 변환은 이 Unity 층이 한다. 규칙 판정은 아니다.
        // 장 번호(InstanceId)는 1부터 (09-network.md 7절). 효과 목록은 에셋 값을 복사해 넘긴다 — 에셋 리스트를 서버와 공유하지 않는다.
        private List<Card> BuildDeck()
        {
            var deck = new List<Card>();
            int nextInstanceId = 1;

            foreach (DeckData.Entry entry in startingDeckRecipe.entries)
            {
                CardData data = entry.card;
                CardEffect[] effects = data.effects.ToArray();

                for (int i = 0; i < entry.count; i++)
                    deck.Add(new Card(nextInstanceId++, data.id, data.type, data.targetType, effects));
            }

            return deck;
        }

        // 턴 마감 판정은 방장만 한다 (09-network.md 8절).
        private void Update()
        {
            if (PhotonNetwork.IsMasterClient)
                _server.Tick();
        }

        #region IGameRequests (UI → 방장)
        public void RequestDraw() => photonView.RPC(nameof(RPC_RequestDraw), RpcTarget.MasterClient);

        public void RequestDiscard(int cardId) => photonView.RPC(nameof(RPC_RequestDiscard), RpcTarget.MasterClient, cardId);

        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers) => photonView.RPC(nameof(RPC_RequestPlayCard), RpcTarget.MasterClient, cardInstanceId, targetActorNumbers);

        public void RequestSetTrap(int cardInstanceId, int slotIndex)
        {
        }

        public void RequestDeclareChapChu()
        {
        }

        #endregion

        #region 방장 — 요청 받기 (요청자 = info.Sender)
        [PunRPC]
        private void RPC_RequestDraw(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _server.Draw(info.Sender.ActorNumber);
        }

        [PunRPC]
        private void RPC_RequestDiscard(int cardId, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _server.Discard(info.Sender.ActorNumber, cardId);
        }

        [PunRPC]
        private void RPC_RequestPlayCard(int cardInstanceId, int[] targetActorNumbers, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _server.PlayCard(info.Sender.ActorNumber, cardInstanceId, targetActorNumbers);
        }

        // TODO: 나간 사람의 카드 · 플레이어 슬롯 오브젝트 삭제 (02-player.md 6절. 카드 처리는 01-game-flow.md 제안 — 최종 사망과 동일).
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _server.RemoveFromTurnOrder(otherPlayer.ActorNumber);
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

        void IServerOutbox.Reject(int actorNumber, int code)
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null) return;
            photonView.RPC(nameof(RPC_RejectRequest), player, code);
        }

        void IServerOutbox.SendDrawnCard(int actorNumber, int cardInstanceId, int cardId)
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null) return;
            photonView.RPC(nameof(RPC_OnDrawn), player, cardInstanceId, cardId);
        }

        void IServerOutbox.SendCardUsed(int actorNumber, int cardInstanceId, int cardId, int[] targetActorNumbers)
        {
            photonView.RPC(nameof(RPC_OnCardUsed), RpcTarget.All, actorNumber, cardInstanceId, cardId, targetActorNumbers);
        }

        void IServerOutbox.SendDeckRefilled(int deckCount)
        {
            photonView.RPC(nameof(RPC_OnDeckRefilled), RpcTarget.All, deckCount);
        }
        #endregion

        #region 각 클라 — 결과 받기 → GameEvents
        public override void OnRoomPropertiesUpdate(Hashtable changedProps)
        {
            if (changedProps.TryGetValue(RoomProps.TurnActor, out object actor))
                GameEvents.RaiseTurnChanged((int)actor);

            if (changedProps.TryGetValue(RoomProps.TurnDeadline, out object deadline))
                GameEvents.RaiseTurnDeadlineChanged((double)deadline);

            if (changedProps.TryGetValue(RoomProps.DeckCount, out object deckCount))
                GameEvents.RaiseDeckCountChanged((int)deckCount);

            if (changedProps.TryGetValue(RoomProps.DiscardCount, out object discardCount))
                GameEvents.RaiseDiscardCountChanged((int)discardCount);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.TryGetValue(PlayerProps.Hp, out object hp))
                GameEvents.RaiseHpChanged(targetPlayer.ActorNumber, (int)hp);

            if (changedProps.TryGetValue(PlayerProps.HandCount, out object handCount))
                GameEvents.RaiseHandCountChanged(targetPlayer.ActorNumber, (int)handCount);
        }

        [PunRPC]
        private void RPC_RejectRequest(int code)
        {
            GameEvents.RaiseMyRequestRejected(code);
        }

        [PunRPC]
        private void RPC_OnDrawn(int cardInstanceId, int cardId)
        {
            GameEvents.RaiseMyDrawn(cardInstanceId, cardId);
        }

        [PunRPC]
        private void RPC_OnCardUsed(int actorNumber, int cardInstanceId, int cardId, int[] targetActorNumbers)
        {
            GameEvents.RaiseCardUsed(actorNumber, cardInstanceId, cardId, targetActorNumbers);
        }

        [PunRPC]
        private void RPC_OnDeckRefilled(int deckCount)
        {
            GameEvents.RaiseDeckRefilled(deckCount);
        }

        public int GetCurrentHandCount(int actorNumber)
        {
            if (!PhotonNetwork.InRoom) return 0;
            var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            return player != null && player.CustomProperties.TryGetValue(PlayerProps.HandCount, out object count) ? (int)count : 0;
        }
        #endregion
    }
}
