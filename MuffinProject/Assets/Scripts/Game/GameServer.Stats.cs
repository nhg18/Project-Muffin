using System;
using System.Collections.Generic;

namespace Chapchu.Game
{
    /// <summary>
    /// 한 판 동안 방장이 플레이어마다 세는 지표 (07-win-condition.md 11-4). 게임 종료 요약에 이 순서대로 실린다 — 순서를 바꾸거나 중간에 끼우지 않는다.
    /// 「낸 카드 수」 는 Actions + Counters + Traps, 「생존 턴 수」 는 빠진 턴(없으면 끝난 턴)이라 따로 세지 않는다.
    /// </summary>
    public enum StatKind
    {
        Dealt,           // 가한 피해 — 내 카드로 남의 HP 가 실제로 줄어든 양
        Taken,           // 받은 피해 — 내 HP 가 피해로 실제로 줄어든 양 (빈 덱 · 피폭 · 자기 피해 포함)
        Healed,          // 회복 — 내 HP 가 회복으로 실제로 늘어난 양
        Kills,           // 킬 — 내 피해로 HP 0 이 된 사람이 최종 사망한 수 (11-3)
        Stolen,          // 빼앗은 카드 — 훔침으로 남의 손패에서 내 손패로 온 장수
        Lost,            // 빼앗긴 카드 — 훔침으로 내 손패에서 나간 장수
        Drawn,           // 카드 드로우 — 턴 뽑기 · 카드 효과로 실제로 뽑은 장수 (시작 손패 · 다시 받기 제외)
        Actions,         // 행동 사용 — 승인된 행동 카드 사용 수 (무효화돼도 센다)
        Counters,        // 카운터 사용 — 승인된 카운터 사용 수
        CounterSuccess,  // 카운터 성공 — 낸 카운터 중 무효화되지 않은 수
        Traps,           // 함정 사용 — 발동한 함정 수 (설치는 세지 않는다)
        ChapchuDeclared  // 찹츄 선언 — 승인된 선언 수
    }

    // GameServer.Stats.cs — 이 판의 지표 집계 (07-win-condition.md 11-4). 세는 곳은 각 규칙 파일, 여기는 담는 곳.
    public partial class GameServer
    {
        /// <summary>지표의 수 — 종료 요약의 지표 배열은 사람마다 이만큼이다.</summary>
        public static readonly int StatCount = Enum.GetValues(typeof(StatKind)).Length;

        // PlayerState 에 두지 않는다 — 대폭발이 PlayerState 를 새로 만들어도 지표는 새 판으로 이어진다 (11-card-list 112). 나간 사람 것도 남는다.
        private readonly Dictionary<int, int[]> _stats = new Dictionary<int, int[]>();

        private void ResetStats(IReadOnlyList<int> actors)
        {
            _stats.Clear();
            foreach (int actor in actors)
                _stats[actor] = new int[StatCount];
        }

        private void AddStat(int actor, StatKind kind, int amount = 1)
        {
            if (amount > 0 && _stats.TryGetValue(actor, out int[] stats))
                stats[(int)kind] += amount;
        }

        // 피해 1건 — lost = HP 가 실제로 줄어든 양 (0 아래로는 줄지 않는다). 받은 사람의 「받은 피해」, 주인이 남이면 주인의 「가한 피해」.
        private void CountDamage(int actor, int source, int lost)
        {
            AddStat(actor, StatKind.Taken, lost);
            if (source >= 0 && source != actor)
                AddStat(source, StatKind.Dealt, lost);
        }
    }
}
