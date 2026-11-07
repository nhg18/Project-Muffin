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
        public static event Action<double, double> OnTrapWindowStarted;      // opensAt, deadline — 「~한 후」 함정 시간
        public static event Action<int, int, int, int, int> OnHandCardsMoved; // from, to, count, fromHandCount, toHandCount — 훔치기 · 교환 (어떤 카드인지는 OnMyCardsGained · Lost)
        public static event Action<int, int[], int[], int, int> OnCardsDiscarded; // actorNumber, cardInstanceIds, cardIds, handCount, discardCount — 강제 버림 (공개)
        public static event Action<int, int, int, int, int> OnTrapDestroyed;  // owner, cardInstanceId, cardId, trapCount, discardCount — 함정 파괴 (공개)
        public static event Action<int, int, int> OnTrapReturned;            // owner, trapCount, handCount — 함정이 주인 손패로 (종류는 주인만)
        public static event Action<int, int, int, int> OnTrapMoved;          // from, to, fromTrapCount, toTrapCount — 함정 빼앗기 (종류는 두 사람만)
        public static event Action<int> OnTurnSkipMarked;                    // actorNumber — 다음 턴 스킵이 걸림
        public static event Action<int> OnTurnSkipped;                       // actorNumber — 턴이 스킵됨
        public static event Action<int, int, int, int> OnCardReturnedToHand; // actorNumber, cardInstanceId, cardId, handCount — 체인의 카드가 시전자 손패로 (C01)
        public static event Action<int, int> OnDeathAvoided;                 // actorNumber, hp — 사망 대기에서 생존
        public static event Action<int, GameEndReason, double> OnGameEnded;  // winner(무승부 -1), reason, returnAt(방으로 이동하는 서버 시각)

        // ── 나에게만 ──
        public static event Action<int, int> OnMyCardDrawn;                  // cardInstanceId, cardId
        public static event Action<int> OnMyRequestRejected;                 // RejectCode
        public static event Action<int, int> OnMyTrapSet;                    // cardInstanceId, cardId — 내 함정 칸에 들어옴 (설치 · 빼앗아 옴)
        public static event Action<int> OnMyTrapRemoved;                     // cardInstanceId — 내 함정 칸에서 빠짐 (손패로 반환 · 빼앗김)
        public static event Action<int[], int[]> OnMyCardsGained;            // cardInstanceIds, cardIds — 내 손패로 들어옴 (훔침 · 교환 · 함정 반환)
        public static event Action<int[]> OnMyCardsLost;                     // cardInstanceIds — 내 손패에서 나감 (훔쳐짐 · 교환)
        public static event Action<int[], int[], int[], int[]> OnMySync;     // 손패 인스턴스 ID · 카드 ID, 함정 인스턴스 ID · 카드 ID — RequestMySync 의 답 (지금 내 비공개 카드 전부)

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
        public static void RaiseHandCardsMoved(int from, int to, int count, int fromHandCount, int toHandCount) => OnHandCardsMoved?.Invoke(from, to, count, fromHandCount, toHandCount);
        public static void RaiseCardsDiscarded(int actorNumber, int[] cardInstanceIds, int[] cardIds, int handCount, int discardCount) => OnCardsDiscarded?.Invoke(actorNumber, cardInstanceIds, cardIds, handCount, discardCount);
        public static void RaiseTrapDestroyed(int owner, int cardInstanceId, int cardId, int trapCount, int discardCount) => OnTrapDestroyed?.Invoke(owner, cardInstanceId, cardId, trapCount, discardCount);
        public static void RaiseTrapReturned(int owner, int trapCount, int handCount) => OnTrapReturned?.Invoke(owner, trapCount, handCount);
        public static void RaiseTrapMoved(int from, int to, int fromTrapCount, int toTrapCount) => OnTrapMoved?.Invoke(from, to, fromTrapCount, toTrapCount);
        public static void RaiseTurnSkipMarked(int actorNumber) => OnTurnSkipMarked?.Invoke(actorNumber);
        public static void RaiseTurnSkipped(int actorNumber) => OnTurnSkipped?.Invoke(actorNumber);
        public static void RaiseCardReturnedToHand(int actorNumber, int cardInstanceId, int cardId, int handCount) => OnCardReturnedToHand?.Invoke(actorNumber, cardInstanceId, cardId, handCount);
        public static void RaiseDeathAvoided(int actorNumber, int hp) => OnDeathAvoided?.Invoke(actorNumber, hp);
        public static void RaiseMyTrapRemoved(int cardInstanceId) => OnMyTrapRemoved?.Invoke(cardInstanceId);
        public static void RaiseMyCardsGained(int[] cardInstanceIds, int[] cardIds) => OnMyCardsGained?.Invoke(cardInstanceIds, cardIds);
        public static void RaiseMyCardsLost(int[] cardInstanceIds) => OnMyCardsLost?.Invoke(cardInstanceIds);
        public static void RaiseMySync(int[] handIds, int[] handCardIds, int[] trapIds, int[] trapCardIds) => OnMySync?.Invoke(handIds, handCardIds, trapIds, trapCardIds);
        public static void RaiseMyCardDrawn(int cardInstanceId, int cardId) => OnMyCardDrawn?.Invoke(cardInstanceId, cardId);
        public static void RaiseMyRequestRejected(int code) => OnMyRequestRejected?.Invoke(code);
    }
}
