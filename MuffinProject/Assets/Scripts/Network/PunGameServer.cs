using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Game.Bots;
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
        [Tooltip("이번 게임의 덱 구성 (05-deck.md 8절). 덱 구성은 미정 — 지금은 카드 묶음 덱(Deck_Main, 11-card-list 0절)을 쓴다.")]
        [SerializeField] private DeckData deck;

        [Tooltip("게임이 끝나고 결과를 보여 주는 시간(초). 지나면 방장이 방을 다시 열고 전원을 방 씬으로 옮긴다 (08-room.md 4절).")]
        [Min(0)] [SerializeField] private float resultSeconds = 5f;

        private GameServer _server; // 모든 클라가 만들지만 방장에서만 쓰인다
        private BotDriver _bots;    // 봇의 판단 — 방장에서만 (18-bot.md 3절 #1). 봇이 없으면 null
        private int _sentSeq;       // 방장 — 마지막으로 보낸 처리 번호 (09-network.md 5절)
        private int _receivedSeq;   // 각 클라 — 마지막으로 받은 처리 번호

        /// <summary>이번 게임의 덱 — 카드 ID 로 카드 정의를 찾는 사전으로도 쓴다 (DeckData.GetCard).</summary>
        public DeckData Deck => deck;

        private void Awake()
        {
            if (deck == null)
                Debug.LogError($"[{nameof(PunGameServer)}] {nameof(deck)} 참조가 비어 있습니다.", this);

            _server = new GameServer(new System.Random(), () => PhotonNetwork.Time);
        }

        // 이 씬은 방에서 LoadLevel 로 넘어오므로 방장이 바로 시작한다 (01-game-flow.md 3절).
        // 한 프레임 뒤에 — 방장 자신에게는 알림이 그 자리에서 재생되므로, 화면들의 Start(좌석 대응 · 스냅샷 복구)가 먼저 끝나야 시작 알림이 덮어쓴다.
        private IEnumerator Start()
        {
            if (!PhotonNetwork.IsMasterClient)
                yield break;

            yield return null;
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
                yield break;

            // 봇도 플레이어다 — 사람 뒤에 봇 번호 순으로 넣고, 턴 순서는 규칙이 섞는다 (18-bot.md 3절 #6)
            int[] bots = BotRoster.Actors;
            int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).Concat(bots).ToArray();
            List<Card> cards = deck.CreateCards();
            Debug.Log($"[{nameof(PunGameServer)}] 게임 시작 — 참가자 {string.Join(", ", actors.Select(a => $"P{a}"))} · 덱 {cards.Count}장");

            // 지난 판의 지금 값을 지운다 — 시작 일이 다시 쓰지 않는 값 (늦게 열린 화면이 지난 판 상태를 읽지 않게)
            var reset = new Hashtable { [PlayerProps.LifeState] = (byte)LifeState.Alive, [PlayerProps.ChapChu] = false, [PlayerProps.TrapCount] = 0 };
            foreach (Player player in PhotonNetwork.PlayerList)
                player.SetCustomProperties(reset);

            // 버림 더미는 빈 채로 · 턴 방향은 정방향으로 시작한다 — 값이 null 이면 Photon 이 키를 지운다
            var roomReset = new Hashtable { [RoomProps.DiscardCount] = 0, [RoomProps.DiscardTop] = null, [RoomProps.TurnDirection] = null, [RoomProps.WaitCasters] = null, [RoomProps.Fallout] = null, [RoomProps.Gaepan] = null };
            foreach (int bot in bots)
            {
                foreach (DictionaryEntry entry in reset)
                    roomReset[BotRoster.PropKey((string)entry.Key, bot)] = entry.Value;
            }
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomReset);

            if (bots.Length > 0)
                _bots = new BotDriver(_server, bots, new System.Random(), () => PhotonNetwork.Time);

            Send(_server.StartGame(actors, cards));
        }

        // 턴 마감 판정 · 봇의 판단은 방장만 한다 (09-network.md 8절 · 18-bot.md 3절 #1).
        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            Send(_server.Tick());
            if (_bots == null)
                return;

            Send(_bots.Tick());
            foreach (string reason in _bots.TakeLogs())
                Debug.Log($"[{nameof(BotDriver)}] {reason}");
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

        /// <summary>
        /// 내 손패 · 설치 함정 내용을 다시 받는다 (09-network.md 10절) — 화면이 늦게 열렸거나 다시 그릴 때. 답은 GameEvents.OnMySync.
        /// 공개 값(체력 · 장수 · 턴)은 CustomProperties 스냅샷으로 읽는다.
        /// </summary>
        public void RequestMySync() => photonView.RPC(nameof(RPC_RequestMySync), RpcTarget.MasterClient);

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
        private void RPC_RequestMySync(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 지금까지 보낸 알림 뒤에 간다 — 받는 쪽은 이 번호 전의 묵은 알림을 버린다
            _server.GetPrivateCards(info.Sender.ActorNumber, out int[] handIds, out int[] handCardIds, out int[] trapIds, out int[] trapCardIds);
            SendTo(info.Sender.ActorNumber, nameof(RPC_OnMySync), handIds, handCardIds, trapIds, trapCardIds);
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomProps.EventSeq] = _sentSeq }); // 새 방장이 이 번호부터 잇게 (09-network.md 5절)
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

        // 새 방장은 이어서 번호를 매긴다 — 아니면 클라들이 새 알림을 묵은 것으로 버린다. 게임 상태 이주는 보류 (09-network.md 9절)
        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            int fromRoom = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.EventSeq, out object value) && value is int seq ? seq : 0;
            _sentSeq = System.Math.Max(System.Math.Max(_sentSeq, _receivedSeq), fromRoom);
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
                        Broadcast(nameof(RPC_OnHpChanged), hp.Actor, hp.Hp);
                        PlayerPropsOf(playerProps, hp.Actor)[PlayerProps.Hp] = hp.Hp;
                        break;

                    case CardDrawn drawn:
                        SendTo(drawn.Actor, nameof(RPC_OnMyCardDrawn), drawn.CardInstanceId, drawn.CardId); // 카드 내용은 주인만 (09 3절)
                        Broadcast(nameof(RPC_OnCardDrawn), drawn.Actor, drawn.HandCount, drawn.DeckCount);
                        PlayerPropsOf(playerProps, drawn.Actor)[PlayerProps.HandCount] = drawn.HandCount;
                        roomProps[RoomProps.DeckCount] = drawn.DeckCount;
                        break;

                    case DeckRefilled refilled:
                        Broadcast(nameof(RPC_OnDeckRefilled), refilled.DeckCount);
                        roomProps[RoomProps.DeckCount] = refilled.DeckCount;
                        roomProps[RoomProps.DiscardCount] = 0;
                        roomProps[RoomProps.DiscardTop] = null; // 값이 null 이면 Photon 이 키를 지운다
                        break;

                    case TurnStarted turn:
                        Broadcast(nameof(RPC_OnTurnStarted), turn.Actor, turn.Deadline);
                        roomProps[RoomProps.TurnActor] = turn.Actor;
                        roomProps[RoomProps.TurnDeadline] = turn.Deadline;
                        Debug.Log($"[{nameof(PunGameServer)}] 턴 시작 P{turn.Actor} — {_server.DebugState()}");
                        break;

                    case CardPlayed played:
                        Broadcast(nameof(RPC_OnCardPlayed), played.Actor, played.CardInstanceId, played.CardId, played.Targets, played.HandCount, played.DiscardCount);
                        PlayerPropsOf(playerProps, played.Actor)[PlayerProps.HandCount] = played.HandCount;
                        break;

                    case ReactionStarted reaction:
                        Broadcast(nameof(RPC_OnReactionStarted), reaction.OpensAt, reaction.Deadline);
                        break;

                    case CountersUsable usable:
                        SendTo(usable.Actor, nameof(RPC_OnMyCountersUsable), usable.CardInstanceIds); // 손패 내용 — 본인만 (09 3절)
                        break;

                    case CardResolved resolved:
                        Broadcast(nameof(RPC_OnCardResolved), resolved.Actor, resolved.CardInstanceId, resolved.CardId, resolved.Targets, resolved.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = resolved.DiscardCount;
                        roomProps[RoomProps.DiscardTop] = resolved.CardId;
                        break;

                    case CardNegated negated:
                        Broadcast(nameof(RPC_OnCardNegated), negated.Actor, negated.CardInstanceId, negated.CardId, negated.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = negated.DiscardCount;
                        roomProps[RoomProps.DiscardTop] = negated.CardId;
                        break;

                    case EmptyDeckDamaged empty:
                        Broadcast(nameof(RPC_OnEmptyDeckDamaged), empty.Actor, empty.Amount);
                        break;

                    case DeathPending pending:
                        Broadcast(nameof(RPC_OnDeathPending), pending.Actor, pending.StartsAt, pending.Deadline);
                        PlayerPropsOf(playerProps, pending.Actor)[PlayerProps.LifeState] = (byte)LifeState.DeathPending;
                        break;

                    case PlayerDied died:
                        Broadcast(nameof(RPC_OnPlayerDied), died.Actor, died.CardIds, died.DiscardCount);
                        PlayerPropsOf(playerProps, died.Actor)[PlayerProps.LifeState] = (byte)LifeState.Dead;
                        PlayerPropsOf(playerProps, died.Actor)[PlayerProps.HandCount] = 0;
                        PlayerPropsOf(playerProps, died.Actor)[PlayerProps.TrapCount] = 0;
                        roomProps[RoomProps.DiscardCount] = died.DiscardCount;
                        SetDiscardTop(roomProps, died.CardIds);
                        break;

                    case PlayerLeft left:
                        Broadcast(nameof(RPC_OnPlayerLeftGame), left.Actor, left.CardIds, left.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = left.DiscardCount; // 나간 사람의 Player Props 는 쓸 수 없다
                        SetDiscardTop(roomProps, left.CardIds);
                        break;

                    case TurnResumed resumed:
                        Broadcast(nameof(RPC_OnTurnResumed), resumed.Actor, resumed.Deadline);
                        roomProps[RoomProps.TurnDeadline] = resumed.Deadline;
                        break;

                    case TrapSet set:
                        SendTo(set.Actor, nameof(RPC_OnMyTrapSet), set.CardInstanceId, set.CardId); // 종류는 소유자만 (09 3절)
                        Broadcast(nameof(RPC_OnTrapSet), set.Actor, set.HandCount, set.TrapCount);
                        PlayerPropsOf(playerProps, set.Actor)[PlayerProps.HandCount] = set.HandCount;
                        PlayerPropsOf(playerProps, set.Actor)[PlayerProps.TrapCount] = set.TrapCount;
                        break;

                    case TrapActivated trap:
                        Broadcast(nameof(RPC_OnTrapActivated), trap.Actor, trap.CardInstanceId, trap.CardId, trap.Targets, trap.TrapCount);
                        PlayerPropsOf(playerProps, trap.Actor)[PlayerProps.TrapCount] = trap.TrapCount;
                        break;

                    case TrapWindowStarted window:
                        Broadcast(nameof(RPC_OnTrapWindowStarted), window.OpensAt, window.Deadline);
                        break;

                    case HandCardsMoved moved:
                        SendTo(moved.From, nameof(RPC_OnMyCardsLost), moved.CardInstanceIds);                    // 카드 내용은 두 사람만 (09 3절)
                        SendTo(moved.To, nameof(RPC_OnMyCardsGained), moved.CardInstanceIds, moved.CardIds);
                        Broadcast(nameof(RPC_OnHandCardsMoved), moved.From, moved.To, moved.CardIds.Length, moved.FromHandCount, moved.ToHandCount);
                        PlayerPropsOf(playerProps, moved.From)[PlayerProps.HandCount] = moved.FromHandCount;
                        PlayerPropsOf(playerProps, moved.To)[PlayerProps.HandCount] = moved.ToHandCount;
                        break;

                    case CardsDiscarded discarded:
                        Broadcast(nameof(RPC_OnCardsDiscarded), discarded.Actor, discarded.CardInstanceIds, discarded.CardIds, discarded.HandCount, discarded.DiscardCount);
                        PlayerPropsOf(playerProps, discarded.Actor)[PlayerProps.HandCount] = discarded.HandCount;
                        roomProps[RoomProps.DiscardCount] = discarded.DiscardCount;
                        SetDiscardTop(roomProps, discarded.CardIds);
                        break;

                    case TrapDestroyed destroyed:
                        Broadcast(nameof(RPC_OnTrapDestroyed), destroyed.Owner, destroyed.CardInstanceId, destroyed.CardId, destroyed.TrapCount, destroyed.DiscardCount);
                        PlayerPropsOf(playerProps, destroyed.Owner)[PlayerProps.TrapCount] = destroyed.TrapCount;
                        roomProps[RoomProps.DiscardCount] = destroyed.DiscardCount;
                        roomProps[RoomProps.DiscardTop] = destroyed.CardId;
                        break;

                    case TrapReturned returned:
                        SendTo(returned.Owner, nameof(RPC_OnMyTrapRemoved), returned.CardInstanceId);
                        SendTo(returned.Owner, nameof(RPC_OnMyCardsGained), new[] { returned.CardInstanceId }, new[] { returned.CardId });
                        Broadcast(nameof(RPC_OnTrapReturned), returned.Owner, returned.TrapCount, returned.HandCount);
                        PlayerPropsOf(playerProps, returned.Owner)[PlayerProps.TrapCount] = returned.TrapCount;
                        PlayerPropsOf(playerProps, returned.Owner)[PlayerProps.HandCount] = returned.HandCount;
                        break;

                    case TrapMoved trapMoved:
                        SendTo(trapMoved.From, nameof(RPC_OnMyTrapRemoved), trapMoved.CardInstanceId);
                        SendTo(trapMoved.To, nameof(RPC_OnMyTrapSet), trapMoved.CardInstanceId, trapMoved.CardId);
                        Broadcast(nameof(RPC_OnTrapMoved), trapMoved.From, trapMoved.To, trapMoved.FromTrapCount, trapMoved.ToTrapCount);
                        PlayerPropsOf(playerProps, trapMoved.From)[PlayerProps.TrapCount] = trapMoved.FromTrapCount;
                        PlayerPropsOf(playerProps, trapMoved.To)[PlayerProps.TrapCount] = trapMoved.ToTrapCount;
                        break;

                    case TurnSkipMarked marked:
                        Broadcast(nameof(RPC_OnTurnSkipMarked), marked.Actor);
                        break;

                    case TurnSkipped skipped:
                        Broadcast(nameof(RPC_OnTurnSkipped), skipped.Actor);
                        break;

                    case CardReturnedToHand back:
                        Broadcast(nameof(RPC_OnCardReturnedToHand), back.Actor, back.CardInstanceId, back.CardId, back.HandCount);
                        PlayerPropsOf(playerProps, back.Actor)[PlayerProps.HandCount] = back.HandCount;
                        break;

                    case DiscardTaken taken:
                        // 버림 더미는 공개라 가져간 카드도 전원에게 (05-deck.md 7절). 맨 위가 비면 키를 지운다
                        Broadcast(nameof(RPC_OnDiscardTaken), taken.Actor, taken.CardInstanceId, taken.CardId, taken.HandCount, taken.DiscardCount, taken.TopCardId);
                        PlayerPropsOf(playerProps, taken.Actor)[PlayerProps.HandCount] = taken.HandCount;
                        roomProps[RoomProps.DiscardCount] = taken.DiscardCount;
                        roomProps[RoomProps.DiscardTop] = taken.TopCardId > 0 ? (object)taken.TopCardId : null;
                        break;

                    case TurnDirectionChanged direction:
                        Broadcast(nameof(RPC_OnTurnDirectionChanged), direction.Reversed, direction.ExtraTurn);
                        roomProps[RoomProps.TurnDirection] = direction.Reversed ? (object)true : null;
                        break;

                    case ActionBlockChanged block:
                        Broadcast(nameof(RPC_OnActionBlockChanged), block.Actor, (byte)block.State);
                        PlayerPropsOf(playerProps, block.Actor)[PlayerProps.ActionBlock] = block.State != ActionBlock.None ? (object)(byte)block.State : null;
                        break;

                    case WaitChanged wait:
                        Broadcast(nameof(RPC_OnWaitChanged), wait.Casters);
                        roomProps[RoomProps.WaitCasters] = wait.Casters.Length > 0 ? wait.Casters : null;
                        break;

                    case GameRestarted restarted:
                        Broadcast(nameof(RPC_OnGameRestarted), restarted.User, restarted.Count, restarted.Order);
                        foreach (int actor in restarted.Order)
                        {
                            Hashtable props = PlayerPropsOf(playerProps, actor);
                            props[PlayerProps.LifeState] = (byte)LifeState.Alive;
                            props[PlayerProps.ChapChu] = false;
                            props[PlayerProps.TrapCount] = 0;
                            props[PlayerProps.HandCount] = 0;
                            props[PlayerProps.ActionBlock] = null;
                        }
                        roomProps[RoomProps.DiscardCount] = 0;
                        roomProps[RoomProps.DiscardTop] = null;
                        roomProps[RoomProps.TurnDirection] = null;
                        roomProps[RoomProps.WaitCasters] = null;
                        Debug.Log($"[{nameof(PunGameServer)}] 대소동 — 새 판 ({restarted.Count}번째) · 순서 {string.Join(" → ", restarted.Order.Select(a => $"P{a}"))}");
                        break;

                    case FalloutChanged fallout:
                        Broadcast(nameof(RPC_OnFalloutChanged), fallout.N, fallout.Turns);
                        roomProps[RoomProps.Fallout] = fallout.N > 0 ? new[] { fallout.N, fallout.Turns } : null;
                        break;

                    case GaepanChanged gaepan:
                        Broadcast(nameof(RPC_OnGaepanChanged), gaepan.Caster, gaepan.Left, gaepan.Again);
                        roomProps[RoomProps.Gaepan] = gaepan.Left > 0 ? new[] { gaepan.Caster, gaepan.Left } : null;
                        break;

                    case GaepanJudged judged:
                        Broadcast(nameof(RPC_OnGaepanJudged), judged.Loser, judged.ByHp, judged.Value, judged.Tied);
                        Debug.Log($"[{nameof(PunGameServer)}] 개판 판정 — 기준 {(judged.ByHp ? "HP" : "손패")} 꼴찌 P{judged.Loser} ({judged.Value}) · 동점 {judged.Tied.Length}명");
                        break;

                    case ChapchuWinHeld held:
                        Broadcast(nameof(RPC_OnChapchuWinHeld), held.Actor);
                        break;

                    case DeathAvoided avoided:
                        Broadcast(nameof(RPC_OnDeathAvoided), avoided.Actor, avoided.Hp);
                        PlayerPropsOf(playerProps, avoided.Actor)[PlayerProps.LifeState] = (byte)LifeState.Alive;
                        break;

                    case ChapchuChanged chapchu:
                        Broadcast(nameof(RPC_OnChapchuChanged), chapchu.Actor, chapchu.IsChapchu);
                        PlayerPropsOf(playerProps, chapchu.Actor)[PlayerProps.ChapChu] = chapchu.IsChapchu;
                        break;

                    case GameEnded ended:
                        double returnAt = PhotonNetwork.Time + resultSeconds;
                        Broadcast(nameof(RPC_OnGameEnded), ended.Winner, (byte)ended.Reason, returnAt);
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

            // 봇은 접속자가 아니라 Player 속성이 없다 — 봇의 공개 값은 방 속성에 같은 키 + #식별자로 (18-bot.md 9절)
            foreach (KeyValuePair<int, Hashtable> pair in playerProps)
            {
                if (!BotRoster.IsBot(pair.Key))
                    continue;

                foreach (DictionaryEntry entry in pair.Value)
                    roomProps[BotRoster.PropKey((string)entry.Key, pair.Key)] = entry.Value;
            }

            roomProps[RoomProps.EventSeq] = _sentSeq; // 지금 값 스냅샷이 어느 알림까지 반영했는지
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

            foreach (KeyValuePair<int, Hashtable> pair in playerProps)
            {
                if (!BotRoster.IsBot(pair.Key))
                    PhotonNetwork.CurrentRoom.GetPlayer(pair.Key)?.SetCustomProperties(pair.Value);
            }
        }

        // 버림 더미에 마지막으로 들어간 카드가 맨 위다. 들어간 카드가 없으면 맨 위는 그대로.
        private static void SetDiscardTop(Hashtable roomProps, int[] discardedCardIds)
        {
            if (discardedCardIds.Length > 0)
                roomProps[RoomProps.DiscardTop] = discardedCardIds[discardedCardIds.Length - 1];
        }

        // 결과 표시 시간 뒤 방을 다시 열고 전원을 방 씬으로 (08-room.md 4절). 다음 판은 방에서 다시 시작한다.
        private IEnumerator ReturnToRoomRoutine()
        {
            yield return new WaitForSeconds(resultSeconds);
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
                yield break;

            // 버림 더미 스냅샷은 방으로 돌아가기 전에 비운다 — 다음 판 화면들의 Start(스냅샷 복구)는 방장의 시작 초기화보다 먼저 읽는다
            // 찹츄 · 함정 장수도 같은 이유로 여기서 지운다 — 게임 종료는 찹츄를 해제하지 않고, 시작 일은 이 값을 다시 보내지 않는다. 봇은 방 속성 키 (18-bot.md 9절)
            var playerReset = new Hashtable { [PlayerProps.ChapChu] = false, [PlayerProps.TrapCount] = 0 };
            var roomReset = new Hashtable { [RoomProps.DiscardCount] = 0, [RoomProps.DiscardTop] = null, [RoomProps.TurnDirection] = null, [RoomProps.WaitCasters] = null, [RoomProps.Fallout] = null, [RoomProps.Gaepan] = null };
            foreach (int bot in BotRoster.Actors)
            {
                foreach (DictionaryEntry entry in playerReset)
                    roomReset[BotRoster.PropKey((string)entry.Key, bot)] = entry.Value;
            }
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomReset);

            foreach (Player player in PhotonNetwork.PlayerList)
                player.SetCustomProperties(playerReset);

            NetworkManager.Instance.UpdateRoomOptions(isVisible: !BotRoster.IsSoloRoom, isOpen: true); // 「혼자 하기」 방은 비공개 그대로 (18-bot.md 10절)
            PhotonNetwork.LoadLevel(ScenePaths.Room);
        }

        // 처리 번호 (09-network.md 5절) — 방장이 보내는 알림 1건마다 1씩 는다. 받는 쪽은 이미 받은 번호 이하를 버린다 (중복 적용 방지).
        private void Broadcast(string rpcName, params object[] parameters) => photonView.RPC(rpcName, RpcTarget.All, WithSeq(parameters));

        private void SendTo(int actorNumber, string rpcName, params object[] parameters)
        {
            Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null)
                return; // 이미 나간 사람 · 봇 (봇은 접속자가 아니다 — 비공개 내용은 아무에게도 가지 않는다)

            photonView.RPC(rpcName, player, WithSeq(parameters));
        }

        private object[] WithSeq(object[] parameters)
        {
            var withSeq = new object[parameters.Length + 1];
            withSeq[0] = ++_sentSeq;
            parameters.CopyTo(withSeq, 1);
            return withSeq;
        }

        // 받는 쪽 — 같은 보낸이의 RPC 는 순서대로 오므로 번호는 늘기만 한다. 이하가 오면 이미 반영한 것 (동기화 응답 뒤의 묵은 알림 등)
        // 결과는 방장이 보낸 것만 반영한다 (09-network.md 2절 #3) — 아니면 다른 클라가 번호를 올려 진짜 알림을 막을 수 있다
        private bool Accept(int seq, PhotonMessageInfo info)
        {
            if (info.Sender == null || !info.Sender.IsMasterClient)
            {
                Debug.LogWarning($"[{nameof(PunGameServer)}] 방장이 아닌 P{info.Sender?.ActorNumber} 가 보낸 결과 — 버린다");
                return false;
            }

            if (seq <= _receivedSeq)
            {
                Debug.LogWarning($"[{nameof(PunGameServer)}] 이미 받은 처리 번호 {seq} (마지막 {_receivedSeq}) — 버린다");
                return false;
            }

            _receivedSeq = seq;
            return true;
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
        private void RPC_OnHpChanged(int seq, int actorNumber, int hp, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseHpChanged(actorNumber, hp);
        }

        [PunRPC]
        private void RPC_OnCardDrawn(int seq, int actorNumber, int handCount, int deckCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardDrawn(actorNumber, handCount, deckCount);
        }

        [PunRPC]
        private void RPC_OnMyCardDrawn(int seq, int cardInstanceId, int cardId, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyCardDrawn(cardInstanceId, cardId);
        }

        [PunRPC]
        private void RPC_OnDeckRefilled(int seq, int deckCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseDeckRefilled(deckCount);
        }

        [PunRPC]
        private void RPC_OnTurnStarted(int seq, int actorNumber, double deadline, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTurnStarted(actorNumber, deadline);
        }

        [PunRPC]
        private void RPC_OnCardPlayed(int seq, int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardPlayed(actorNumber, cardInstanceId, cardId, targets, handCount, discardCount);
        }

        [PunRPC]
        private void RPC_OnReactionStarted(int seq, double opensAt, double deadline, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseReactionStarted(opensAt, deadline);
        }

        [PunRPC]
        private void RPC_OnMyCountersUsable(int seq, int[] cardInstanceIds, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyCountersUsable(cardInstanceIds);
        }

        [PunRPC]
        private void RPC_OnCardResolved(int seq, int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardResolved(actorNumber, cardInstanceId, cardId, targets, discardCount);
        }

        [PunRPC]
        private void RPC_OnCardNegated(int seq, int actorNumber, int cardInstanceId, int cardId, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardNegated(actorNumber, cardInstanceId, cardId, discardCount);
        }

        [PunRPC]
        private void RPC_OnEmptyDeckDamaged(int seq, int actorNumber, int amount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseEmptyDeckDamaged(actorNumber, amount);
        }

        [PunRPC]
        private void RPC_OnDeathPending(int seq, int actorNumber, double startsAt, double deadline, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseDeathPending(actorNumber, startsAt, deadline);
        }

        [PunRPC]
        private void RPC_OnPlayerDied(int seq, int actorNumber, int[] cardIds, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaisePlayerDied(actorNumber, cardIds, discardCount);
        }

        [PunRPC]
        private void RPC_OnPlayerLeftGame(int seq, int actorNumber, int[] cardIds, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaisePlayerLeftGame(actorNumber, cardIds, discardCount);
        }

        [PunRPC]
        private void RPC_OnTurnResumed(int seq, int actorNumber, double deadline, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTurnResumed(actorNumber, deadline);
        }

        [PunRPC]
        private void RPC_OnTrapSet(int seq, int actorNumber, int handCount, int trapCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTrapSet(actorNumber, handCount, trapCount);
        }

        [PunRPC]
        private void RPC_OnMyTrapSet(int seq, int cardInstanceId, int cardId, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyTrapSet(cardInstanceId, cardId);
        }

        [PunRPC]
        private void RPC_OnTrapActivated(int seq, int actorNumber, int cardInstanceId, int cardId, int[] targets, int trapCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTrapActivated(actorNumber, cardInstanceId, cardId, targets, trapCount);
        }

        [PunRPC]
        private void RPC_OnTrapWindowStarted(int seq, double opensAt, double deadline, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTrapWindowStarted(opensAt, deadline);
        }

        [PunRPC]
        private void RPC_OnHandCardsMoved(int seq, int from, int to, int count, int fromHandCount, int toHandCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseHandCardsMoved(from, to, count, fromHandCount, toHandCount);
        }

        [PunRPC]
        private void RPC_OnMyCardsGained(int seq, int[] cardInstanceIds, int[] cardIds, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyCardsGained(cardInstanceIds, cardIds);
        }

        [PunRPC]
        private void RPC_OnMyCardsLost(int seq, int[] cardInstanceIds, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyCardsLost(cardInstanceIds);
        }

        [PunRPC]
        private void RPC_OnCardsDiscarded(int seq, int actorNumber, int[] cardInstanceIds, int[] cardIds, int handCount, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardsDiscarded(actorNumber, cardInstanceIds, cardIds, handCount, discardCount);
        }

        [PunRPC]
        private void RPC_OnTrapDestroyed(int seq, int owner, int cardInstanceId, int cardId, int trapCount, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTrapDestroyed(owner, cardInstanceId, cardId, trapCount, discardCount);
        }

        [PunRPC]
        private void RPC_OnTrapReturned(int seq, int owner, int trapCount, int handCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTrapReturned(owner, trapCount, handCount);
        }

        [PunRPC]
        private void RPC_OnMyTrapRemoved(int seq, int cardInstanceId, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyTrapRemoved(cardInstanceId);
        }

        [PunRPC]
        private void RPC_OnTrapMoved(int seq, int from, int to, int fromTrapCount, int toTrapCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTrapMoved(from, to, fromTrapCount, toTrapCount);
        }

        [PunRPC]
        private void RPC_OnTurnSkipMarked(int seq, int actorNumber, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTurnSkipMarked(actorNumber);
        }

        [PunRPC]
        private void RPC_OnTurnSkipped(int seq, int actorNumber, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTurnSkipped(actorNumber);
        }

        [PunRPC]
        private void RPC_OnDiscardTaken(int seq, int actorNumber, int cardInstanceId, int cardId, int handCount, int discardCount, int topCardId, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseDiscardTaken(actorNumber, cardInstanceId, cardId, handCount, discardCount, topCardId);
        }

        [PunRPC]
        private void RPC_OnTurnDirectionChanged(int seq, bool reversed, int extraTurn, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTurnDirectionChanged(reversed, extraTurn);
        }

        [PunRPC]
        private void RPC_OnActionBlockChanged(int seq, int actorNumber, byte state, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseActionBlockChanged(actorNumber, (ActionBlock)state);
        }

        [PunRPC]
        private void RPC_OnWaitChanged(int seq, int[] casters, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseWaitChanged(casters);
        }

        [PunRPC]
        private void RPC_OnGameRestarted(int seq, int user, int count, int[] order, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseGameRestarted(user, count, order);
        }

        [PunRPC]
        private void RPC_OnFalloutChanged(int seq, int n, int turns, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseFalloutChanged(n, turns);
        }

        [PunRPC]
        private void RPC_OnGaepanChanged(int seq, int caster, int left, bool again, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseGaepanChanged(caster, left, again);
        }

        [PunRPC]
        private void RPC_OnGaepanJudged(int seq, int loser, bool byHp, int value, int[] tied, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseGaepanJudged(loser, byHp, value, tied);
        }

        [PunRPC]
        private void RPC_OnChapchuWinHeld(int seq, int actorNumber, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseChapchuWinHeld(actorNumber);
        }

        [PunRPC]
        private void RPC_OnCardReturnedToHand(int seq, int actorNumber, int cardInstanceId, int cardId, int handCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardReturnedToHand(actorNumber, cardInstanceId, cardId, handCount);
        }

        [PunRPC]
        private void RPC_OnDeathAvoided(int seq, int actorNumber, int hp, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseDeathAvoided(actorNumber, hp);
        }

        [PunRPC]
        private void RPC_OnChapchuChanged(int seq, int actorNumber, bool isChapchu, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseChapchuChanged(actorNumber, isChapchu);
        }

        [PunRPC]
        private void RPC_OnGameEnded(int seq, int winner, byte reason, double returnAt, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseGameEnded(winner, (GameEndReason)reason, returnAt);
        }

        [PunRPC]
        private void RPC_OnMySync(int seq, int[] handIds, int[] handCardIds, int[] trapIds, int[] trapCardIds, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMySync(handIds, handCardIds, trapIds, trapCardIds);
        }

        [PunRPC]
        private void RPC_RejectRequest(int seq, int code, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyRequestRejected(code);
        }

        #endregion
    }
}
