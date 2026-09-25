using System;

namespace Chapchu.Practice
{
    /// <summary>
    /// 마스터(GameServer) → UI 통지. UI는 여기서 받은 값만 표시하고 판정하지 않는다. 기능이 늘면 여기에 이벤트를 추가한다.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<int> OnTurnChanged;               // actorNumber
        public static event Action<int, string> OnEndTurnRejected;   // actorNumber, reason

        public static void RaiseTurnChanged(int actorNumber) => OnTurnChanged?.Invoke(actorNumber);
        public static void RaiseEndTurnRejected(int actorNumber, string reason) => OnEndTurnRejected?.Invoke(actorNumber, reason);
    }
}
