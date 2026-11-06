using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// <see cref="GameServer"/> 의 Photon 전송 층 (09-network.md 4 · 10절). 규칙(검증 · 계산)은 쓰지 않는다.
    /// 요청: Request* → RPC_Request* → (방장) GameServer.
    /// 결과: GameServer 가 돌려준 일어난 일 → 받을 사람에게 RPC_On* (전원 / 주인만) + 지금 값 스냅샷(CustomProperties) → 각 클라 GameEvents.
    /// GameScene 에 하나 둔다. 모든 클라이언트에 있지만 규칙은 방장에서만 돈다.
    /// </summary>
    // ── 요청 하나 추가하는 법 (예: 함정 설치) ──
    // 1. 보내기:  public void RequestSetTrap(int cardInstanceId) => photonView.RPC(nameof(RPC_RequestSetTrap), RpcTarget.MasterClient, cardInstanceId);
    // 2. 받기 (방장). 요청자는 인자로 받지 말고 info.Sender 를 쓴다:
    //        [PunRPC] private void RPC_RequestSetTrap(int cardInstanceId, PhotonMessageInfo info)
    //        {
    //            if (!PhotonNetwork.IsMasterClient) return;
    //            Send(_server.SetTrap(info.Sender.ActorNumber, cardInstanceId));
    //        }
    // 3. 새 종류의 일이면 Send 의 switch 에 case 를 더한다 — 누구에게(전원 / 주인만) 보낼지는 여기서 정한다.
    [RequireComponent(typeof(PhotonView))]
    public class PunGameServer : MonoBehaviourPunCallbacks
    {
        [Tooltip("이번 게임의 덱 구성 (05-deck.md 8절). 덱 구성은 미정이라 임시 에셋(Deck_Default)을 쓴다.")]
        [SerializeField] private DeckData deck;

        [Tooltip("게임이 끝나고 결과를 보여 주는 시간(초). 지나면 방장이 방을 다시 열고 전원을 방 씬으로 옮긴다 (08-room.md 4절).")]
        [Min(0)] [SerializeField] private float resultSeconds = 5f;

        private GameServer _server; // 모든 클라가 만들지만 방장에서만 쓰인다

        /// <summary>이번 게임의 덱 — 카드 ID 로 카드 정의를 찾는 사전으로도 쓴다 (DeckData.GetCard).</summary>
        public DeckData Deck => deck;

        private void Awake()
        {
            if (deck == null)
                Debug.LogError($"[{nameof(PunGameServer)}] {nameof(deck)} 참조가 비어 있습니다.", this);

            _server = new GameServer(new System.Random(), () => PhotonNetwork.Time);
        }

        // 이 씬은 방에서 LoadLevel 로 넘어오므로 방장이 바로 시작한다 (01-game-flow.md 3절).
        private void Start()
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToArray();
            List<Card> cards = deck.CreateCards();
            Debug.Log($"[{nameof(PunGameServer)}] 게임 시작 — 참가자 {string.Join(", ", actors.Select(a => $"P{a}"))} · 덱 {cards.Count}장");

            // 지난 판의 지금 값을 지운다 — 시작 일이 다시 쓰지 않는 값 (늦게 열린 화면이 지난 판 상태를 읽지 않게)
            var reset = new Hashtable { [PlayerProps.LifeState] = (byte)LifeState.Alive, [PlayerProps.ChapChu] = false, [PlayerProps.TrapCount] = 0 };
            foreach (Player player in PhotonNetwork.PlayerList)
                player.SetCustomProperties(reset);

            Send(_server.StartGame(actors, cards));
        }

        // 턴 마감 판정은 방장만 한다 (09-network.md 8절).
        private void Update()
        {
            if (PhotonNetwork.IsMasterClient)
                Send(_server.Tick());
        }

        #region 요청 (각 클라 → 방장)

        /// <summary>카드 1장 뽑기 (메인 행동). 결과는 GameEvents 로 온다.</summary>
        public void RequestDraw() => photonView.RPC(nameof(RPC_RequestDraw), RpcTarget.MasterClient);

        /// <summary>행동 카드 사용. 대상은 고르는 카드일 때만 쓰인다 (자동 대상이면 방장이 무시).</summary>
        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers) => photonView.RPC(nameof(RPC_RequestPlayCard), RpcTarget.MasterClient, cardInstanceId, targetActorNumbers);

        /// <summary>함정 설치 — 내 턴 · 메인 행동 전 · 빈 슬롯 (04-card.md 6절). 메인 행동이 아니다.</summary>
        public void RequestSetTrap(int cardInstanceId) => photonView.RPC(nameof(RPC_RequestSetTrap), RpcTarget.MasterClient, cardInstanceId);

        /// <summary>설치한 함정 발동 — 조건 · 시점은 방장이 본다 (04-card.md 6절 「발동 시점」). 대상은 고르는 카드일 때만 쓰인다.</summary>
        public void RequestActivateTrap(int cardInstanceId, int[] targetActorNumbers) => photonView.RPC(nameof(RPC_RequestActivateTrap), RpcTarget.MasterClient, cardInstanceId, targetActorNumbers);

        /// <summary>찹츄 선언 — 손패가 정확히 10장일 때 언제든 (07-win-condition.md 4.1).</summary>
        public void RequestDeclareChapchu() => photonView.RPC(nameof(RPC_RequestDeclareChapchu), RpcTarget.MasterClient);

        /// <summary>카운터 사용 — 반응 시간 안에, 턴과 관계없이. 체인 맨 위 카드에 반응한다. 대상은 고르는 카드일 때만 쓰인다.</summary>
        public void RequestPlayCounter(int cardInstanceId, int[] targetActorNumbers) => photonView.RPC(nameof(RPC_RequestPlayCounter), RpcTarget.MasterClient, cardInstanceId, targetActorNumbers);

        [PunRPC]
        private void RPC_RequestDraw(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.Draw(info.Sender.ActorNumber));
        }

        [PunRPC]
        private void RPC_RequestPlayCard(int cardInstanceId, int[] targetActorNumbers, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.PlayCard(info.Sender.ActorNumber, cardInstanceId, targetActorNumbers));
        }

        [PunRPC]
        private void RPC_RequestSetTrap(int cardInstanceId, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.SetTrap(info.Sender.ActorNumber, cardInstanceId));
        }

        [PunRPC]
        private void RPC_RequestActivateTrap(int cardInstanceId, int[] targetActorNumbers, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.ActivateTrap(info.Sender.ActorNumber, cardInstanceId, targetActorNumbers));
        }

        [PunRPC]
        private void RPC_RequestDeclareChapchu(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.DeclareChapchu(info.Sender.ActorNumber));
        }

        [PunRPC]
        private void RPC_RequestPlayCounter(int cardInstanceId, int[] targetActorNumbers, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.PlayCounter(info.Sender.ActorNumber, cardInstanceId, targetActorNumbers));
        }

        // 나간 사람은 최종 사망과 같이 — 턴 순서 · 대상 후보에서 빼고 손패를 버림 더미로 (01-game-flow.md 6절).
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            Send(_server.RemovePlayer(otherPlayer.ActorNumber));
        }

        #endregion

        #region 결과 보내기 (방장)

        // 일어난 일을 순서대로 받을 사람에게 보낸다. 같은 보낸이의 RPC 는 보낸 순서대로 도착한다.
        // 지금 값 스냅샷(CustomProperties)은 늦게 들어온 · 복구용이라 묶어서 마지막에 한 번 쓴다 (09-network.md 6.1).
        private void Send(IReadOnlyList<GameEvent> events)
        {
            if (events.Count == 0)
                return;

            var roomProps = new Hashtable();
            var playerProps = new Dictionary<int, Hashtable>();

            foreach (GameEvent gameEvent in events)
            {
                switch (gameEvent)
                {
                    case HpChanged hp:
                        photonView.RPC(nameof(RPC_OnHpChanged), RpcTarget.All, hp.Actor, hp.Hp);
                        PlayerPropsOf(playerProps, hp.Actor)[PlayerProps.Hp] = hp.Hp;
                        break;

                    case CardDrawn drawn:
                        SendTo(drawn.Actor, nameof(RPC_OnMyCardDrawn), drawn.CardInstanceId, drawn.CardId); // 카드 내용은 주인만 (09 3절)
                        photonView.RPC(nameof(RPC_OnCardDrawn), RpcTarget.All, drawn.Actor, drawn.HandCount, drawn.DeckCount);
                        PlayerPropsOf(playerProps, drawn.Actor)[PlayerProps.HandCount] = drawn.HandCount;
                        roomProps[RoomProps.DeckCount] = drawn.DeckCount;
                        break;

                    case DeckRefilled refilled:
                        photonView.RPC(nameof(RPC_OnDeckRefilled), RpcTarget.All, refilled.DeckCount);
                        roomProps[RoomProps.DeckCount] = refilled.DeckCount;
                        roomProps[RoomProps.DiscardCount] = 0;
                        break;

                    case TurnStarted turn:
                        photonView.RPC(nameof(RPC_OnTurnStarted), RpcTarget.All, turn.Actor, turn.Deadline);
                        roomProps[RoomProps.TurnActor] = turn.Actor;
                        roomProps[RoomProps.TurnDeadline] = turn.Deadline;
                        Debug.Log($"[{nameof(PunGameServer)}] 턴 시작 P{turn.Actor} — {_server.DebugState()}");
                        break;

                    case CardPlayed played:
                        photonView.RPC(nameof(RPC_OnCardPlayed), RpcTarget.All, played.Actor, played.CardInstanceId, played.CardId, played.Targets, played.HandCount, played.DiscardCount);
                        PlayerPropsOf(playerProps, played.Actor)[PlayerProps.HandCount] = played.HandCount;
                        break;

                    case ReactionStarted reaction:
                        photonView.RPC(nameof(RPC_OnReactionStarted), RpcTarget.All, reaction.OpensAt, reaction.Deadline);
                        break;

                    case CardResolved resolved:
                        photonView.RPC(nameof(RPC_OnCardResolved), RpcTarget.All, resolved.Actor, resolved.CardInstanceId, resolved.CardId, resolved.Targets, resolved.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = resolved.DiscardCount;
                        break;

                    case CardNegated negated:
                        photonView.RPC(nameof(RPC_OnCardNegated), RpcTarget.All, negated.Actor, negated.CardInstanceId, negated.CardId, negated.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = negated.DiscardCount;
                        break;

                    case EmptyDeckDamaged empty:
                        photonView.RPC(nameof(RPC_OnEmptyDeckDamaged), RpcTarget.All, empty.Actor, empty.Amount);
                        break;

                    case DeathPending pending:
                        photonView.RPC(nameof(RPC_OnDeathPending), RpcTarget.All, pending.Actor, pending.StartsAt, pending.Deadline);
                        PlayerPropsOf(playerProps, pending.Actor)[PlayerProps.LifeState] = (byte)LifeState.DeathPending;
                        break;

                    case PlayerDied died:
                        photonView.RPC(nameof(RPC_OnPlayerDied), RpcTarget.All, died.Actor, died.DiscardCount);
                        PlayerPropsOf(playerProps, died.Actor)[PlayerProps.LifeState] = (byte)LifeState.Dead;
                        PlayerPropsOf(playerProps, died.Actor)[PlayerProps.HandCount] = 0;
                        PlayerPropsOf(playerProps, died.Actor)[PlayerProps.TrapCount] = 0;
                        roomProps[RoomProps.DiscardCount] = died.DiscardCount;
                        break;

                    case PlayerLeft left:
                        photonView.RPC(nameof(RPC_OnPlayerLeftGame), RpcTarget.All, left.Actor, left.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = left.DiscardCount; // 나간 사람의 Player Props 는 쓸 수 없다
                        break;

                    case TurnResumed resumed:
                        photonView.RPC(nameof(RPC_OnTurnResumed), RpcTarget.All, resumed.Actor, resumed.Deadline);
                        roomProps[RoomProps.TurnDeadline] = resumed.Deadline;
                        break;

                    case TrapSet set:
                        SendTo(set.Actor, nameof(RPC_OnMyTrapSet), set.CardInstanceId, set.CardId); // 종류는 소유자만 (09 3절)
                        photonView.RPC(nameof(RPC_OnTrapSet), RpcTarget.All, set.Actor, set.HandCount, set.TrapCount);
                        PlayerPropsOf(playerProps, set.Actor)[PlayerProps.HandCount] = set.HandCount;
                        PlayerPropsOf(playerProps, set.Actor)[PlayerProps.TrapCount] = set.TrapCount;
                        break;

                    case TrapActivated trap:
                        photonView.RPC(nameof(RPC_OnTrapActivated), RpcTarget.All, trap.Actor, trap.CardInstanceId, trap.CardId, trap.Targets, trap.TrapCount);
                        PlayerPropsOf(playerProps, trap.Actor)[PlayerProps.TrapCount] = trap.TrapCount;
                        break;

                    case TrapWindowStarted window:
                        photonView.RPC(nameof(RPC_OnTrapWindowStarted), RpcTarget.All, window.OpensAt, window.Deadline);
                        break;

                    case ChapchuChanged chapchu:
                        photonView.RPC(nameof(RPC_OnChapchuChanged), RpcTarget.All, chapchu.Actor, chapchu.IsChapchu);
                        PlayerPropsOf(playerProps, chapchu.Actor)[PlayerProps.ChapChu] = chapchu.IsChapchu;
                        break;

                    case GameEnded ended:
                        double returnAt = PhotonNetwork.Time + resultSeconds;
                        photonView.RPC(nameof(RPC_OnGameEnded), RpcTarget.All, ended.Winner, (byte)ended.Reason, returnAt);
                        Debug.Log($"[{nameof(PunGameServer)}] 게임 종료 — {ended.Reason} · 승자 P{ended.Winner} — {_server.DebugState()}");
                        StartCoroutine(ReturnToRoomRoutine());
                        break;

                    case RequestRejected rejected:
                        SendTo(rejected.Actor, nameof(RPC_RejectRequest), rejected.Code);
                        Debug.LogWarning($"[{nameof(PunGameServer)}] P{rejected.Actor} 요청 거절 — 코드 {rejected.Code}");
                        break;

                    default:
                        Debug.LogError($"[{nameof(PunGameServer)}] 보내는 방법이 없는 일: {gameEvent.GetType().Name}");
                        break;
                }
            }

            if (roomProps.Count > 0)
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

            foreach (KeyValuePair<int, Hashtable> pair in playerProps)
                PhotonNetwork.CurrentRoom.GetPlayer(pair.Key)?.SetCustomProperties(pair.Value);
        }

        // 결과 표시 시간 뒤 방을 다시 열고 전원을 방 씬으로 (08-room.md 4절). 다음 판은 방에서 다시 시작한다.
        private IEnumerator ReturnToRoomRoutine()
        {
            yield return new WaitForSeconds(resultSeconds);
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
                yield break;

            NetworkManager.Instance.UpdateRoomOptions(isVisible: true, isOpen: true);
            PhotonNetwork.LoadLevel(ScenePaths.Room);
        }

        private void SendTo(int actorNumber, string rpcName, params object[] parameters)
        {
            Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null)
                return; // 이미 나간 사람

            photonView.RPC(rpcName, player, parameters);
        }

        private static Hashtable PlayerPropsOf(Dictionary<int, Hashtable> playerProps, int actorNumber)
        {
            if (!playerProps.TryGetValue(actorNumber, out Hashtable props))
            {
                props = new Hashtable();
                playerProps[actorNumber] = props;
            }

            return props;
        }

        #endregion

        #region 결과 받기 (각 클라) → GameEvents

        [PunRPC]
        private void RPC_OnHpChanged(int actorNumber, int hp) => GameEvents.RaiseHpChanged(actorNumber, hp);

        [PunRPC]
        private void RPC_OnCardDrawn(int actorNumber, int handCount, int deckCount) => GameEvents.RaiseCardDrawn(actorNumber, handCount, deckCount);

        [PunRPC]
        private void RPC_OnMyCardDrawn(int cardInstanceId, int cardId) => GameEvents.RaiseMyCardDrawn(cardInstanceId, cardId);

        [PunRPC]
        private void RPC_OnDeckRefilled(int deckCount) => GameEvents.RaiseDeckRefilled(deckCount);

        [PunRPC]
        private void RPC_OnTurnStarted(int actorNumber, double deadline) => GameEvents.RaiseTurnStarted(actorNumber, deadline);

        [PunRPC]
        private void RPC_OnCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
            => GameEvents.RaiseCardPlayed(actorNumber, cardInstanceId, cardId, targets, handCount, discardCount);

        [PunRPC]
        private void RPC_OnReactionStarted(double opensAt, double deadline) => GameEvents.RaiseReactionStarted(opensAt, deadline);

        [PunRPC]
        private void RPC_OnCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount)
            => GameEvents.RaiseCardResolved(actorNumber, cardInstanceId, cardId, targets, discardCount);

        [PunRPC]
        private void RPC_OnCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount)
            => GameEvents.RaiseCardNegated(actorNumber, cardInstanceId, cardId, discardCount);

        [PunRPC]
        private void RPC_OnEmptyDeckDamaged(int actorNumber, int amount) => GameEvents.RaiseEmptyDeckDamaged(actorNumber, amount);

        [PunRPC]
        private void RPC_OnDeathPending(int actorNumber, double startsAt, double deadline) => GameEvents.RaiseDeathPending(actorNumber, startsAt, deadline);

        [PunRPC]
        private void RPC_OnPlayerDied(int actorNumber, int discardCount) => GameEvents.RaisePlayerDied(actorNumber, discardCount);

        [PunRPC]
        private void RPC_OnPlayerLeftGame(int actorNumber, int discardCount) => GameEvents.RaisePlayerLeftGame(actorNumber, discardCount);

        [PunRPC]
        private void RPC_OnTurnResumed(int actorNumber, double deadline) => GameEvents.RaiseTurnResumed(actorNumber, deadline);

        [PunRPC]
        private void RPC_OnTrapSet(int actorNumber, int handCount, int trapCount) => GameEvents.RaiseTrapSet(actorNumber, handCount, trapCount);

        [PunRPC]
        private void RPC_OnMyTrapSet(int cardInstanceId, int cardId) => GameEvents.RaiseMyTrapSet(cardInstanceId, cardId);

        [PunRPC]
        private void RPC_OnTrapActivated(int actorNumber, int cardInstanceId, int cardId, int[] targets, int trapCount)
            => GameEvents.RaiseTrapActivated(actorNumber, cardInstanceId, cardId, targets, trapCount);

        [PunRPC]
        private void RPC_OnTrapWindowStarted(double opensAt, double deadline) => GameEvents.RaiseTrapWindowStarted(opensAt, deadline);

        [PunRPC]
        private void RPC_OnChapchuChanged(int actorNumber, bool isChapchu) => GameEvents.RaiseChapchuChanged(actorNumber, isChapchu);

        [PunRPC]
        private void RPC_OnGameEnded(int winner, byte reason, double returnAt) => GameEvents.RaiseGameEnded(winner, (GameEndReason)reason, returnAt);

        [PunRPC]
        private void RPC_RejectRequest(int code) => GameEvents.RaiseMyRequestRejected(code);

        #endregion
    }
}
