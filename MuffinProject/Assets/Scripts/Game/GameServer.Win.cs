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

            // 마감이 지난 요청 — 넘김은 다음 Tick 이 한다 (09-network.md 8절)
            if (_declareWait ? _clock() >= _nextTurnAt : !_mainActionUsed && IsTurnTimeUp(_clock()))
                return Reject(requester, RejectCode.NotYourTurn);

            SetChapchu(requester, true);
            AddStat(requester, StatKind.ChapchuDeclared); // 카드로 부여된 찹츄(GiveChapchu)는 선언이 아니다
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

        // 끝내면서 「게임 종료 요약」 을 만든다 (07 11-3). 순위: 승자 → 남은 사람(HP 높은 순) → 빠진 사람(늦게 빠진 순).
        // 같은 값이면 공동 순위 · 다음 순위는 건너뛴다. 무승부는 전원 1위. 대소동이 나간 사람을 턴 순서에서 빼므로 _players 로 센다.
        private void EndGame(int winner, GameEndReason reason)
        {
            _gameOver = true;

            int[] actors = _players.Keys
                .OrderBy(actor => RankGroup(actor, winner))
                .ThenByDescending(RankValue)
                .ThenBy(actor => actor)
                .ToArray();

            var ranks = new int[actors.Length];
            for (int i = 0; i < actors.Length; i++)
            {
                bool tied = i > 0 && RankGroup(actors[i], winner) == RankGroup(actors[i - 1], winner) && RankValue(actors[i]) == RankValue(actors[i - 1]);
                ranks[i] = reason == GameEndReason.Draw ? 1 : tied ? ranks[i - 1] : i + 1;
            }

            _events.Add(new GameEnded(winner, reason, actors, ranks,
                actors.Select(actor => _players[actor].Hp).ToArray(),
                actors.Select(actor => _players[actor].OutTurn).ToArray(),
                actors.Select(actor => _players[actor].DownedBy).ToArray(),
                actors.Select(actor => _players[actor].OutByLeaving).ToArray(),
                actors.SelectMany(actor => _stats[actor]).ToArray(),
                TurnSerial));
        }

        // 순위 묶음 — 0 승자 · 1 끝까지 남은 사람 · 2 빠진 사람(최종 사망 · 나감)
        private int RankGroup(int actor, int winner) => actor == winner ? 0 : IsInGame(actor) ? 1 : 2;

        // 묶음 안에서 큰 값이 위 — 남은 사람은 HP, 빠진 사람은 빠진 턴 번호
        private int RankValue(int actor) => IsInGame(actor) ? _players[actor].Hp : _players[actor].OutTurn;
    }
}
