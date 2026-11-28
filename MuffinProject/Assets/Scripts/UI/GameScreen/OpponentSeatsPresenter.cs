using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.Fx;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 좌석 3개에 값을 넣는다 (10-ui.md 2-1 「상대 좌석」 · 02-player.md 3절). 봇도 사람과 같이 앉는다 (18-bot.md 8-2). 좌석 인덱스 ↔ actorNumber 는 여기서만 계산한다 —
    /// 목걸이 색(PlayerLook) 등 다른 UI 도 이 결과를 쓴다. HP · 손패 수 · 턴은 GameScreenPresenter 가 연출 큐 순서로 넘긴다.
    /// 퇴장은 RoomEvents.OnPlayerLeft 로 받아 좌석을 숨긴다 (02-player.md 6절) — 게임 이벤트가 아니라 큐 밖.
    /// 늦게 열렸을 때는 Start 에서 각 Player 의 CustomProperties 와 방의 턴 주인을 한 번 읽는다 (09-network.md 6.1).
    /// 대상 선택(10-ui.md 6절)도 여기서 — 좌석에 빨간 고리 · 테를 켜고 안내 바(TargetAskView)를 띄운 뒤 탭을 모아 actorNumber 로 돌려준다.
    /// 나를 포함하는 카드(OneAny)는 내 패널도 빨간 테 + 탭을 받는다.
    /// 취소 = 안내 바의 「취소」 버튼(모바일 포함) · PC 우클릭 · Esc · 내 턴이 끝남. 제한 시간은 없다 (턴 제한 시간(GameServer.TurnTimeLimit, 03-turn.md 2절)이 유일한 타이머).
    /// </summary>
    public class OpponentSeatsPresenter : MonoBehaviour
    {
        /// <summary>상대 좌석 수 = 정원 - 나 (02-player.md 3절). 좌석 인덱스 1 · 2 · 3. GameScreenView 도 이 값을 쓴다.</summary>
        public const int SeatCount = NetworkManager.MaxPlayers - 1;
        private const string BotNicknameFormat = "{0} (봇)"; // 봇 좌석은 닉네임 옆에 「(봇)」 (18-bot.md 8-2)

        [Tooltip("좌석 인덱스 1 · 2 · 3 순서 (02-player.md 3절). 빈 좌석은 숨긴다.")]
        [SerializeField] private OpponentSeatView[] seats = new OpponentSeatView[SeatCount];
        [Tooltip("좌석마다의 함정 칸 3개 (3D 층) — seats 와 같은 순서. 빈 좌석의 칸은 숨긴다.")]
        [SerializeField] private TrapSlotsView[] trapSlots = new TrapSlotsView[SeatCount];
        [Tooltip("대상 선택 안내 바 — 「카드 · 누구에게 쓸까요?」 + 취소.")]
        [SerializeField] private TargetAskView targetAsk;
        [Tooltip("내 패널 — 나를 포함하는 카드(OneAny)의 대상 선택 때 나를 고르는 자리.")]
        [SerializeField] private MyPanelView myPanel;
        [Tooltip("상대가 뽑은 뒷면을 꺼낸 뒤 다음 일로 넘어가기까지 초. 내 손패(HandPresenter.dealInterval)와 같게 — 첫 분배 때 한 장씩 날아온다.")]
        [SerializeField] private float dealInterval = 0.15f;

        [Header("캐릭터 모습")]
        [Tooltip("사람의 캐릭터 그림 — 기본 캐릭터 (19-character-items 「기본 캐릭터 1종」).")]
        [SerializeField] private Sprite defaultBody;
        [Tooltip("봇 번호 1 · 2 · 3 의 모습 — 사람과도, 봇끼리도 달라 보이게 (18-bot.md 8-2). 매치 방은 쓰지 않는다.")]
        [SerializeField] private CharacterLook[] botLooks = new CharacterLook[3];

        /// <summary>캐릭터 모습 한 벌 — 몸 그림 + 위에 겹치는 꾸미기(없어도 된다). 두 그림은 같은 크기 · 같은 자리로 그린 원본이다.</summary>
        [Serializable]
        public struct CharacterLook
        {
            public Sprite body;
            public Sprite accessory;
        }

        private readonly Dictionary<int, OpponentSeatView> _seatOf = new Dictionary<int, OpponentSeatView>(); // actorNumber → 좌석
        private readonly Dictionary<int, TrapSlotsView> _trapsOf = new Dictionary<int, TrapSlotsView>(); // actorNumber → 함정 칸
        private readonly List<int> _picked = new List<int>(); // 대상 선택 중 고른 actorNumber
        private bool _selecting;
        private bool _selectCancelled;
        private int _selectCount;       // 이번 선택에서 고를 수
        private bool _selectIncludeMe;  // 이번 선택에서 나도 고를 수 있다
        private int _aimedActor; // 끄는 카드가 겨눈 상대 (없으면 0)

        /// <summary>
        /// actorNumber 의 캐릭터 모습 — 봇은 봇 번호의 모습, 사람은 기본 캐릭터. 좌석 · 턴 순서가 같이 쓴다.
        /// 매치 방의 봇은 사람과 똑같이 보여야 해서 기본 캐릭터 (18-bot.md 2절 #11).
        /// </summary>
        public CharacterLook LookOf(int actorNumber)
        {
            if (BotRoster.IsBot(actorNumber) && !BotRoster.IsMatchRoom)
            {
                int index = actorNumber - BotRoster.ActorOf(1);
                if (index >= 0 && index < botLooks.Length && botLooks[index].body != null)
                    return botLooks[index];
            }
            return new CharacterLook { body = defaultBody };
        }

        /// <summary>대상 선택 중인가 — 그동안 손패는 잠긴다 (HandPresenter).</summary>
        public bool IsSelectingTargets => _selecting;

        private void Awake()
        {
            if (seats == null || seats.Length != SeatCount || seats.Any(seat => seat == null))
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] {nameof(seats)} 는 {SeatCount}개가 모두 연결돼야 합니다.", this);
            if (trapSlots == null || trapSlots.Length != SeatCount || trapSlots.Any(slots => slots == null))
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] {nameof(trapSlots)} 는 {SeatCount}개가 모두 연결돼야 합니다.", this);
            if (targetAsk == null)
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] {nameof(targetAsk)} 참조가 비어 있습니다.", this);
            if (myPanel == null)
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] {nameof(myPanel)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
            targetAsk.CancelClicked += HandleCancelClicked;
            myPanel.OnClicked += HandleMeClicked;
            foreach (OpponentSeatView seat in seats)
                seat.OnClicked += HandleSeatClicked;
        }

        private void OnDisable()
        {
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
            targetAsk.CancelClicked -= HandleCancelClicked;
            myPanel.OnClicked -= HandleMeClicked;
            foreach (OpponentSeatView seat in seats)
                seat.OnClicked -= HandleSeatClicked;
        }

        private void Start()
        {
            targetAsk.Hide();
            myPanel.SetTargetable(false);
            foreach (OpponentSeatView seat in seats)
                seat.gameObject.SetActive(false);
            foreach (TrapSlotsView slots in trapSlots)
                slots.gameObject.SetActive(false);

            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때 — 좌석 없음

            int[] opponents = PlayerLook.OrderedActors().Skip(1).ToArray(); // 나를 뺀 좌석 순서 — 사람 뒤에 봇 (18-bot.md 8-3)
            if (opponents.Length > SeatCount)
            {
                // 정원은 남이 바꿀 수 있다 — 방장이 시작하지 않지만(PunGameServer), 화면은 앉힐 수 있는 만큼만 앉힌다
                Debug.LogWarning($"[{nameof(OpponentSeatsPresenter)}] 상대 {opponents.Length}명 — 앞의 {SeatCount}명만 앉힌다", this);
                opponents = opponents.Take(SeatCount).ToArray();
            }

            int[] seatIndices = GetOpponentSeatIndices(opponents.Length);
            if (seatIndices.Length != opponents.Length)
            {
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] 상대 {opponents.Length}명은 좌석에 앉힐 수 없습니다 (최대 {SeatCount}).", this);
                return;
            }

            for (int i = 0; i < opponents.Length; i++)
            {
                int actor = opponents[i];
                int seatIndex = seatIndices[i];
                OpponentSeatView seat = seats[seatIndex - 1];
                _seatOf[actor] = seat;
                TrapSlotsView traps = trapSlots[seatIndex - 1];
                _trapsOf[actor] = traps;
                traps.gameObject.SetActive(true);
                traps.SetCount(0);

                seat.gameObject.SetActive(true);
                string nickname = PlayerLook.NameOf(actor);
                // 매치 방의 봇은 사람과 똑같이 닉네임만 (18-bot.md 8-2, PM 2026-11-26)
                seat.SetNickname(BotRoster.IsBot(actor) && !BotRoster.IsMatchRoom ? string.Format(BotNicknameFormat, nickname) : nickname);
                CharacterLook look = LookOf(actor);
                seat.SetLook(look.body, look.accessory);
                seat.SetLife(LifeState.Alive);
                seat.SetChapchu(false);
                seat.SetTurn(false);
                seat.SetTargetable(false);
                seat.SetSkip(false); // 다음 턴 스킵은 스냅샷에 없다 — 다음 알림부터
                seat.SetBlocked(BotRoster.TryGetPublic(actor, PlayerProps.ActionBlock, out object block) && block is byte blockValue && blockValue != (byte)ActionBlock.None);

                // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값. 이벤트가 먼저 왔다면 같은 값이다. 봇의 값은 방 속성에 있다 (18-bot.md 9절).
                if (BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object hp) && hp is int hpValue)
                    seat.SetHp(Mathf.Clamp(hpValue, 0, GameServer.HpLimit), GameServer.HpBarMax(hpValue)); // 방 속성은 남이 쓸 수 있다

                if (BotRoster.TryGetPublic(actor, PlayerProps.HandCount, out object handCount) && handCount is int handCountValue)
                    seat.SetHandCount(handCountValue);

                if (BotRoster.TryGetPublic(actor, PlayerProps.ChapChu, out object chapchu) && chapchu is bool chapchuValue)
                    seat.SetChapchu(chapchuValue);

                if (BotRoster.TryGetPublic(actor, PlayerProps.TrapCount, out object trapCount) && trapCount is int trapCountValue)
                    traps.SetCount(trapCountValue);

                // 사망 대기는 보이지 않는다 (10-ui.md 2-1) — 최종 사망만
                if (BotRoster.TryGetPublic(actor, PlayerProps.LifeState, out object life) && life is byte lifeValue && lifeValue == (byte)LifeState.Dead)
                    seat.SetLife((LifeState)lifeValue);
            }

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnActor, out object turnActor) && turnActor is int turnActorValue)
                ShowTurn(turnActorValue);
        }

        /// <summary>상대 수에 따른 좌석 인덱스 (02-player.md 3절). 2명이면 정면(2), 3명이면 1 · 2, 4명이면 1 · 2 · 3. 상대는 actorNumber 오름차순으로 앉는다.</summary>
        public static int[] GetOpponentSeatIndices(int opponentCount)
        {
            switch (opponentCount)
            {
                case 1:  return new[] { 2 };
                case 2:  return new[] { 1, 2 };
                case 3:  return new[] { 1, 2, 3 };
                default: return Array.Empty<int>();
            }
        }

        /// <summary>상대의 HP (OnHpChanged). 좌석이 없는 사람(나 · 나간 사람)이면 무시.</summary>
        public void ShowHp(int actorNumber, int hp)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetHp(hp, GameServer.HpBarMax(hp)); // 100 을 넘으면(GOAT) 막대는 가득, 숫자만 그 값
        }

        /// <summary>상대의 손패 장수 (OnCardPlayed 의 handCount). 뒷면 부채꼴도 같이 준다.</summary>
        public void ShowHandCount(int actorNumber, int handCount)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetHandCount(handCount);
        }

        /// <summary>상대의 보이고 있는 손패 장수. 좌석이 없으면(나 · 나간 사람) false.</summary>
        public bool TryGetHandCount(int actorNumber, out int handCount)
        {
            handCount = 0;
            if (!_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                return false;

            handCount = seat.HandCount;
            return true;
        }

        /// <summary>상대 캐릭터 가운데(화면 좌표). 좌석이 없으면(나 · 나간 사람) false.</summary>
        public bool TryGetCharacterScreenPoint(int actorNumber, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (!_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                return false;

            screenPoint = seat.CharacterScreenPoint;
            return true;
        }

        /// <summary>상대의 4 단계 공통 연출 자리 (ResultFx). 좌석이 없으면(나 · 나간 사람) false.</summary>
        public bool TryGetFxSeat(int actorNumber, out ResultFx.Seat fxSeat)
        {
            fxSeat = default;
            if (!_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                return false;

            fxSeat = seat.FxSeat;
            return true;
        }

        /// <summary>상대 좌석의 손 자리(화면 좌표) — 그 사람이 낸 카드가 출발하고, 손패로 돌아가는 카드가 닿는다. 좌석이 없으면(나 · 나간 사람) false.</summary>
        public bool TryGetHandScreenPoint(int actorNumber, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (!_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                return false;

            screenPoint = seat.HandScreenPoint;
            return true;
        }

        /// <summary>상대의 함정 칸 수 (OnTrapSet 등의 trapCount) — 설치된 만큼 뒷면. 종류는 보이지 않는다.</summary>
        public void ShowTrapCount(int actorNumber, int trapCount)
        {
            if (_trapsOf.TryGetValue(actorNumber, out TrapSlotsView traps))
                traps.SetCount(trapCount);
        }

        /// <summary>상대의 마지막으로 채워진 함정 칸 — 발동한 함정 카드가 나오는 자리. 좌석이 없으면 null.</summary>
        public Transform GetTrapSlot(int actorNumber)
            => _trapsOf.TryGetValue(actorNumber, out TrapSlotsView traps) ? traps.SlotAnchor(traps.Count - 1) : null;

        /// <summary>상대가 뽑았다 (OnCardDrawn) — 뒷면이 덱에서 날아온다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator DrawRoutine(int actorNumber, int handCount)
        {
            if (!_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                yield break;

            seat.SetHandCount(handCount);
            yield return new WaitForSeconds(dealInterval);
        }

        /// <summary>행동 카드 금지 (OnActionBlockChanged, 1007 짖기) — 이름표 옆 「행동 금지」. 좌석이 없는 사람이면 무시.</summary>
        public void ShowBlocked(int actorNumber, bool blocked)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetBlocked(blocked);
        }

        /// <summary>다음 턴 스킵 (OnTurnSkipMarked → OnTurnSkipped) — 이름표 옆 「스킵」. 좌석이 없는 사람이면 무시.</summary>
        public void ShowSkipMarked(int actorNumber, bool marked)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetSkip(marked);
        }

        /// <summary>상대의 찹츄 상태 (OnChapchuChanged) — 왕관 · 분홍 이름 칸 · 말풍선. 해제되면 바로 사라진다 (10-ui.md 7절).</summary>
        public void ShowChapchu(int actorNumber, bool isChapchu)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetChapchu(isChapchu);
        }

        /// <summary>
        /// 살아 있음 · 최종 사망 (OnDeathAvoided · OnPlayerDied, 06-health.md) — 이름표 · 캐릭터 팻말. 좌석이 없는 사람(나 · 나간 사람)이면 무시.
        /// 사망 대기는 보이지 않는 상태라 받지 않는다 (10-ui.md 2-1).
        /// </summary>
        public void ShowLife(int actorNumber, LifeState life)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetLife(life);

            // 최종 사망 — 손패 · 설치 함정은 버림 더미로 가고 「함정 슬롯 0」 으로 보인다 (06-health 9절, 시안: 사망이면 빈 칸)
            if (life == LifeState.Dead && _trapsOf.TryGetValue(actorNumber, out TrapSlotsView traps))
                traps.SetCount(0);
        }

        /// <summary>턴 주인의 좌석에만 노란 이름 칸 · 스톱워치. 내 턴이면 모두 끈다. 대상을 고르는 중에 내 턴이 끝나면 선택을 취소한다 (10-ui.md 6절).</summary>
        public void ShowTurn(int turnActorNumber)
        {
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
                pair.Value.SetTurn(pair.Key == turnActorNumber);

            if (_selecting && PhotonNetwork.LocalPlayer != null && turnActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                _selectCancelled = true;
        }

        /// <summary>이 화면 좌표에 앉은 상대의 actorNumber — 끄는 카드를 상대 위에 놓았는지 볼 때 (10-ui.md 4절). 없거나 최종 사망한 상대면 0.</summary>
        public int ActorAt(Vector2 screenPoint)
        {
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
            {
                if (pair.Value.ContainsScreenPoint(screenPoint))
                    return IsDead(pair.Key) ? 0 : pair.Key;
            }
            return 0;
        }

        /// <summary>대상 선택을 취소한다 — 안내 바의 「취소」 · 고르던 카드가 손패에서 빠졌을 때 · 카운터의 대상을 고르는 중에 반응 시간이 끝남 (HandPresenter). 선택 중이 아니면 아무 일도 없다.</summary>
        public void CancelSelection()
        {
            if (_selecting)
                _selectCancelled = true;
        }

        /// <summary>체인 맨 위 카드의 대상인 상대 좌석을 강조한다 (10-ui.md 5-2 #3 「대상 표시」). 빈 배열이면 모두 끈다.</summary>
        public void ShowTargeted(int[] targets)
        {
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
                pair.Value.SetTargeted(Array.IndexOf(targets, pair.Key) >= 0);
        }

        /// <summary>끄는 카드가 겨눈 상대의 좌석만 강조한다. 0 이면 모두 끈다. 대상 선택 중에는 건드리지 않는다 — 같은 고리 · 테를 쓴다.</summary>
        public void ShowAim(int actorNumber)
        {
            if (_selecting || actorNumber == _aimedActor) return;

            _aimedActor = actorNumber;
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
                pair.Value.SetAimed(pair.Key == actorNumber);
        }

        /// <summary>
        /// 좌석에서 count 명을 고르게 한다 (10-ui.md 6절). 안내 바에 카드 이름을 띄우고, 앉은 상대 중 최종 사망하지 않은 사람(includeMe 면 내 패널도)에 빨간 고리 · 테 + 탭.
        /// 고른 자리는 강조를 꺼서 같은 사람을 두 번 못 고른다. 그 밖의 대상 후보 조건은 보지 않는다 — 맞지 않으면 방장이 거절한다 (04-card.md 11절).
        /// 고르는 중에 상대가 나가 남은 후보로 count 명을 채울 수 없으면 취소한다 (02-player.md 6절).
        /// 다 고르면 actorNumber 배열, 취소면 null 을 done 으로 돌려준다. 호출한 코루틴은 yield return 으로 끝까지 기다린다.
        /// </summary>
        public IEnumerator SelectTargetsRoutine(string cardName, int count, bool includeMe, Action<int[]> done)
        {
            if (_selecting)
            {
                Debug.LogWarning($"[{nameof(OpponentSeatsPresenter)}] 이미 대상 선택 중 — 새 요청은 취소로 처리", this);
                done(null);
                yield break;
            }

            _selecting = true;
            _aimedActor = 0;
            _selectCancelled = false;
            _selectCount = count;
            _selectIncludeMe = includeMe;
            _picked.Clear();
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
                pair.Value.SetTargetable(!IsDead(pair.Key)); // 최종 사망한 상대는 고를 수 없다 (10-ui.md 6절 「강조」)
            myPanel.SetTargetable(includeMe);
            targetAsk.Show(cardName);

            while (_picked.Count < count && !_selectCancelled)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) // PC 우클릭 · Esc 취소 (10-ui.md 6절). 모바일은 안내 바의 취소 버튼
                    _selectCancelled = true;
                yield return null;
            }

            targetAsk.Hide();
            foreach (OpponentSeatView seat in _seatOf.Values)
                seat.SetTargetable(false);
            myPanel.SetTargetable(false);
            _selecting = false;

            bool complete = _picked.Count == count;
            if (!complete)
                Debug.Log($"[{nameof(OpponentSeatsPresenter)}] 대상 선택 취소 — {_picked.Count}/{count}명", this);
            done(complete ? _picked.ToArray() : null);
        }

        // 내 패널 탭 — includeMe 일 때만 탭을 받는다 (MyPanelView.SetTargetable).
        private void HandleMeClicked()
        {
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            if (!_selecting || _picked.Contains(me)) return;

            _picked.Add(me);
            myPanel.SetTargetable(false);
        }

        private void HandleSeatClicked(OpponentSeatView seat)
        {
            if (!_selecting) return;

            int actorNumber = _seatOf.FirstOrDefault(pair => pair.Value == seat).Key;
            if (actorNumber == 0 || _picked.Contains(actorNumber) || IsDead(actorNumber)) return;

            _picked.Add(actorNumber);
            seat.SetTargetable(false);
        }

        private void HandleCancelClicked() => CancelSelection();

        private void HandlePlayerLeft(Player player)
        {
            if (!_seatOf.TryGetValue(player.ActorNumber, out OpponentSeatView seat)) return;

            _seatOf.Remove(player.ActorNumber);
            seat.gameObject.SetActive(false);
            if (_trapsOf.TryGetValue(player.ActorNumber, out TrapSlotsView traps))
            {
                _trapsOf.Remove(player.ActorNumber);
                traps.gameObject.SetActive(false);
            }
            Debug.Log($"[{nameof(OpponentSeatsPresenter)}] P{player.ActorNumber} 퇴장 — 좌석을 숨김 (02-player.md 6절)", this);

            if (_selecting)
                ForgetLeftCandidate(player.ActorNumber);
        }

        // 고르는 중에 나간 사람은 대상 후보에서 뺀다 (02-player.md 6절). 남은 후보로 다 채울 수 없으면 선택을 취소한다.
        private void ForgetLeftCandidate(int actorNumber)
        {
            _picked.Remove(actorNumber);

            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            int left = _seatOf.Keys.Count(actor => !_picked.Contains(actor) && !IsDead(actor));
            if (_selectIncludeMe && !_picked.Contains(me))
                left++;

            if (_picked.Count + left < _selectCount)
                _selectCancelled = true;
        }

        // 최종 사망(LifeState.Dead) — 방장이 쓴 공개 값을 읽는다. 대소동으로 되살아나면 방장이 Alive 로 되돌린다 (PunGameServer).
        private static bool IsDead(int actorNumber)
            => BotRoster.TryGetPublic(actorNumber, PlayerProps.LifeState, out object life) && life is byte lifeValue && lifeValue == (byte)LifeState.Dead;
    }
}
