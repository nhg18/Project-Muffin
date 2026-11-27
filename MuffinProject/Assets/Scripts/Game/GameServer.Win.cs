using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Win.cs — 찹츄 자동 선언 · 승리 판정 · 게임 종료 (07-win-condition.md)
    public partial class GameServer
    {
        /// <summary>찹츄 선언 장수 — 정확히 10장 (07-win-condition.md 4.1 확정). 화면의 찹츄 자리도 이 값을 쓴다.</summary>
        public const int ChapchuHandCount = 10;

        private bool _gameOver;
        private bool _judgesElimination; // 혼자 시작한 게임(개발용)은 처치 승리를 판정하지 않는다 (07 3절 제안)

        public bool IsGameOver => _gameOver;

        // 손패가 바뀐 사람 — 처리 한 덩어리(효과 하나 · 뽑기 한 번 · 카드 한 장 내기)가 끝난 뒤의 장수로 선언을 판정한다 (SettleChapchu)
        private readonly HashSet<int> _handDirty = new HashSet<int>();

        // 손패 장수가 바뀌는 곳마다 부른다. 선언 뒤에는 장수가 바뀌어도 찹츄가 풀리지 않는다 — 장수는 턴 시작 판정 때만 본다 (07 4.2).
        private void OnHandChanged(int actor) => _handDirty.Add(actor);

        // 자동 선언 (07-win-condition.md 4.1, 2026-11-14) — 손패가 바뀐 사람 중 정확히 10장인 사람을 찹츄 상태로. 내 턴 · 남의 턴을 가리지 않는다.
        // 효과 하나가 끝난 뒤의 장수만 본다 — 여러 장을 뽑다 지나친 10장은 선언이 아니다 (PM 2026-11-19).
        // HP 0 · 사망 대기 중에는 선언하지 않는다. 살아나면 그때 10장인지 다시 본다 (SettleDeaths · SurviveNow, PM 2026-11-19).
        private void SettleChapchu()
        {
            if (_handDirty.Count == 0)
                return;

            foreach (int actor in _turnOrder) // 여럿이 함께 10장이 되면 턴 순서대로 알린다
            {
                if (!_handDirty.Contains(actor) || _gameOver || !IsInGame(actor))
                    continue;

                PlayerState player = _players[actor];
                if (player.IsChapchu || player.Life != LifeState.Alive || player.Hp <= 0 || player.Hand.Count != ChapchuHandCount)
                    continue;

                SetChapchu(actor, true);
                AddStat(actor, StatKind.ChapchuDeclared); // 카드로 부여된 찹츄(GiveChapchu)는 선언이 아니다
            }
            _handDirty.Clear();
        }

        private void SetChapchu(int actor, bool value)
        {
            _players[actor].IsChapchu = value;
            _events.Add(new ChapchuChanged(actor, value));
        }

        // 처치 승리 · 무승부 (07 3절). 체인이 끝난 뒤에만 판정한다 — 사망 정리(SettleDeaths)에서 누가 죽으면 그 자리에서.
        // 고르기(1019)로 멈춘 처리도 체인 중이다 — 이어 간 처리 끝(ContinueResolve)에서 다시 판정한다.
        private void CheckElimination()
        {
            if (_gameOver || !_judgesElimination || _chain.Count > 0 || IsResolvePaused || IsChoosing)
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
