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
    // 1. 보내기:  public void RequestSetTrap(int cardInstanceId) => SendRequest(nameof(RPC_RequestSetTrap), cardInstanceId);
    // 2. 받기 (방장). 요청자는 인자로 받지 말고 info.Sender 를 쓴다:
    //        [PunRPC] private void RPC_RequestSetTrap(int cardInstanceId, PhotonMessageInfo info)
    //        {
    //            if (!NetworkManager.IsTrustedMaster) return;
    //            Send(_server.SetTrap(info.Sender.ActorNumber, cardInstanceId));
    //        }
    // 3. 새 종류의 일이면 Send 의 switch 에 case 를 더한다 — 누구에게(전원 / 주인만) 보낼지는 여기서 정한다.
    [RequireComponent(typeof(PhotonView))]
    public class PunGameServer : MonoBehaviourPunCallbacks
    {
        [Tooltip("이번 게임의 덱 구성 (05-deck.md 8절). 덱 구성은 미정 — 지금은 카드 묶음 덱(Deck_Main, 11-card-list 0절)을 쓴다.")]
        [SerializeField] private DeckData deck;

        [Tooltip("게임이 끝난 뒤 결과 씬으로 넘어가기까지의 시간(초) — 마지막 연출을 볼 시간 (07-win-condition.md 11-1, 제안).")]
        [Min(0)] [SerializeField] private float resultDelaySeconds = 2f;

        private GameServer _server; // 모든 클라가 만들지만 방장에서만 쓰인다
        private BotDriver _bots;    // 봇의 판단 — 방장에서만 (18-bot.md 3절 #1). 봇이 없으면 null
        private int _sentSeq;       // 방장 — 마지막으로 보낸 처리 번호 (09-network.md 5절)
        private int _receivedSeq;   // 각 클라 — 마지막으로 받은 처리 번호

        private readonly RequestLimiter _limiter = new RequestLimiter(nameof(PunGameServer)); // 요청 빈도 제한 (09-network.md 13절)

        // 게임을 시작할 때의 닉네임 — 결과 화면이 나간 사람의 이름도 보이게 (07-win-condition.md 11-2). 모든 클라가 자기 것을 찍어 둔다
        private readonly Dictionary<int, string> _startNames = new Dictionary<int, string>();
        private bool _gameEnded; // 종료 알림을 받았다 — 이 뒤로는 요청을 보내지 않는다 (곧 결과 씬으로 넘어간다)
        private bool _restarting; // 방장 — 개발자 도구 「새 판」 으로 이 씬을 다시 연다. 전환 동안 판을 더 굴리지 않는다 (옛 판의 알림이 새 씬에 닿지 않게)

        // ── 방장 이주 (09-network.md 9.1) ──
        private bool _started;          // 방장 — 이 기기의 규칙이 판을 갖고 있다 (시작했거나 사본으로 되살렸다)
        private int _backupActor = -1;  // 방장 — 마지막으로 사본을 보낸 예비 방장
        private int _backupRevision;    // 방장 — 그때의 상태 변경 번호 (GameServer.Revision)
        private int _backupSentSeq;     // 방장 — 그때 사본에 실어 보낸 처리 번호. 번호만 올라도(거절 알림) 다시 보낸다
        private Player _backupPlayer;   // 방장 — 계산해 둔 예비 방장. 입장 · 퇴장 때만 다시 계산한다 (매 프레임 PlayerListOthers 를 읽지 않게)
        private bool _backupPlayerStale = true; // 입장 · 퇴장이 있었다 — 다음 SendBackup 이 예비 방장을 다시 계산한다
        private GameSnapshot _backup;   // 예비 방장 — 방장에게서 받은 가장 새 사본. 화면에는 쓰지 않는다
        private int _backupSeq;         // 예비 방장 — 그 사본을 만들 때 방장이 보낸 마지막 처리 번호

        // 사본 하나의 크기 한도 — 카드 · 사람 수로 보아 넉넉하다. 넘으면 받지 않는다
        private const int MaxBackupLength = 20000;

        // 방장이 바뀔 때 방 속성의 처리 번호를 이어받는 한도 — 남이 큰 값을 써서 새 방장의 알림을 모두 묵은 것으로 만들지 못하게
        private const int MaxSeqJump = 1000;

        /// <summary>이번 게임의 덱 — 카드 ID 로 카드 정의를 찾는 사전으로도 쓴다 (DeckData.GetCard).</summary>
        public DeckData Deck => deck;

        private void Awake()
        {
            if (deck == null)
                Debug.LogError($"[{nameof(PunGameServer)}] {nameof(deck)} 참조가 비어 있습니다.", this);

            GameClock.ResetForNewGame(); // 지난 판에 멈춘 시간을 비운다 — 이 씬의 누구도 게임 시각을 읽기 전에
            _server = CreateServer();

            if (PhotonNetwork.InRoom)
            {
                foreach (Player player in PhotonNetwork.PlayerList)
                    _startNames[player.ActorNumber] = player.NickName;
            }
        }

        // 이 씬은 방에서 LoadLevel 로 넘어오므로 방장이 바로 시작한다 (01-game-flow.md 3절).
        // 한 프레임 뒤에 — 방장 자신에게는 알림이 그 자리에서 재생되므로, 화면들의 Start(좌석 대응 · 스냅샷 복구)가 먼저 끝나야 시작 알림이 덮어쓴다.
        private IEnumerator Start()
        {
            // 새 판이다 — 지난 판의 종료 요약을 비운다 (결과 화면이 지난 판을 그리지 않게). 방장 여부를 보기 전에 — 모든 기기에서 비운다
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LastResult = null;

            if (!NetworkManager.IsTrustedMaster)
                yield break;

            yield return null;
            if (!NetworkManager.IsTrustedMaster)
                yield break;

            // 봇도 플레이어다 — 사람 뒤에 봇 번호 순으로 넣고, 턴 순서는 규칙이 섞는다 (18-bot.md 3절 #6)
            int[] bots = BotRoster.Actors;
            int[] actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).Concat(bots).ToArray();

            // 정원은 남이 바꿀 수 있다 — 2 ~ 4명이 아니면 시작하지 않고 방으로 돌아간다 (08-room.md 2절)
            if (actors.Length < NetworkManager.MinPlayers || actors.Length > NetworkManager.MaxPlayers)
            {
                Debug.LogError($"[{nameof(PunGameServer)}] 참가자 {actors.Length}명 — 2 ~ 4명이 아니라 시작하지 않는다");
                NetworkManager.Instance.UpdateRoomOptions(null, isOpen: true);
                NetworkManager.Instance.LoadSceneForAll(ScenePaths.Room);
                yield break;
            }

            List<Card> cards = deck.CreateCards();
            Debug.Log($"[{nameof(PunGameServer)}] 게임 시작 — 참가자 {string.Join(", ", actors.Select(a => $"P{a}"))} · 덱 {cards.Count}장");

            // 지난 판의 지금 값을 지운다 — 시작 일이 다시 쓰지 않는 값 (늦게 열린 화면이 지난 판 상태를 읽지 않게)
            ResetPublicProps();

            if (bots.Length > 0)
                _bots = CreateBots(bots);

            _started = true;
            IReadOnlyList<GameEvent> started = _server.StartGame(actors, cards, bots); // 봇은 시간 초과 자동 행동 · 강제 퇴장 대상이 아니다 (18-bot.md 11절)
            // 섞은 턴 순서를 첫 턴 알림보다 먼저 — 화면의 턴 순서가 좌석 순서가 아니라 이 순서를 그린다 (03-turn.md 6절 #2)
            NetworkManager.Instance.SetRoomProps(new Hashtable { [RoomProps.TurnOrder] = _server.TurnOrder.ToArray() });
            Send(started);
        }

        // 턴 마감 판정 · 봇의 판단은 방장만 한다 (09-network.md 8절 · 18-bot.md 3절 #1).
        private void Update()
        {
            if (!NetworkManager.IsTrustedMaster || _restarting)
                return;

            Send(_server.Tick());
            if (_bots != null)
            {
                Send(_bots.Tick());
                foreach (string reason in _bots.TakeLogs())
                {
                    // 봇의 이유 줄은 손패 · 함정 내용을 담는다 — 개발 빌드에서만 (CODE_CONVENTION 7절). 버리기는 늘 한다
                    if (Debug.isDebugBuild)
                        Debug.Log($"[{nameof(BotDriver)}] {reason}");
                }
            }

            SendBackup(); // 알림이 없는 변화(스킵 · 고민 연장) · 예비 방장이 바뀐 때
        }

        #region 요청 (각 클라 → 방장)

        /// <summary>카드 1장 뽑기 (메인 행동). 결과는 GameEvents 로 온다.</summary>
        public void RequestDraw() => SendRequest(nameof(RPC_RequestDraw));

        /// <summary>행동 카드 사용. 대상은 고르는 카드일 때만 쓰인다 (자동 대상이면 방장이 무시).</summary>
        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers) => SendRequest(nameof(RPC_RequestPlayCard), cardInstanceId, targetActorNumbers);

        /// <summary>함정 설치 — 내 턴 · 메인 행동 전 · 빈 슬롯 (04-card.md 6절). 메인 행동이 아니다.</summary>
        public void RequestSetTrap(int cardInstanceId) => SendRequest(nameof(RPC_RequestSetTrap), cardInstanceId);

        /// <summary>
        /// 내 손패 · 설치 함정 내용을 다시 받는다 (09-network.md 10절) — 화면이 늦게 열렸거나 다시 그릴 때. 답은 GameEvents.OnMySync.
        /// 공개 값(체력 · 장수 · 턴)은 CustomProperties 스냅샷으로 읽는다.
        /// </summary>
        public void RequestMySync() => SendRequest(nameof(RPC_RequestMySync));

        /// <summary>내가 고르는 효과의 답 (1019 목표 조준 — 0 손패 전부 버리기 · 1 피해 받기). deadline 은 답하는 고르기의 마감 (OnChoiceStarted 로 받은 값). 고르는 사람 · 시간은 방장이 본다.</summary>
        public void RequestChoose(int option, double deadline) => SendRequest(nameof(RPC_RequestChoose), option, deadline);

        /// <summary>카운터 사용 — 반응 시간 안에, 턴과 관계없이. 체인 맨 위 카드에 반응한다. 대상은 고르는 카드일 때만 쓰인다.</summary>
        public void RequestPlayCounter(int cardInstanceId, int[] targetActorNumbers) => SendRequest(nameof(RPC_RequestPlayCounter), cardInstanceId, targetActorNumbers);

        /// <summary>반응 시간 스킵 — 낼 수 있는 카운터가 있지만 내지 않는다. 답은 오지 않는다 (04-card.md 8절).</summary>
        public void RequestReactionSkip() => SendRequest(nameof(RPC_RequestReactionSkip));

        /// <summary>고민 중 — 낼 수 있는 카운터를 집었다. 반응 시간에 한 번만 보낸다. 답은 오지 않는다 (04-card.md 8절 「고민 연장」).</summary>
        public void RequestReactionHold(int cardInstanceId) => SendRequest(nameof(RPC_RequestReactionHold), cardInstanceId);

        // 요청은 믿는 방장에게 보낸다 — 「지금 방장」 은 아무 클라나 가져갈 수 있다 (09-network.md 13절)
        private void SendRequest(string rpcName, params object[] parameters)
        {
            if (_gameEnded)
                return; // 끝난 판에는 어차피 거절(코드 13)이다 — 씬이 바뀐 뒤 도착해 받을 곳이 없는 요청을 만들지 않는다

            Player master = PhotonNetwork.CurrentRoom?.GetPlayer(NetworkManager.TrustedMaster);
            if (master == null)
            {
                Debug.LogWarning($"[{nameof(PunGameServer)}] 믿는 방장이 없다 — {rpcName} 요청을 보내지 않는다");
                return;
            }

            photonView.RPC(rpcName, master, parameters);
        }

        [PunRPC]
        private void RPC_RequestDraw(PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.Draw(info.Sender.ActorNumber));
        }

        [PunRPC]
        private void RPC_RequestPlayCard(int cardInstanceId, int[] targetActorNumbers, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.PlayCard(info.Sender.ActorNumber, cardInstanceId, targetActorNumbers));
        }

        [PunRPC]
        private void RPC_RequestSetTrap(int cardInstanceId, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.SetTrap(info.Sender.ActorNumber, cardInstanceId));
        }

        [PunRPC]
        private void RPC_RequestMySync(PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;

            // 지금까지 보낸 알림 뒤에 간다 — 받는 쪽은 이 번호 전의 묵은 알림을 버린다
            _server.GetPrivateCards(info.Sender.ActorNumber, out int[] handIds, out int[] handCardIds, out int[] trapIds, out int[] trapCardIds);
            SendTo(info.Sender.ActorNumber, nameof(RPC_OnMySync), handIds, handCardIds, trapIds, trapCardIds);
            NetworkManager.Instance.SetRoomProps(new Hashtable { [RoomProps.EventSeq] = _sentSeq }); // 새 방장이 이 번호부터 잇게 (09-network.md 5절)
        }

        [PunRPC]
        private void RPC_RequestChoose(int option, double deadline, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.Choose(info.Sender.ActorNumber, option, deadline));
        }

        [PunRPC]
        private void RPC_RequestPlayCounter(int cardInstanceId, int[] targetActorNumbers, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.PlayCounter(info.Sender.ActorNumber, cardInstanceId, targetActorNumbers));
        }

        [PunRPC]
        private void RPC_RequestReactionSkip(PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.SkipReaction(info.Sender.ActorNumber));
        }

        [PunRPC]
        private void RPC_RequestReactionHold(int cardInstanceId, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            Send(_server.HoldReaction(info.Sender.ActorNumber, cardInstanceId));
        }

        /// <summary>개발자 도구 (10-ui.md 12절) — 개발 방(매치가 아닌 방)에서만 방장이 받는다. value 는 명령마다 (켜기 1 · 끄기 0 · 카드 종류 ID).</summary>
        public void RequestDev(DevCommand command, int value) => SendRequest(nameof(RPC_RequestDev), (int)command, value);

        public enum DevCommand
        {
            UnlimitedTurnTime = 1,
            SeatBot = 2,
            GiveCard = 3,
            NewGame = 4,
        }

        [PunRPC]
        private void RPC_RequestDev(int command, int value, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            // 매치 방(정식 판)에서는 받지 않는다 — 방 속성은 남이 쓸 수 있어 방장이 쓴 원본을 본다
            if (NetworkManager.RoomProp(RoomProps.Match) is bool match && match)
            {
                Debug.LogWarning($"[{nameof(PunGameServer)}] 매치 방 — P{info.Sender.ActorNumber} 의 개발자 도구 요청 {command} 를 받지 않는다");
                return;
            }

            int actor = info.Sender.ActorNumber;
            switch ((DevCommand)command)
            {
                case DevCommand.UnlimitedTurnTime:
                    Send(_server.SetUnlimitedTurnTime(value != 0));
                    break;
                case DevCommand.SeatBot:
                    if (!_started) return;
                    _bots ??= CreateBots(System.Array.Empty<int>());
                    _bots.SetDriven(actor, value != 0);
                    break;
                case DevCommand.GiveCard:
                    Send(_server.DevGiveCard(actor, value));
                    break;
                case DevCommand.NewGame:
                    if (_gameEnded || _restarting) return;
                    _gameEnded = true; // 이 판의 요청을 멈춘다 — 같은 씬을 다시 연다
                    _restarting = true;
                    ResetPublicProps();
                    NetworkManager.Instance.LoadSceneForAll(SceneFlow.GameSceneAfterRoom); // 결과 화면의 「한 판 더」 와 같은 씬
                    break;
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            RoomEvents.OnBecameTrustedMaster += TakeOver;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            RoomEvents.OnBecameTrustedMaster -= TakeOver;
        }

        #region 방장 이주 (09-network.md 9.1)

        // 예비 방장 = 방장이 사라지면 Photon 이 방장으로 세울 사람 — 방에 남은 사람 중 번호가 가장 작은 사람. 없으면 null
        private static Player BackupPlayer()
        {
            Player backup = null;
            foreach (Player player in PhotonNetwork.PlayerListOthers)
            {
                if (!player.IsInactive && (backup == null || player.ActorNumber < backup.ActorNumber))
                    backup = player;
            }
            return backup;
        }

        // 방장 — 상태가 바뀌었거나 예비 방장이 바뀌었으면 상태 전체를 예비 방장 한 명에게만 보낸다. 손패 · 함정 · 덱 순서가 들어 있다 —
        // 그 기기의 화면은 이 값을 쓰지 않는다. 방 속성에는 두지 않는다 (전원이 읽는다)
        private void SendBackup()
        {
            if (!_started || _gameEnded)
                return;

            if (_backupPlayerStale)
            {
                _backupPlayer = BackupPlayer();
                _backupPlayerStale = false;
            }

            Player backup = _backupPlayer;
            if (backup == null)
            {
                _backupActor = -1;
                return;
            }

            if (backup.ActorNumber == _backupActor && _server.Revision == _backupRevision && _sentSeq == _backupSentSeq)
                return;

            GameSnapshot snapshot = _server.Capture();
            _backupActor = backup.ActorNumber;
            _backupRevision = _server.Revision;
            _backupSentSeq = _sentSeq;
            photonView.RPC(nameof(RPC_OnBackup), backup, _sentSeq, snapshot.Ints, snapshot.Times);
        }

        [PunRPC]
        private void RPC_OnBackup(int sentSeq, int[] ints, double[] times, PhotonMessageInfo info)
        {
            if (info.Sender == null || info.Sender.ActorNumber != NetworkManager.TrustedMaster)
                return;
            if (ints == null || times == null || ints.Length > MaxBackupLength || times.Length > MaxBackupLength)
                return;

            _backup = new GameSnapshot(ints, times);
            _backupSeq = sentSeq;
        }

        // 내가 믿는 방장이 됐다 — 받아 둔 사본으로 판을 되살려 멈췄던 단계부터 잇는다. 사본이 없거나 읽을 수 없으면 판을 중지하고 방으로 돌아간다.
        private void TakeOver()
        {
            InheritSeq();
            if (_gameEnded || _started)
                return; // 끝난 판은 결과 씬으로 옮기기만 하면 된다 (GoToResultRoutine)

            _sentSeq = System.Math.Max(_sentSeq, _backupSeq); // 사본에 함께 온 번호 — 되살리지 못해 중지 알림만 보낼 때도 잇는다
            GameSnapshot backup = _backup;
            _backup = null;
            var server = CreateServer();
            if (backup == null || !server.Restore(backup, deck.CreateCards()))
            {
                Debug.LogError($"[{nameof(PunGameServer)}] 방장 이주 실패 — {(backup == null ? "받아 둔 사본이 없다" : server.RestoreError)}. 판을 중지하고 방으로 돌아간다");
                Halt();
                return;
            }

            _server = server;
            _started = true;

            int[] bots = BotRoster.Actors;
            _bots = bots.Length > 0
                ? CreateBots(bots)
                : null;

            // 사본 뒤에 나간 사람 (앞 방장 포함) — 나감 알림은 앞 방장만 받았다. 그 사람 앞으로는 단계를 다시 알리지 않는다
            int[] departed = _startNames.Keys.Where(actor =>
            {
                Player player = PhotonNetwork.CurrentRoom.GetPlayer(actor);
                return player == null || player.IsInactive;
            }).ToArray();

            Debug.Log($"[{nameof(PunGameServer)}] 방장 이주 — 사본으로 판을 이어받았다 (처리 번호 {_sentSeq})");
            Send(_server.ResumeAfterMigration(departed));

            foreach (int actor in departed)
                Send(_server.RemovePlayer(actor));

            // 각자의 손패 · 함정을 방장 값으로 맞춘다 — 사본이 마지막 알림보다 한 걸음 늦었을 수 있다
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                _server.GetPrivateCards(player.ActorNumber, out int[] handIds, out int[] handCardIds, out int[] trapIds, out int[] trapCardIds);
                SendTo(player.ActorNumber, nameof(RPC_OnMySync), handIds, handCardIds, trapIds, trapCardIds);
            }
            NetworkManager.Instance.SetRoomProps(new Hashtable { [RoomProps.EventSeq] = _sentSeq });
        }

        // 시작 경로(Awake)와 이주 경로(TakeOver)가 함께 쓴다 — 한쪽만 바뀌어 이주한 판만 다르게 움직이지 않게
        private static GameServer CreateServer()
        {
            return new GameServer(new SecureRandom(), () => GameClock.Now); // 덱 순서를 나온 카드로 맞추지 못하게 (09-network.md 9.1)
        }

        private BotDriver CreateBots(int[] bots)
        {
            return new BotDriver(_server, bots, new System.Random(), () => GameClock.Now, bot => BotRoster.LevelOf(bot - BotRoster.ActorBase));
        }

        // 판을 이어 갈 수 없다 — 전원에게 알리고 방(대기 화면)으로 돌아간다. 거기서 다시 시작할 수 있다 (09-network.md 9.1, PM 2026-11-19)
        private void Halt()
        {
            Broadcast(nameof(RPC_OnGameHalted));
            ResetPublicProps(); // 방에서 다시 시작한 판의 화면이 중지된 판의 값을 읽지 않게 — 게임 씬을 떠나기 전에 지운다 (GoToResultRoutine 과 같은 순서)
            NetworkManager.Instance.UpdateRoomOptions(null, isOpen: true);
            NetworkManager.Instance.LoadSceneForAll(ScenePaths.Room);
        }

        [PunRPC]
        private void RPC_OnGameHalted(int seq, PhotonMessageInfo info)
        {
            // 처리 번호로 거르지 않는다 — 사본 없이 이어받은 새 방장은 앞 방장이 누구에게 몇 번까지 보냈는지(거절 알림) 모른다.
            // 번호가 겹쳐 중지 알림을 묵은 것으로 버리면 그 사람만 경고 없이 방으로 옮겨진다. 믿는 방장이 보냈는지만 본다 —
            // 이 알림은 판의 마지막 알림이고 여러 번 받아도 결과가 같다 (09-network.md 9.1 #11)
            if (info.Sender == null || info.Sender.ActorNumber != NetworkManager.TrustedMaster)
                return;

            _gameEnded = true; // 이 뒤로는 요청을 보내지 않는다
            GameEvents.RaiseGameHalted();
        }

        #endregion

        // 새 믿는 방장은 이어서 번호를 매긴다 — 아니면 클라들이 새 알림을 묵은 것으로 버린다.
        // 방장 자리만 가져간 사람은 이어받지 않는다 — 믿는 방장이 될 때 이어받는다 (09-network.md 13절 #1)
        private void InheritSeq()
        {
            int known = System.Math.Max(_sentSeq, _receivedSeq);
            int fromRoom = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.EventSeq, out object value) && value is int seq ? seq : 0;
            if (fromRoom > known + MaxSeqJump)
            {
                Debug.LogWarning($"[{nameof(PunGameServer)}] 방 속성의 처리 번호 {fromRoom} 이 받은 번호 {known} 보다 너무 크다 — 쓰지 않는다");
                fromRoom = 0;
            }
            _sentSeq = System.Math.Max(known, fromRoom);
        }

        // 나간 사람은 최종 사망과 같이 — 턴 순서 · 대상 후보에서 빼고 손패를 버림 더미로 (01-game-flow.md 6절).
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            _backupPlayerStale = true;
            if (!NetworkManager.IsTrustedMaster) return;
            Send(_server.RemovePlayer(otherPlayer.ActorNumber));
        }

        // 다시 들어온 사람이 예비 방장이 될 수 있다 — 다음 SendBackup 이 다시 계산한다
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            _backupPlayerStale = true;
        }

        #endregion

        #region 결과 보내기 (방장)

        // 일어난 일을 순서대로 받을 사람에게 보낸다. 같은 보낸이의 RPC 는 보낸 순서대로 도착한다.
        // 지금 값 스냅샷(CustomProperties)은 늦게 들어온 · 복구용이라 묶어서 마지막에 한 번 쓴다 (09-network.md 6.1).
        private void Send(IReadOnlyList<GameEvent> events)
        {
            if (events.Count == 0)
                return;

            _bots?.Observe(events); // 봇도 판에서 일어난 일을 본다 — 누가 자기를 노렸는지 (18-bot.md 7-8)

            var roomProps = new Hashtable();
            var playerProps = new Dictionary<int, Hashtable>();
            bool onlyRejected = true; // 거절만 있으면 지금 값이 바뀌지 않았다 — 스냅샷을 방 전원에게 쓰지 않는다

            for (int i = 0; i < events.Count; i++)
            {
                GameEvent gameEvent = events[i];
                onlyRejected &= gameEvent is RequestRejected;
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
                        Debug.Log($"[{nameof(PunGameServer)}] 턴 시작 P{turn.Actor} — {_server.DebugState(Debug.isDebugBuild)}");
                        break;

                    case CardPlayed played:
                        Broadcast(nameof(RPC_OnCardPlayed), played.Actor, played.CardInstanceId, played.CardId, played.Targets, played.HandCount, played.DiscardCount);
                        PlayerPropsOf(playerProps, played.Actor)[PlayerProps.HandCount] = played.HandCount;
                        break;

                    case ReactionStarted reaction:
                        Broadcast(nameof(RPC_OnReactionStarted), reaction.OpensAt); // 마감은 보내지 않는다 (09-network.md 3절)
                        SendCountersUsable(events, i + 1);
                        break;

                    case CountersUsable _:
                        break; // 바로 앞 ReactionStarted 에서 함께 보냈다 (SendCountersUsable)

                    case CardResolved resolved:
                        Broadcast(nameof(RPC_OnCardResolved), resolved.Actor, resolved.CardInstanceId, resolved.CardId, resolved.Targets, resolved.DiscardCount);
                        break;

                    case CardNegated negated:
                        Broadcast(nameof(RPC_OnCardNegated), negated.Actor, negated.CardInstanceId, negated.CardId, negated.DiscardCount);
                        break;

                    case EmptyDeckDamaged empty:
                        Broadcast(nameof(RPC_OnEmptyDeckDamaged), empty.Actor, empty.Amount);
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
                        Broadcast(nameof(RPC_OnPlayerLeftGame), left.Actor, left.CardIds, left.DiscardCount, left.Forced);
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

                    case ChainDiscarded chainDiscarded:
                        Broadcast(nameof(RPC_OnChainDiscarded), chainDiscarded.CardInstanceIds, chainDiscarded.CardIds, chainDiscarded.DiscardCount);
                        roomProps[RoomProps.DiscardCount] = chainDiscarded.DiscardCount;
                        SetDiscardTop(roomProps, chainDiscarded.CardIds);
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

                    case TurnSkipCleared skipCleared:
                        Broadcast(nameof(RPC_OnTurnSkipCleared), skipCleared.Actor);
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
                        roomProps[RoomProps.TurnOrder] = restarted.Order; // 새 판은 순서를 다시 섞는다
                        roomProps[RoomProps.WaitCasters] = null;
                        Debug.Log($"[{nameof(PunGameServer)}] 대폭발 — 새 판 ({restarted.Count}번째) · 순서 {string.Join(" → ", restarted.Order.Select(a => $"P{a}"))}");
                        break;

                    case FalloutChanged fallout:
                        Broadcast(nameof(RPC_OnFalloutChanged), fallout.N, fallout.Turns);
                        roomProps[RoomProps.Fallout] = fallout.N > 0 ? new[] { fallout.N, fallout.Turns } : null;
                        break;

                    case GaepanChanged gaepan:
                        Broadcast(nameof(RPC_OnGaepanChanged), gaepan.Caster, gaepan.Left, gaepan.Again);
                        roomProps[RoomProps.Gaepan] = gaepan.Left > 0 ? new[] { gaepan.Caster, gaepan.Left } : null;
                        break;

                    case ChoiceStarted choice:
                        Broadcast(nameof(RPC_OnChoiceStarted), choice.Actor, choice.User, choice.CardId, choice.HandCount, choice.Damage, choice.StartsAt, choice.Deadline, choice.Kind);
                        break;

                    case ChoiceMade made:
                        Broadcast(nameof(RPC_OnChoiceMade), made.Actor, made.Option, made.TimedOut, made.Kind);
                        Debug.Log($"[{nameof(PunGameServer)}] P{made.Actor} 고르기 — {(made.Option < 0 ? "나가서 불발" : made.Kind == ChoiceKind.HandCard ? "버릴 카드 1장" : made.Option == 0 ? "손패 전부 버리기" : "피해 받기")}{(made.TimedOut ? " (시간 초과 무작위)" : "")}");
                        break;

                    case GaepanJudged judged:
                        Broadcast(nameof(RPC_OnGaepanJudged), judged.Loser, judged.ByHp, judged.Value, judged.Tied, judged.Actors, judged.Values);
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
                        Broadcast(nameof(RPC_OnGameEnded), ended.Winner, (byte)ended.Reason, ended.Actors, ended.Ranks, ended.Hps, ended.OutTurns, ended.DownedBy, ended.Left, ended.Stats, ended.EndTurn);
                        Debug.Log($"[{nameof(PunGameServer)}] 게임 종료 — {ended.Reason} · 승자 P{ended.Winner} — {_server.DebugState(Debug.isDebugBuild)}");

                        // 결과 화면의 상태를 새로 쓴다 — 지난 판의 마감 · 준비가 남아 결과 씬이 열리자마자 로비로 나가거나 바로 시작하지 않게 (07 11-1)
                        roomProps[RoomProps.ResultDeadline] = PhotonNetwork.Time + resultDelaySeconds + PunResultServer.AutoLeaveSeconds;
                        roomProps[RoomProps.ResultReady] = null;
                        roomProps[RoomProps.ResultStartAt] = null;
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

            if (onlyRejected)
            {
                // 거절 알림도 처리 번호를 올린다 — 방 속성에는 쓰지 않고(09-network.md 13절 #6) 예비 방장에게만 사본과 함께 전한다 (9.1 흐름 2).
                // 봇의 거절은 알림이 나가지 않아 번호가 그대로라 다시 보내지 않는다
                SendBackup();
                return;
            }

            roomProps[RoomProps.EventSeq] = _sentSeq; // 지금 값 스냅샷이 어느 알림까지 반영했는지
            NetworkManager.Instance.SetRoomProps(roomProps);
            SendBackup(); // 알림과 같은 묶음으로 나가게 — 알림만 가고 사본이 안 간 채 방장이 사라지는 틈을 줄인다

            foreach (KeyValuePair<int, Hashtable> pair in playerProps)
            {
                if (!BotRoster.IsBot(pair.Key))
                    NetworkManager.Instance.SetPlayerProps(PhotonNetwork.CurrentRoom.GetPlayer(pair.Key), pair.Value);
            }
        }

        // 버림 더미에 마지막으로 들어간 카드가 맨 위다. 들어간 카드가 없으면 맨 위는 그대로.
        private static void SetDiscardTop(Hashtable roomProps, int[] discardedCardIds)
        {
            if (discardedCardIds.Length > 0)
                roomProps[RoomProps.DiscardTop] = discardedCardIds[discardedCardIds.Length - 1];
        }

        // 게임이 끝나면 잠깐 뒤 방장이 전원을 결과 씬으로 옮긴다 (07-win-condition.md 11-1). 종료 알림을 받은 모든 클라가 돌린다 —
        // 그 사이에 방장이 나가면 새 믿는 방장이 이어서 한다. 씬이 바뀌면 이 오브젝트와 함께 끝난다.
        private IEnumerator GoToResultRoutine()
        {
            yield return new WaitForSeconds(resultDelaySeconds);
            yield return new WaitUntil(() => NetworkManager.IsTrustedMaster);

            // 다음 판 화면들의 Start(스냅샷 복구)는 방장의 시작 초기화보다 먼저 읽는다 — 그래서 게임 씬을 떠나기 전에 지운다.
            // 「한 판 더」 로 시작할 때 지우면 늦다: 방장은 자기가 쓴 속성을 서버 응답 뒤에야 보는데, 씬을 여는 동안은 받기가 멈춘다.
            // 결과 화면은 이 값이 아니라 종료 요약(GameResult)을 읽는다.
            ResetPublicProps();
            NetworkManager.Instance.LoadSceneForAll(ScenePaths.Result);
        }

        // 판마다 새로 시작하는 공개 값(지금 값 스냅샷)을 처음으로 — 시작 일이 다시 쓰지 않는 값까지. 값이 null 이면 Photon 이 키를 지운다.
        // 찹츄 · 함정 · 손패 장수: 게임 종료는 찹츄를 해제하지 않고, 시작 일은 이 값을 다시 보내지 않는다.
        // HP 는 시작 값으로 — 다음 판 화면이 지난 판 HP 를 보이거나 시작 HP 를 변화로 기록하지 않게.
        // 턴 주인 · 마감 · 덱 장수: 다음 판 화면이 열리자마자 지난 판의 턴을 그리지 않게. 봇은 방 속성 키 (18-bot.md 9절)
        private static void ResetPublicProps()
        {
            var playerReset = new Hashtable
            {
                [PlayerProps.LifeState] = (byte)LifeState.Alive, [PlayerProps.ChapChu] = false, [PlayerProps.TrapCount] = 0,
                [PlayerProps.HandCount] = 0, [PlayerProps.Hp] = GameServer.MaxHp, [PlayerProps.ActionBlock] = null,
            };
            var roomReset = new Hashtable
            {
                [RoomProps.DiscardCount] = 0, [RoomProps.DiscardTop] = null, [RoomProps.TurnDirection] = null, [RoomProps.WaitCasters] = null,
                [RoomProps.Fallout] = null, [RoomProps.Gaepan] = null, [RoomProps.TurnActor] = null, [RoomProps.TurnOrder] = null, [RoomProps.TurnDeadline] = null, [RoomProps.DeckCount] = null,
                [RoomProps.EventSeq] = null, // 처리 번호는 판마다 1부터 — 지난 판 번호를 새 방장이 이어받지 않게
            };
            foreach (int bot in BotRoster.Actors)
            {
                foreach (DictionaryEntry entry in playerReset)
                    roomReset[BotRoster.PropKey((string)entry.Key, bot)] = entry.Value;
            }
            NetworkManager.Instance.SetRoomProps(roomReset);

            foreach (Player player in PhotonNetwork.PlayerList)
                NetworkManager.Instance.SetPlayerProps(player, playerReset);
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

        // 반응 시간마다 「낼 수 있는 내 카운터」 를 방의 모두에게 보낸다 — 없는 사람은 빈 목록 (09-network.md 3 · 5절).
        // 가진 사람에게만 보내면 나머지 사람의 처리 번호가 비어, 누가 카운터를 쥐었는지 셀 수 있다 (04-card 결정 #8).
        // 방장은 ReactionStarted 바로 뒤에 CountersUsable 을 가진 사람마다 붙여 낸다 (GameServer.AddCountersUsable).
        private void SendCountersUsable(IReadOnlyList<GameEvent> events, int from)
        {
            var usable = new Dictionary<int, int[]>();
            for (int i = from; i < events.Count && events[i] is CountersUsable counters; i++)
                usable[counters.Actor] = counters.CardInstanceIds;

            foreach (Player player in PhotonNetwork.PlayerList)
                SendTo(player.ActorNumber, nameof(RPC_OnMyCountersUsable),
                    usable.TryGetValue(player.ActorNumber, out int[] ids) ? ids : System.Array.Empty<int>());
        }

        private object[] WithSeq(object[] parameters)
        {
            var withSeq = new object[parameters.Length + 1];
            withSeq[0] = ++_sentSeq;
            parameters.CopyTo(withSeq, 1);
            return withSeq;
        }

        // 받는 쪽 — 같은 보낸이의 RPC 는 순서대로 오므로 번호는 늘기만 한다. 이하가 오면 이미 반영한 것 (동기화 응답 뒤의 묵은 알림 등)
        // 결과는 믿는 방장이 보낸 것만 반영한다 (09-network.md 2절 #3 · 13절) — 아니면 다른 클라가 번호를 올려 진짜 알림을 막을 수 있다.
        // 「지금 방장」 이 아니라 믿는 방장 — 아무 클라나 방장 자리를 가져갈 수 있다(SetMasterClient)
        private bool Accept(int seq, PhotonMessageInfo info)
        {
            if (info.Sender == null || info.Sender.ActorNumber != NetworkManager.TrustedMaster)
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

        // 결과의 짝 배열(인스턴스 ID · 카드 ID 등)은 길이가 같아야 한다 — 화면이 같은 자리로 읽는다
        private static bool Paired(System.Array a, System.Array b)
        {
            if (a != null && b != null && a.Length == b.Length)
                return true;

            Debug.LogWarning($"[{nameof(PunGameServer)}] 길이가 맞지 않는 결과 — 버린다");
            return false;
        }

        private static int[] Ints(int[] values) => values ?? System.Array.Empty<int>();

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
                GameEvents.RaiseCardPlayed(actorNumber, cardInstanceId, cardId, Ints(targets), handCount, discardCount);
        }

        [PunRPC]
        private void RPC_OnReactionStarted(int seq, double opensAt, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseReactionStarted(opensAt);
        }

        [PunRPC]
        private void RPC_OnMyCountersUsable(int seq, int[] cardInstanceIds, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyCountersUsable(Ints(cardInstanceIds));
        }

        [PunRPC]
        private void RPC_OnCardResolved(int seq, int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseCardResolved(actorNumber, cardInstanceId, cardId, Ints(targets), discardCount);
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
        private void RPC_OnPlayerDied(int seq, int actorNumber, int[] cardIds, int discardCount, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaisePlayerDied(actorNumber, Ints(cardIds), discardCount);
        }

        [PunRPC]
        private void RPC_OnPlayerLeftGame(int seq, int actorNumber, int[] cardIds, int discardCount, bool forced, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaisePlayerLeftGame(actorNumber, Ints(cardIds), discardCount, forced);
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
                GameEvents.RaiseTrapActivated(actorNumber, cardInstanceId, cardId, Ints(targets), trapCount);
        }

        [PunRPC]
        private void RPC_OnChainDiscarded(int seq, int[] cardInstanceIds, int[] cardIds, int discardCount, PhotonMessageInfo info)
        {
            if (Paired(cardInstanceIds, cardIds) && Accept(seq, info))
                GameEvents.RaiseChainDiscarded(cardInstanceIds, cardIds, discardCount);
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
            if (Paired(cardInstanceIds, cardIds) && Accept(seq, info))
                GameEvents.RaiseMyCardsGained(cardInstanceIds, cardIds);
        }

        [PunRPC]
        private void RPC_OnMyCardsLost(int seq, int[] cardInstanceIds, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseMyCardsLost(Ints(cardInstanceIds));
        }

        [PunRPC]
        private void RPC_OnCardsDiscarded(int seq, int actorNumber, int[] cardInstanceIds, int[] cardIds, int handCount, int discardCount, PhotonMessageInfo info)
        {
            if (Paired(cardInstanceIds, cardIds) && Accept(seq, info))
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
        private void RPC_OnTurnSkipCleared(int seq, int actorNumber, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseTurnSkipCleared(actorNumber);
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
                GameEvents.RaiseWaitChanged(Ints(casters));
        }

        [PunRPC]
        private void RPC_OnGameRestarted(int seq, int user, int count, int[] order, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseGameRestarted(user, count, Ints(order));
        }

        [PunRPC]
        private void RPC_OnFalloutChanged(int seq, int n, int turns, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseFalloutChanged(n, turns);
        }

        [PunRPC]
        private void RPC_OnChoiceStarted(int seq, int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline, int kind, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseChoiceStarted(actor, user, cardId, handCount, damage, startsAt, deadline, kind);
        }

        [PunRPC]
        private void RPC_OnChoiceMade(int seq, int actor, int option, bool timedOut, int kind, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseChoiceMade(actor, option, timedOut, kind);
        }

        [PunRPC]
        private void RPC_OnGaepanChanged(int seq, int caster, int left, bool again, PhotonMessageInfo info)
        {
            if (Accept(seq, info))
                GameEvents.RaiseGaepanChanged(caster, left, again);
        }

        [PunRPC]
        private void RPC_OnGaepanJudged(int seq, int loser, bool byHp, int value, int[] tied, int[] actors, int[] values, PhotonMessageInfo info)
        {
            if (Paired(actors, values) && Accept(seq, info))
                GameEvents.RaiseGaepanJudged(loser, byHp, value, Ints(tied), actors, values);
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
        private void RPC_OnGameEnded(int seq, int winner, byte reason, int[] actors, int[] ranks, int[] hps, int[] outTurns, int[] downedBy, bool[] left, int[] stats, int endTurn, PhotonMessageInfo info)
        {
            if (!Paired(actors, ranks) || !Paired(actors, hps) || !Paired(actors, outTurns) || !Paired(actors, downedBy) || !Paired(actors, left) || !Accept(seq, info))
                return;

            // 지표는 사람마다 StatCount 개 (07 11-4)
            if (stats == null || stats.Length != actors.Length * GameServer.StatCount)
            {
                Debug.LogWarning($"[{nameof(PunGameServer)}] 길이가 맞지 않는 지표 — 종료 요약을 버린다");
                return;
            }

            // 이름은 각자 게임을 시작할 때 찍어 둔 것 — 나간 사람은 지금 방에 없다. 봇은 「봇 N」
            string[] names = actors.Select(actor => BotRoster.IsBot(actor) ? BotRoster.NicknameOf(actor) : _startNames.TryGetValue(actor, out string name) ? name : "").ToArray();
            NetworkManager.Instance.LastResult = new GameResult(winner, (GameEndReason)reason, actors, names, ranks, hps, outTurns, downedBy, left, stats, endTurn);

            _gameEnded = true;
            GameEvents.RaiseGameEnded(winner, (GameEndReason)reason);
            StartCoroutine(GoToResultRoutine());
        }

        [PunRPC]
        private void RPC_OnMySync(int seq, int[] handIds, int[] handCardIds, int[] trapIds, int[] trapCardIds, PhotonMessageInfo info)
        {
            if (Paired(handIds, handCardIds) && Paired(trapIds, trapCardIds) && Accept(seq, info))
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
