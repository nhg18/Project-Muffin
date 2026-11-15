using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Win.cs — 찹츄 선언 · 해제 · 승리 판정 · 게임 종료 (07-win-condition.md)
    public partial class GameServer
    {
        /// <summary>찹츄 선언 장수 — 정확히 10장 (07-win-condition.md 4.1 확정). 화면의 찹츄 버튼도 이 값을 쓴다.</summary>
        public const int ChapchuHandCount = 10;

        private bool _gameOver;
        private bool _judgesElimination; // 혼자 시작한 게임(개발용)은 처치 승리를 판정하지 않는다 (07 3절 제안)

        public bool IsGameOver => _gameOver;

        /// <summary>찹츄 선언 (07-win-condition.md 4.1). 내 턴에만 — 살아 있고 손패가 정확히 10장일 때. 선언 대기 중에 승인되면 그 순간 턴이 끝난다 (03-turn.md 4절).</summary>
        public IReadOnlyList<GameEvent> DeclareChapchu(int requester)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            // 참가자가 아닌 요청(시작 전 · 방장이 바뀐 뒤)도 여기서 거절한다 — 원본을 꺼내기 전에
            if (!InGame(requester) || _players[requester].Life != LifeState.Alive)
                return Reject(requester, RejectCode.NotAlive);

            if (requester != CurrentTurnActor)
                return Reject(requester, RejectCode.NotYourTurn); // 남의 턴에는 선언할 수 없다 (07 4.1, 2026-11-07)

            PlayerState player = _players[requester];

            if (player.IsChapchu)
                return Reject(requester, RejectCode.AlreadyChapchu);

            if (player.Hand.Count != ChapchuHandCount)
                return Reject(requester, RejectCode.NotTenCards);

            SetChapchu(requester, true);
            if (_declareWait)
                AdvanceTurn(); // 선언 대기 중 — 승인된 순간 턴 종료, 남은 시간을 기다리지 않는다 (03-turn.md 4절)

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

        // 처치 승리 · 무승부 (07 3절). 체인 · 함정 시간 · 사망 대기가 모두 끝난 뒤에만 판정한다.
        private void CheckElimination()
        {
            if (_gameOver || !_judgesElimination || _chain.Count > 0 || IsTrapWindowOpen || IsDeathPending)
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
