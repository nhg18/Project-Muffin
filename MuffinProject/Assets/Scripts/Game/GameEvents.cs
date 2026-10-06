using System;

namespace Chapchu.Game
{

    /// <summary>
    /// 마스터 → UI 단방향 통지. UI는 여기서 받은 값만 표시하고 판정하지 않는다.
    /// 모든 이벤트는 누구의 변화인지 알 수 있도록 actorNumber 를 첫 인자로 갖는다.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<int[]> OnGameStarted; // playerActors
        public static event Action<int> OnTurnChanged;                 // actorNumber
        public static event Action<double> OnTurnDeadlineChanged;      // 턴 마감 서버 시각(PhotonNetwork.Time). 남은 시간 = 마감 - 현재
        public static event Action<double> OnReactionDeadlineChanged;  // 반응 마감 서버 시각(PhotonNetwork.Time). 0 = 체인 끝 (반응 타이머 숨김)
        public static event Action<int, int, int> OnDrawn;             // actorNumber, cardInstanceId, cardId — 주인에게만
        public static event Action<int, int> OnHpChanged;              // actorNumber, hp
        public static event Action<int, int> OnHandCountChanged;       // actorNumber, handCount
        public static event Action<int, int> OnTrapCountChanged;       // actorNumber, trapCount
        public static event Action<int, LifeState> OnLifeStateChanged; // actorNumber, lifeState
        public static event Action<int, bool> OnChapChuChanged;        // actorNumber, isChapChu
        public static event Action<int> OnDeckCountChanged;            // deckCount
        public static event Action<int> OnDiscardCountChanged;         // discardCount
        public static event Action<int> OnDeckRefilled;                // deckCount — 버림 더미를 섞어 덱을 다시 채움 (연출용, 전원)
        public static event Action<int, int> OnRequestRejected;        // actorNumber, rejectCode (RejectCode). 문구는 RejectText
        public static event Action<int, int, int, int[]> OnCardUsed;   // actorNumber, cardInstanceId, cardId, targetActorNumbers — 방장이 승인해 체인에 올린 카드 (전원). 효과는 아직 — 처리는 OnCardResolved
        public static event Action<int, int, int, int[], bool> OnCardResolved; // actorNumber, cardInstanceId, cardId, 실제로 효과를 받은 대상, negated — 체인 카드 1장 처리 (전원, 위에서부터)

        // 로컬 UI 전용. 마스터 통지가 아니므로 Presentation 으로 이동 예정.
        public static event Action<bool> OnHandModeChanged;

        public static void RaiseOnGameStarted(int [] playerActors) => OnGameStarted?.Invoke(playerActors);
        public static void RaiseTurnChanged(int actorNumber) => OnTurnChanged?.Invoke(actorNumber);
        public static void RaiseTurnDeadlineChanged(double deadline) => OnTurnDeadlineChanged?.Invoke(deadline);
        public static void RaiseReactionDeadlineChanged(double deadline) => OnReactionDeadlineChanged?.Invoke(deadline);
        public static void RaiseDrawn(int actorNumber, int cardInstanceId, int cardId) => OnDrawn?.Invoke(actorNumber, cardInstanceId, cardId);
        public static void RaiseCardUsed(int actorNumber, int cardInstanceId, int cardId, int[] targetActorNumbers) => OnCardUsed?.Invoke(actorNumber, cardInstanceId, cardId, targetActorNumbers);
        public static void RaiseCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] affectedActorNumbers, bool negated) => OnCardResolved?.Invoke(actorNumber, cardInstanceId, cardId, affectedActorNumbers, negated);
        public static void RaiseHpChanged(int actorNumber, int hp) => OnHpChanged?.Invoke(actorNumber, hp);
        public static void RaiseHandCountChanged(int actorNumber, int handCount) => OnHandCountChanged?.Invoke(actorNumber, handCount);
        public static void RaiseTrapCountChanged(int actorNumber, int trapCount) => OnTrapCountChanged?.Invoke(actorNumber, trapCount);
        public static void RaiseLifeStateChanged(int actorNumber, LifeState lifeState) => OnLifeStateChanged?.Invoke(actorNumber, lifeState);
        public static void RaiseChapChuChanged(int actorNumber, bool isChapChu) => OnChapChuChanged?.Invoke(actorNumber, isChapChu);
        public static void RaiseDeckCountChanged(int deckCount) => OnDeckCountChanged?.Invoke(deckCount);
        public static void RaiseDiscardCountChanged(int discardCount) => OnDiscardCountChanged?.Invoke(discardCount);
        public static void RaiseDeckRefilled(int deckCount) => OnDeckRefilled?.Invoke(deckCount);
        public static void RaiseRequestRejected(int actorNumber, int code) => OnRequestRejected?.Invoke(actorNumber, code);
        public static void RaiseHandModeChanged(bool isHandMode) => OnHandModeChanged?.Invoke(isHandMode);
    }
}
