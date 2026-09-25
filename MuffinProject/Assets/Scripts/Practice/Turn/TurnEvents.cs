using System;

namespace Chapchu.Practice
{
    /// <summary>
    /// 마스터 → UI 통지. UI는 여기서 받은 값만 표시하고 판정하지 않는다.
    /// </summary>
    public static class TurnEvents
    {
        public static event Action<int> OnTurnChanged;               // actorNumber
        public static event Action<int, string> OnEndTurnRejected;   // actorNumber, reason

        public static void RaiseTurnChanged(int actorNumber) => OnTurnChanged?.Invoke(actorNumber);
        public static void RaiseEndTurnRejected(int actorNumber, string reason) => OnEndTurnRejected?.Invoke(actorNumber, reason);
    }
}
