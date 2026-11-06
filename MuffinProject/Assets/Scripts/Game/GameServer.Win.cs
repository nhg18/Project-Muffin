using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Win.cs — 찹츄 선언 · 해제 · 승리 판정 · 게임 종료 (07-win-condition.md)
    public partial class GameServer
    {
        // 확정 (07-win-condition.md 4.1).
        private const int ChapchuHandCount = 10;

        private bool _gameOver;
        private bool _judgesElimination; // 혼자 시작한 게임(개발용)은 처치 승리를 판정하지 않는다 (07 3절 제안)

        public bool IsGameOver => _gameOver;

        /// <summary>찹츄 선언 (07-win-condition.md 4.1). 살아 있고 손패가 정확히 10장이면 턴과 관계없이 언제든.</summary>
        public IReadOnlyList<GameEvent> DeclareChapchu(int requester)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            PlayerState player = _players[requester];
            if (!IsInGame(requester) || player.Life != LifeState.Alive)
                return Reject(requester, RejectCode.NotAlive);

            if (player.IsChapchu)
                return Reject(requester, RejectCode.AlreadyChapchu);

            if (player.Hand.Count != ChapchuHandCount)
                return Reject(requester, RejectCode.NotTenCards);

            SetChapchu(requester, true);
            return TakeEvents();
        }

        // 손패 장수가 바뀌는 곳마다 부른다 — 10장이 아니게 되는 순간 즉시 해제, 자동 재선언 없음 (07 4.2).
        private void OnHandChanged(int actor)
        {
            PlayerState player = _players[actor];
            if (player.IsChapchu && player.Hand.Count != ChapchuHandCount)
                SetChapchu(actor, false);
        }

        private void SetChapchu(int actor, bool value)
        {
            _players[actor].IsChapchu = value;
            _events.Add(new ChapchuChanged(actor, value));
        }

        // 처치 승리 · 무승부 (07 3절). 체인 · 사망 대기가 모두 끝난 뒤에만 판정한다.
        private void CheckElimination()
        {
            if (_gameOver || !_judgesElimination || _chain.Count > 0 || IsDeathPending)
                return;

            int[] remaining = _turnOrder.Where(IsInGame).ToArray();
            if (remaining.Length == 1)
                EndGame(remaining[0], GameEndReason.Elimination);
            else if (remaining.Length == 0)
                EndGame(-1, GameEndReason.Draw);
        }

        private void EndGame(int winner, GameEndReason reason)
        {
            _gameOver = true;
            _events.Add(new GameEnded(winner, reason));
        }
    }
}
