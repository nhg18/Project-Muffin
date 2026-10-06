using System.Collections.Generic;
using Chapchu.Core;

namespace Chapchu.Game
{
    // GameServer.Turn.cs
    public partial class GameServer
    {
        // 확정(03-turn.md 2절).
        private const double TurnTimeLimit = 20;

        private double _turnDeadline;

        // 게임 시작 1회: 참가자 순서를 무작위로 섞어 턴 순서를 정하고 첫 사람에게 턴을 준다 (03-turn.md 6절).
        private void InitTurnOrder(IReadOnlyList<int> actors)
        {
            _turnOrder.Clear();
            _turnOrder.AddRange(actors);
            Shuffle(_turnOrder);

            SetTurn(_turnOrder[0]);
        }

        // 방장이 매 프레임 부른다. 마감이 지나면 아무것도 하지 않은 것으로 보고 넘긴다 (03-turn.md 4절 — 자동 제출 없음).
        public void Tick()
        {
            if (CurrentTurnActor == -1) return;

            if (_clock() >= _turnDeadline)
                AdvanceTurn();
        }

        // 턴 순서에서 뺀다. 턴 주인이었으면 다음 사람에게 넘긴다 (03-turn.md 7절).
        public void RemoveFromTurnOrder(int actor)
        {
            if (!_turnOrder.Contains(actor)) return;

            if (actor == CurrentTurnActor)
                AdvanceTurn(); // 빼기 전에 다음 사람을 구한다

            _turnOrder.Remove(actor);
        }

        // 턴을 넘기는 곳은 여기 하나다 — 뽑기 · 카드 사용 · 시간 초과 · 나감이 모두 이걸 부른다. 턴 종료 요청은 없다 (자동 넘김만).
        private void AdvanceTurn()
        {
            SetTurn(GetNextActor(CurrentTurnActor));
        }

        private void SetTurn(int actorNumber)
        {
            CurrentTurnActor = actorNumber;
            _turnDeadline = _clock() + TurnTimeLimit;

            _outbox.SetRoomState(RoomProps.TurnActor, actorNumber);
            _outbox.SetRoomState(RoomProps.TurnDeadline, _turnDeadline);
        }

        // TODO: 살아 있는 사람만 (기능 7).
        private int GetNextActor(int currentActor)
        {
            int index = _turnOrder.IndexOf(currentActor);
            return _turnOrder[(index + 1) % _turnOrder.Count];
        }
    }
}
