using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 좌석 3개에 값을 넣는다 (10-ui.md 2-1 「상대 좌석」 · 02-player.md 3절). 좌석 인덱스 ↔ actorNumber 는 여기서만 계산한다 —
    /// 목걸이 색(PlayerLook) 등 다른 UI 도 이 결과를 쓴다. HP · 손패 수 · 턴은 GameScreenPresenter 가 연출 큐 순서로 넘긴다.
    /// 퇴장은 RoomEvents.OnPlayerLeft 로 받아 좌석을 숨긴다 (02-player.md 6절) — 게임 이벤트가 아니라 큐 밖.
    /// 늦게 열렸을 때는 Start 에서 각 Player 의 CustomProperties 와 방의 턴 주인을 한 번 읽는다 (09-network.md 6.1).
    /// 대상 선택(10-ui.md 6절)도 여기서 — 좌석에 빨간 고리 · 테를 켜고 안내 바(TargetAskView)를 띄운 뒤 탭을 모아 actorNumber 로 돌려준다.
    /// 나를 포함하는 카드(OneAny)는 내 패널도 빨간 테 + 탭을 받는다.
    /// 취소 = 안내 바의 「취소」 버튼(모바일 포함) · PC 우클릭 · Esc · 내 턴이 끝남. 제한 시간은 없다 (턴 30초가 유일한 타이머).
    /// </summary>
    public class OpponentSeatsPresenter : MonoBehaviour
    {
        private const int SeatCount = 3; // 좌석 인덱스 1 · 2 · 3 (02-player.md 3절)

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

        private readonly Dictionary<int, OpponentSeatView> _seatOf = new Dictionary<int, OpponentSeatView>(); // actorNumber → 좌석
        private readonly Dictionary<int, TrapSlotsView> _trapsOf = new Dictionary<int, TrapSlotsView>(); // actorNumber → 함정 칸
        private readonly List<int> _picked = new List<int>(); // 대상 선택 중 고른 actorNumber
        private bool _selecting;
        private bool _selectCancelled;
        private int _aimedActor; // 끄는 카드가 겨눈 상대 (없으면 0)

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

            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            int[] opponents = PhotonNetwork.PlayerList.Select(player => player.ActorNumber).Where(actor => actor != me).OrderBy(actor => actor).ToArray();
            int[] seatIndices = GetOpponentSeatIndices(opponents.Length);
            if (seatIndices.Length != opponents.Length)
            {
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] 상대 {opponents.Length}명은 좌석에 앉힐 수 없습니다 (최대 {SeatCount}).", this);
                return;
            }

            for (int i = 0; i < opponents.Length; i++)
            {
                Player player = PhotonNetwork.CurrentRoom.GetPlayer(opponents[i]);
                int seatIndex = seatIndices[i];
                OpponentSeatView seat = seats[seatIndex - 1];
                _seatOf[opponents[i]] = seat;
                TrapSlotsView traps = trapSlots[seatIndex - 1];
                _trapsOf[opponents[i]] = traps;
                traps.gameObject.SetActive(true);
                traps.SetCount(0);

                seat.gameObject.SetActive(true);
                seat.SetNickname(PlayerLook.NameOf(player));
                seat.SetColor(PlayerLook.SeatColors[seatIndex - 1]);
                seat.SetChapchu(false);
                seat.SetTurn(false);
                seat.SetTargetable(false);

                // 씬이 늦게 열려 첫 이벤트를 놓쳤을 때를 위한 지금 값. 이벤트가 먼저 왔다면 같은 값이다.
                if (player.CustomProperties.TryGetValue(PlayerProps.Hp, out object hp) && hp is int hpValue)
                    seat.SetHp(hpValue, GameServer.MaxHp);

                if (player.CustomProperties.TryGetValue(PlayerProps.HandCount, out object handCount) && handCount is int handCountValue)
                    seat.SetHandCount(handCountValue);

                if (player.CustomProperties.TryGetValue(PlayerProps.ChapChu, out object chapchu) && chapchu is bool chapchuValue)
                    seat.SetChapchu(chapchuValue);

                if (player.CustomProperties.TryGetValue(PlayerProps.TrapCount, out object trapCount) && trapCount is int trapCountValue)
                    traps.SetCount(trapCountValue);
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
                seat.SetHp(hp, GameServer.MaxHp);
        }

        /// <summary>상대의 손패 장수 (OnCardPlayed 의 handCount). 뒷면 부채꼴도 같이 준다.</summary>
        public void ShowHandCount(int actorNumber, int handCount)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetHandCount(handCount);
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

        /// <summary>상대의 찹츄 상태 (OnChapchuChanged) — 왕관 · 분홍 띠 · 말풍선. 해제되면 바로 사라진다 (10-ui.md 7절).</summary>
        public void ShowChapchu(int actorNumber, bool isChapchu)
        {
            if (_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                seat.SetChapchu(isChapchu);
        }

        /// <summary>턴 주인의 좌석에만 노란 머리 띠 · 발밑 타원. 내 턴이면 모두 끈다. 대상을 고르는 중에 내 턴이 끝나면 선택을 취소한다 (10-ui.md 6절).</summary>
        public void ShowTurn(int turnActorNumber)
        {
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
                pair.Value.SetTurn(pair.Key == turnActorNumber);

            if (_selecting && PhotonNetwork.LocalPlayer != null && turnActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                _selectCancelled = true;
        }

        /// <summary>이 화면 좌표에 앉은 상대의 actorNumber — 끄는 카드를 상대 위에 놓았는지 볼 때 (10-ui.md 4절). 없으면 0.</summary>
        public int ActorAt(Vector2 screenPoint)
        {
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
            {
                if (pair.Value.ContainsScreenPoint(screenPoint))
                    return pair.Key;
            }
            return 0;
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
        /// 좌석에서 count 명을 고르게 한다 (10-ui.md 6절). 안내 바에 카드 이름을 띄우고, 앉은 상대 전부(includeMe 면 내 패널도)에 빨간 고리 · 테 + 탭.
        /// 고른 자리는 강조를 꺼서 같은 사람을 두 번 못 고른다. 대상 후보 조건은 보지 않는다 — 맞지 않으면 방장이 거절한다 (04-card.md 11절).
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
            _picked.Clear();
            foreach (OpponentSeatView seat in _seatOf.Values)
                seat.SetTargetable(true);
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
            if (actorNumber == 0 || _picked.Contains(actorNumber)) return;

            _picked.Add(actorNumber);
            seat.SetTargetable(false);
        }

        private void HandleCancelClicked()
        {
            if (_selecting)
                _selectCancelled = true;
        }

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
        }
    }
}
