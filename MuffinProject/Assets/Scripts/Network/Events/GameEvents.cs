using System;
using Chapchu.Game;

namespace Chapchu.Network.Events
{
    /// <summary>
    /// 인게임 결과 통지 (방장 → 각 클라이언트). 화면은 여기서 받은 값만 보여 주고 판정하지 않는다 (09-network.md 2절).
    /// 받는 순서 = 일어난 순서. 연출도 이 순서로 한다.
    /// 전원에게 가는 이벤트는 누구의 일인지 actorNumber 를 첫 인자로 갖는다. OnMy 로 시작하는 이벤트는 나에게만 온다.
    /// </summary>
    public static class GameEvents
    {
        // ── 전원에게 ──
        public static event Action<int, int> OnHpChanged;                    // actorNumber, hp
        public static event Action<int, int, int> OnCardDrawn;               // actorNumber, handCount, deckCount — 카드 내용은 OnMyCardDrawn
        public static event Action<int> OnDeckRefilled;                      // deckCount — 버림 더미를 섞어 덱을 다시 채움
        public static event Action<int, double> OnTurnStarted;               // actorNumber, deadline(PhotonNetwork.Time 기준 초)
        public static event Action<int, double> OnTurnResumed;               // actorNumber, deadline — 메인 행동 전에 멈췄던 턴이 이어짐 (턴 시작 아님)
        public static event Action<int, int, int, int[], int, int> OnCardPlayed; // actorNumber, cardInstanceId, cardId, targets, handCount, discardCount — 체인에 올라감 (연출 1 ~ 3 단계). 효과는 OnCardResolved 뒤
        public static event Action<double, double> OnReactionStarted;        // opensAt, deadline (PhotonNetwork.Time 기준 초) — opensAt 전은 연출 중, 카운터 거절
        public static event Action<int, int, int, int[], int> OnCardResolved; // actorNumber, cardInstanceId, cardId, targets, discardCount — 효과 적용 (연출 4 단계), 효과 결과가 뒤따른다
        public static event Action<int, int, int, int> OnCardNegated;        // actorNumber, cardInstanceId, cardId, discardCount — 무효화, 4 단계 없이 버림 더미로
        public static event Action<int, int> OnEmptyDeckDamaged;             // actorNumber, amount — 빈 덱 피해, 체력은 뒤따르는 OnHpChanged
        public static event Action<int, double, double> OnDeathPending;      // actorNumber, startsAt, deadline — 사망 대기, 게임이 멈춘다
        public static event Action<int, int> OnPlayerDied;                   // actorNumber, discardCount — 최종 사망, 손패 0
        public static event Action<int, int> OnPlayerLeftGame;               // actorNumber, discardCount — 게임 중 나감, 손패 0 (좌석 제거는 RoomEvents.OnPlayerLeft)
        public static event Action<int, bool> OnChapchuChanged;              // actorNumber, isChapchu — 선언 · 해제
        public static event Action<int, int, int> OnTrapSet;                 // actorNumber, handCount, trapCount — 함정 설치 (종류는 OnMyTrapSet)
        public static event Action<int, int, int, int[], int> OnTrapActivated; // actorNumber, cardInstanceId, cardId, targets, trapCount — 공개 · 체인에 올라감
        public static event Action<double, double> OnTrapWindowStarted;      // opensAt, deadline — 「피해 받은 후」 함정 시간
        public static event Action<int, GameEndReason, double> OnGameEnded;  // winner(무승부 -1), reason, returnAt(방으로 이동하는 서버 시각)

        // ── 나에게만 ──
        public static event Action<int, int> OnMyCardDrawn;                  // cardInstanceId, cardId
        public static event Action<int> OnMyRequestRejected;                 // RejectCode
        public static event Action<int, int> OnMyTrapSet;                    // cardInstanceId, cardId — 내가 설치한 함정 (손패에서 슬롯으로)

        public static void RaiseHpChanged(int actorNumber, int hp) => OnHpChanged?.Invoke(actorNumber, hp);
        public static void RaiseCardDrawn(int actorNumber, int handCount, int deckCount) => OnCardDrawn?.Invoke(actorNumber, handCount, deckCount);
        public static void RaiseDeckRefilled(int deckCount) => OnDeckRefilled?.Invoke(deckCount);
        public static void RaiseTurnStarted(int actorNumber, double deadline) => OnTurnStarted?.Invoke(actorNumber, deadline);
        public static void RaiseCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount) => OnCardPlayed?.Invoke(actorNumber, cardInstanceId, cardId, targets, handCount, discardCount);
        public static void RaiseReactionStarted(double opensAt, double deadline) => OnReactionStarted?.Invoke(opensAt, deadline);
        public static void RaiseCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets, int discardCount) => OnCardResolved?.Invoke(actorNumber, cardInstanceId, cardId, targets, discardCount);
        public static void RaiseCardNegated(int actorNumber, int cardInstanceId, int cardId, int discardCount) => OnCardNegated?.Invoke(actorNumber, cardInstanceId, cardId, discardCount);
        public static void RaiseEmptyDeckDamaged(int actorNumber, int amount) => OnEmptyDeckDamaged?.Invoke(actorNumber, amount);
        public static void RaiseDeathPending(int actorNumber, double startsAt, double deadline) => OnDeathPending?.Invoke(actorNumber, startsAt, deadline);
        public static void RaisePlayerDied(int actorNumber, int discardCount) => OnPlayerDied?.Invoke(actorNumber, discardCount);
        public static void RaisePlayerLeftGame(int actorNumber, int discardCount) => OnPlayerLeftGame?.Invoke(actorNumber, discardCount);
        public static void RaiseChapchuChanged(int actorNumber, bool isChapchu) => OnChapchuChanged?.Invoke(actorNumber, isChapchu);
        public static void RaiseGameEnded(int winner, GameEndReason reason, double returnAt) => OnGameEnded?.Invoke(winner, reason, returnAt);
        public static void RaiseTurnResumed(int actorNumber, double deadline) => OnTurnResumed?.Invoke(actorNumber, deadline);
        public static void RaiseTrapSet(int actorNumber, int handCount, int trapCount) => OnTrapSet?.Invoke(actorNumber, handCount, trapCount);
        public static void RaiseTrapActivated(int actorNumber, int cardInstanceId, int cardId, int[] targets, int trapCount) => OnTrapActivated?.Invoke(actorNumber, cardInstanceId, cardId, targets, trapCount);
        public static void RaiseTrapWindowStarted(double opensAt, double deadline) => OnTrapWindowStarted?.Invoke(opensAt, deadline);
        public static void RaiseMyTrapSet(int cardInstanceId, int cardId) => OnMyTrapSet?.Invoke(cardInstanceId, cardId);
        public static void RaiseMyCardDrawn(int cardInstanceId, int cardId) => OnMyCardDrawn?.Invoke(cardInstanceId, cardId);
        public static void RaiseMyRequestRejected(int code) => OnMyRequestRejected?.Invoke(code);
    }
}
