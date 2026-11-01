using System;

namespace Chapchu.Network
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
        public static event Action<int, int, int, int[], int, int> OnCardPlayed; // actorNumber, cardInstanceId, cardId, targets, handCount, discardCount

        // ── 나에게만 ──
        public static event Action<int, int> OnMyCardDrawn;                  // cardInstanceId, cardId
        public static event Action<int> OnMyRequestRejected;                 // RejectCode

        public static void RaiseHpChanged(int actorNumber, int hp) => OnHpChanged?.Invoke(actorNumber, hp);
        public static void RaiseCardDrawn(int actorNumber, int handCount, int deckCount) => OnCardDrawn?.Invoke(actorNumber, handCount, deckCount);
        public static void RaiseDeckRefilled(int deckCount) => OnDeckRefilled?.Invoke(deckCount);
        public static void RaiseTurnStarted(int actorNumber, double deadline) => OnTurnStarted?.Invoke(actorNumber, deadline);
        public static void RaiseCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount) => OnCardPlayed?.Invoke(actorNumber, cardInstanceId, cardId, targets, handCount, discardCount);
        public static void RaiseMyCardDrawn(int cardInstanceId, int cardId) => OnMyCardDrawn?.Invoke(cardInstanceId, cardId);
        public static void RaiseMyRequestRejected(int code) => OnMyRequestRejected?.Invoke(code);
    }
}
