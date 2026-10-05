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

        // 턴 순서에서 뺀다. 턴 주인이었으면 다음 사람에게 넘긴다 (03-turn.md 7절).
        public void RemoveFromTurnOrder(int actor)
        {
            if (!_turnOrder.Contains(actor)) return;

            if (actor == CurrentTurnActor)
                SetTurn(GetNextActor(actor)); // 빼기 전에 다음 사람을 구한다

            _turnOrder.Remove(actor);
        }

        private void SetTurn(int actorNumber)
        {
            CurrentTurnActor = actorNumber;
            _outbox.SetRoomState(RoomProps.TurnActor, actorNumber);
        }

        // TODO: 살아 있는 사람만 (기능 7).
        private int GetNextActor(int currentActor)
        {
            int index = _turnOrder.IndexOf(currentActor);
            return _turnOrder[(index + 1) % _turnOrder.Count];
        }
    }
}
