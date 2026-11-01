using System.Collections.Generic;

namespace Chapchu.Game
{
    // GameServer.Turn.cs — 턴 순서 · 턴 넘기기 · 제한 시간 (03-turn.md)
    public partial class GameServer
    {
        // 확정 (03-turn.md 2절).
        private const double TurnTimeLimit = 20;

        private double _turnDeadline;

        /// <summary>방장이 매 프레임 부른다. 마감이 지나면 아무것도 하지 않은 것으로 보고 넘긴다 (03-turn.md 4절 — 자동 제출 없음).</summary>
        public IReadOnlyList<GameEvent> Tick()
        {
            if (CurrentTurnActor != -1 && _clock() >= _turnDeadline)
                AdvanceTurn();

            return TakeEvents();
        }

        /// <summary>나간 사람을 턴 순서에서 뺀다. 턴 주인이었으면 다음 사람에게 넘긴다 (01-game-flow.md 6절 · 03-turn.md 7절).</summary>
        public IReadOnlyList<GameEvent> RemovePlayer(int actor)
        {
            if (!_turnOrder.Contains(actor))
                return TakeEvents();

            if (actor == CurrentTurnActor)
                AdvanceTurn(); // 빼기 전에 다음 사람을 구한다

            _turnOrder.Remove(actor);
            return TakeEvents();
        }

        // 참가자 순서를 무작위로 섞어 턴 순서를 정하고 첫 사람에게 턴을 준다 (03-turn.md 6절).
        private void StartFirstTurn()
        {
            _turnOrder.Clear();
            _turnOrder.AddRange(_players.Keys);
            Shuffle(_turnOrder);

            SetTurn(_turnOrder[0]);
        }

        // 턴을 넘기는 곳은 여기 하나다 — 뽑기 · 카드 사용 · 시간 초과 · 나감이 모두 이걸 부른다.
        private void AdvanceTurn()
        {
            SetTurn(GetNextActor(CurrentTurnActor));
        }

        private void SetTurn(int actor)
        {
            CurrentTurnActor = actor;
            _turnDeadline = _clock() + TurnTimeLimit;

            _events.Add(new TurnStarted(actor, _turnDeadline));
        }

        // TODO: 살아 있는 사람만 (기능 7).
        private int GetNextActor(int currentActor)
        {
            int index = _turnOrder.IndexOf(currentActor);
            return _turnOrder[(index + 1) % _turnOrder.Count];
        }
    }
}
