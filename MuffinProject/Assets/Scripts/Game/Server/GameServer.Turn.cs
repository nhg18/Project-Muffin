using System.Collections.Generic;
using Chapchu.Core;

namespace Chapchu.Game
{
    // GameServer.Turn.cs
    public partial class GameServer
    {
        // 게임 시작 1회: 참가자 순서를 무작위로 섞어 턴 순서를 정하고 첫 사람에게 턴을 준다 (03-turn.md 6절).
        private void InitTurnOrder(IReadOnlyList<int> actors)
        {
            _turnOrder.Clear();
            _turnOrder.AddRange(actors);
            Shuffle(_turnOrder);

            SetTurn(_turnOrder[0]);
        }

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
