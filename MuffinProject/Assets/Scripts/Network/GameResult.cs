using System.Collections.Generic;
using Chapchu.Game;

namespace Chapchu.Network
{
    /// <summary>
    /// 방금 끝난 판의 「게임 종료 요약」 (07-win-condition.md 11-3 · 11-4) — 방장이 보낸 값 그대로. 결과 화면은 이것만 보고 그린다.
    /// 게임 씬에서 받고 결과 씬에서 읽으므로 씬을 넘어 사는 NetworkManager 가 든다 (NetworkManager.LastResult).
    /// 배열은 모두 같은 길이이고 순위 순이다. 나간 사람도 들어 있다.
    /// </summary>
    public sealed class GameResult
    {
        /// <summary>승자 actorNumber. 무승부면 -1.</summary>
        public int Winner { get; }
        public GameEndReason Reason { get; }
        public IReadOnlyList<int> Actors { get; }
        /// <summary>표시 이름의 원본 — 게임을 시작할 때의 닉네임 (나간 사람도 남는다). 봇은 「봇 N」. 화면에 쓸 때는 걸러서 쓴다 (NicknameValidator.ForDisplay).</summary>
        public IReadOnlyList<string> Names { get; }
        /// <summary>순위 — 1부터. 같은 값이면 공동 순위 (1 · 2 · 2 · 4), 무승부는 전원 1.</summary>
        public IReadOnlyList<int> Ranks { get; }
        public IReadOnlyList<int> Hps { get; }
        /// <summary>게임에서 빠진 턴 번호 (최종 사망 · 나감). 끝까지 남았으면 0.</summary>
        public IReadOnlyList<int> OutTurns { get; }
        /// <summary>HP 를 0 으로 만든 사람. 없으면 -1.</summary>
        public IReadOnlyList<int> DownedBy { get; }
        /// <summary>게임 도중 방을 나가서 빠졌는지.</summary>
        public IReadOnlyList<bool> Left { get; }
        /// <summary>게임이 끝난 턴 번호 — 끝까지 남은 사람의 「생존 턴 수」.</summary>
        public int EndTurn { get; }

        private readonly int[] _stats; // 사람마다 GameServer.StatCount 개 — Actors 순서대로 이어 붙인 것

        internal GameResult(int winner, GameEndReason reason, int[] actors, string[] names, int[] ranks, int[] hps, int[] outTurns, int[] downedBy, bool[] left, int[] stats, int endTurn)
        {
            Winner = winner;
            Reason = reason;
            Actors = actors;
            Names = names;
            Ranks = ranks;
            Hps = hps;
            OutTurns = outTurns;
            DownedBy = downedBy;
            Left = left;
            _stats = stats;
            EndTurn = endTurn;
        }

        /// <summary>index 번째 사람(Actors 순서)의 지표 값 (07 11-4).</summary>
        public int Stat(int index, StatKind kind) => _stats[index * GameServer.StatCount + (int)kind];
    }
}
