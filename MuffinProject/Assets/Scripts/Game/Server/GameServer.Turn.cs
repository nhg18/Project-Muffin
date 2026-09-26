using Chapchu.Core;

namespace Chapchu.Game
{
    // GameServer.Turn.cs
    public partial class GameServer
    {
        public void EndTurn(int requester)
        {
            if (requester != CurrentTurnActor)
            {
                _outbox.Reject(requester, "내 턴이 아닙니다.");
                return;
            }

            SetTurn(GetNextActor(CurrentTurnActor));
        }
        
        private void SetTurn(int actorNumber)
        {
            CurrentTurnActor = actorNumber;
            _outbox.SetRoomState(RoomProps.TurnActor, actorNumber);
        }

        // TODO: 살아 있는 사람만 · 나간 사람 건너뛰기 (기능 2 · 7).
        private int GetNextActor(int currentActor)
        {
            int index = _turnOrder.IndexOf(currentActor);
            return _turnOrder[(index + 1) % _turnOrder.Count];
        }
    }
}
