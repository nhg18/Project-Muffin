using System;

namespace Muffin.Game
{

    /// <summary>
    /// 마스터 → UI 단방향 통지. UI는 여기서 받은 값만 표시하고 판정하지 않는다.
    /// 모든 이벤트는 누구의 변화인지 알 수 있도록 actorNumber 를 첫 인자로 갖는다.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<int> OnTurnChanged;                 // actorNumber
        public static event Action<int, int> OnDrawn;                  // actorNumber, cardId
        public static event Action<int, int> OnHpChanged;              // actorNumber, hp
        public static event Action<int, int> OnHandCountChanged;       // actorNumber, handCount
        public static event Action<int, int> OnTrapCountChanged;       // actorNumber, trapCount
        public static event Action<int, LifeState> OnLifeStateChanged; // actorNumber, lifeState
        public static event Action<int, bool> OnChapChuChanged;        // actorNumber, isChapChu
        public static event Action<int> OnDeckCountChanged;            // deckCount
        public static event Action<int, string> OnRequestRejected;     // actorNumber, reason

        // 로컬 UI 전용. 마스터 통지가 아니므로 Presentation 으로 이동 예정.
        public static event Action<bool> OnHandModeChanged;

        public static void RaiseTurnChanged(int actorNumber) => OnTurnChanged?.Invoke(actorNumber);
        public static void RaiseDrawn(int actorNumber, int cardId) => OnDrawn?.Invoke(actorNumber, cardId);
        public static void RaiseHpChanged(int actorNumber, int hp) => OnHpChanged?.Invoke(actorNumber, hp);
        public static void RaiseHandCountChanged(int actorNumber, int handCount) => OnHandCountChanged?.Invoke(actorNumber, handCount);
        public static void RaiseTrapCountChanged(int actorNumber, int trapCount) => OnTrapCountChanged?.Invoke(actorNumber, trapCount);
        public static void RaiseLifeStateChanged(int actorNumber, LifeState lifeState) => OnLifeStateChanged?.Invoke(actorNumber, lifeState);
        public static void RaiseChapChuChanged(int actorNumber, bool isChapChu) => OnChapChuChanged?.Invoke(actorNumber, isChapChu);
        public static void RaiseDeckCountChanged(int deckCount) => OnDeckCountChanged?.Invoke(deckCount);
        public static void RaiseRequestRejected(int actorNumber, string reason) => OnRequestRejected?.Invoke(actorNumber, reason);

        public static void RaiseHandModeChanged(bool isHandMode) => OnHandModeChanged?.Invoke(isHandMode);
    }
}
