using System.Collections.Generic;
using System.Linq;
using Chapchu.Game;
using Chapchu.Network;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면이 보이는 「판 지표」 8개와 지표마다의 1등 업적 (07-win-condition.md 11-2, 시안 「결과화면 UI v3」 STATS · awards).
    /// 값은 방장이 보낸 종료 요약 그대로 — 여기서는 누가 1등인지만 가린다. 시상대 배지와 「판 기록」 판이 함께 쓴다.
    /// </summary>
    public static class ResultAwards
    {
        /// <summary>지표 하나 — 「판 기록」 의 줄 이름 · 1등 업적 이름 · 배지에 적는 값 글.</summary>
        public readonly struct Metric
        {
            public readonly StatKind Kind;
            public readonly string Name;
            public readonly string Award;
            public readonly string ValueFormat;

            public Metric(StatKind kind, string name, string award, string valueFormat)
            {
                Kind = kind;
                Name = name;
                Award = award;
                ValueFormat = valueFormat;
            }
        }

        /// <summary>한 사람이 받은 업적 하나.</summary>
        public readonly struct Award
        {
            /// <summary>Metrics 의 자리.</summary>
            public readonly int Metric;
            public readonly int Value;
            /// <summary>2등과의 비율 — 클수록 압도적. 한 사람의 배지는 이 값이 큰 것부터 보인다.</summary>
            public readonly float Lead;

            public Award(int metric, int value, float lead)
            {
                Metric = metric;
                Value = value;
                Lead = lead;
            }
        }

        // 2등이 0 일 때(혼자만 값이 있다)의 비율 — 가장 앞에 온다 (시안 99)
        private const float SoleLead = 99f;

        /// <summary>화면에 보이는 지표 — 이 순서가 「판 기록」 의 줄 순서이고, 아이콘 배열도 이 순서다.</summary>
        public static readonly Metric[] Metrics =
        {
            new Metric(StatKind.Dealt, "입힌 데미지", "물어뜯기 장인", "피해 {0}"),
            new Metric(StatKind.Taken, "입은 데미지", "동네북", "받은 피해 {0}"),
            new Metric(StatKind.Stolen, "빼앗은 카드", "간식 도둑", "{0}장 빼앗음"),
            new Metric(StatKind.Lost, "빼앗긴 카드", "털린 밥그릇", "{0}장 빼앗김"),
            new Metric(StatKind.Drawn, "카드 드로우", "카드 폭식", "{0}장 뽑음"),
            new Metric(StatKind.Actions, "행동 사용", "바쁜 발바닥", "행동 {0}회"),
            new Metric(StatKind.Counters, "카운터 사용", "되치기 고수", "카운터 {0}회"),
            new Metric(StatKind.Traps, "함정 사용", "함정왕", "함정 {0}회"),
        };

        /// <summary>
        /// 지표마다 1등을 가린다. 1등이 여럿이면 모두 받고, 가장 큰 값이 0 이면 아무도 받지 않는다.
        /// tops[지표][사람] = 그 지표의 1등인지, byPlayer[사람] = 받은 업적 (2등과의 비율이 큰 순 → 지표 순). 사람 자리는 result.Actors 순서.
        /// </summary>
        public static void Compute(GameResult result, out bool[][] tops, out List<Award>[] byPlayer)
        {
            int count = result.Actors.Count;
            tops = new bool[Metrics.Length][];
            byPlayer = Enumerable.Range(0, count).Select(_ => new List<Award>()).ToArray();

            for (int m = 0; m < Metrics.Length; m++)
            {
                int[] values = Enumerable.Range(0, count).Select(i => result.Stat(i, Metrics[m].Kind)).ToArray();
                int best = values.Max();
                tops[m] = values.Select(value => best > 0 && value == best).ToArray();
                if (best <= 0)
                    continue;

                int winners = values.Count(value => value == best);
                int second = values.Where(value => value < best).DefaultIfEmpty(0).Max();
                float lead = winners > 1 ? 1f : second > 0 ? (float)best / second : SoleLead;
                for (int i = 0; i < count; i++)
                {
                    if (tops[m][i])
                        byPlayer[i].Add(new Award(m, best, lead));
                }
            }

            // List.Sort 는 순서를 지키지 않는다 — 같은 비율이면 지표 순이 되게 OrderBy 로
            for (int i = 0; i < count; i++)
                byPlayer[i] = byPlayer[i].OrderByDescending(award => award.Lead).ThenBy(award => award.Metric).ToList();
        }
    }
}
