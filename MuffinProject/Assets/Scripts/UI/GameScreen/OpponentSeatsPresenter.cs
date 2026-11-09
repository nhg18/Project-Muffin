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
    /// 취소 = 안내 바의 「취소」 버튼(모바일 포함) 또는 PC 우클릭.
    /// </summary>
    public class OpponentSeatsPresenter : MonoBehaviour
    {
        private const int SeatCount = 3; // 좌석 인덱스 1 · 2 · 3 (02-player.md 3절)

        [Tooltip("좌석 인덱스 1 · 2 · 3 순서 (02-player.md 3절). 빈 좌석은 숨긴다.")]
        [SerializeField] private OpponentSeatView[] seats = new OpponentSeatView[SeatCount];
        [Tooltip("대상 선택 안내 바 — 「카드 · 누구에게 쓸까요?」 + 남은 초 + 취소.")]
        [SerializeField] private TargetAskView targetAsk;
        [Tooltip("상대가 뽑은 뒷면을 꺼낸 뒤 다음 일로 넘어가기까지 초. 내 손패(HandPresenter.dealInterval)와 같게 — 첫 분배 때 한 장씩 날아온다.")]
        [SerializeField] private float dealInterval = 0.15f;
        [Tooltip("대상 선택 제한 초. 지나면 취소 (10-ui.md 6절 — 제안값 5).")]
        [SerializeField] private float targetSelectTimeout = 5f;

        private readonly Dictionary<int, OpponentSeatView> _seatOf = new Dictionary<int, OpponentSeatView>(); // actorNumber → 좌석
        private readonly List<int> _picked = new List<int>(); // 대상 선택 중 고른 actorNumber
        private bool _selecting;
        private bool _selectCancelled;

        /// <summary>대상 선택 중인가 — 그동안 손패는 잠긴다 (HandPresenter).</summary>
        public bool IsSelectingTargets => _selecting;

        private void Awake()
        {
            if (seats == null || seats.Length != SeatCount || seats.Any(seat => seat == null))
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] {nameof(seats)} 는 {SeatCount}개가 모두 연결돼야 합니다.", this);
            if (targetAsk == null)
                Debug.LogError($"[{nameof(OpponentSeatsPresenter)}] {nameof(targetAsk)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
            targetAsk.CancelClicked += HandleCancelClicked;
            foreach (OpponentSeatView seat in seats)
                seat.OnClicked += HandleSeatClicked;
        }

        private void OnDisable()
        {
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
            targetAsk.CancelClicked -= HandleCancelClicked;
            foreach (OpponentSeatView seat in seats)
                seat.OnClicked -= HandleSeatClicked;
        }

        private void Start()
        {
            targetAsk.Hide();
            foreach (OpponentSeatView seat in seats)
                seat.gameObject.SetActive(false);

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

        /// <summary>상대가 뽑았다 (OnCardDrawn) — 뒷면이 덱에서 날아온다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator DrawRoutine(int actorNumber, int handCount)
        {
            if (!_seatOf.TryGetValue(actorNumber, out OpponentSeatView seat))
                yield break;

            seat.SetHandCount(handCount);
            yield return new WaitForSeconds(dealInterval);
        }

        /// <summary>턴 주인의 좌석에만 노란 머리 띠 · 발밑 타원. 내 턴이면 모두 끈다.</summary>
        public void ShowTurn(int turnActorNumber)
        {
            foreach (KeyValuePair<int, OpponentSeatView> pair in _seatOf)
                pair.Value.SetTurn(pair.Key == turnActorNumber);
        }

        /// <summary>
        /// 상대 좌석에서 count 명을 고르게 한다 (10-ui.md 6절). 안내 바에 카드 이름 · 남은 초를 띄우고, 앉은 상대 전부에 빨간 고리 · 테 + 탭. 고른 좌석은 강조를 꺼서 같은 사람을 두 번 못 고른다.
        /// 다 고르면 actorNumber 배열, 취소 · 제한 시간이면 null 을 done 으로 돌려준다. 호출한 코루틴은 yield return 으로 끝까지 기다린다.
        /// </summary>
        public IEnumerator SelectTargetsRoutine(string cardName, int count, Action<int[]> done)
        {
            if (_selecting)
            {
                Debug.LogWarning($"[{nameof(OpponentSeatsPresenter)}] 이미 대상 선택 중 — 새 요청은 취소로 처리", this);
                done(null);
                yield break;
            }

            _selecting = true;
            _selectCancelled = false;
            _picked.Clear();
            foreach (OpponentSeatView seat in _seatOf.Values)
                seat.SetTargetable(true);
            targetAsk.Show(cardName);

            float elapsed = 0f;
            int shownSeconds = -1;
            while (elapsed < targetSelectTimeout && _picked.Count < count && !_selectCancelled)
            {
                if (Input.GetMouseButtonDown(1)) // PC 우클릭 취소 (10-ui.md 6절). 모바일은 안내 바의 취소 버튼
                    _selectCancelled = true;

                int seconds = Mathf.CeilToInt(targetSelectTimeout - elapsed);
                if (seconds != shownSeconds)
                {
                    shownSeconds = seconds;
                    targetAsk.SetRemainingSeconds(seconds);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            targetAsk.Hide();
            foreach (OpponentSeatView seat in _seatOf.Values)
                seat.SetTargetable(false);
            _selecting = false;

            bool complete = _picked.Count == count;
            if (!complete)
                Debug.Log($"[{nameof(OpponentSeatsPresenter)}] 대상 선택 {(_selectCancelled ? "취소" : "시간 초과")} — {_picked.Count}/{count}명", this);
            done(complete ? _picked.ToArray() : null);
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
            Debug.Log($"[{nameof(OpponentSeatsPresenter)}] P{player.ActorNumber} 퇴장 — 좌석을 숨김 (02-player.md 6절)", this);
        }
    }
}
