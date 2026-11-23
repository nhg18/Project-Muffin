using System.Collections.Generic;

namespace Chapchu.Game
{
    /// <summary>
    /// 처리 체인에 오른 카드 한 장 (04-card.md 9절). 대상은 낼 때 확정된다 (04 5 · 10절) — 바꾸는 것은 대상 변경 카운터(C02 · C04)뿐이다.
    /// 아래 카드를 바꾸는 표시는 위에 쌓인 카운터가 처리될 때 붙는다 (04 7절 「아래 카드를 바꾸는 카운터」) — 체인은 위에서부터 처리하므로 아래 카드보다 먼저 정해진다.
    /// </summary>
    internal sealed class ChainEntry
    {
        public Card Card { get; }
        public int User { get; }
        public int[] Targets { get; set; }

        /// <summary>함정이 쓰는 사건 값 — 「버려진 후」(T09)의 버려진 장수.</summary>
        public int TriggerAmount;

        /// <summary>효과 전체가 무효화됐다 — 효과 없이 버림 더미로 (ReturnTo 가 있으면 그 사람 손패로).</summary>
        public bool Negated;
        public int NegatedBy = -1;
        /// <summary>무효된 이 카드를 받을 사람 — 없으면 -1 (버림 더미로). 쓴 사람(2003 진짜 할거야?) · 카운터를 낸 사람(2020 내 거야!) (04 7절).</summary>
        public int ReturnTo = -1;
        public bool Returned;       // 손패로 돌아갔다 — 버림 더미에 쌓지 않는다 (04 9.1)

        /// <summary>「본인에게만 무효화」 한 사람들 (C05 · C08 · C13 · C15). 효과를 받는 사람에서 뺀다.</summary>
        public readonly HashSet<int> Excluded = new HashSet<int>();

        /// <summary>
        /// 「손패 − 내 몫 무효」 한 사람 → 그때 잃을 뻔한 장수 (2019 안 놔!, 04 7절 「무효 (내 몫 · 손패 −)」).
        /// 이 카드의 손패 감소 효과(빼앗기 · 버리기 · 주기)만 그 사람을 건너뛴다 — 피해 등 다른 효과는 그대로 받는다.
        /// </summary>
        public readonly Dictionary<int, int> HandLossBlocked = new Dictionary<int, int>();

        /// <summary>이 카드의 적힌 숫자(피해 · 회복 · 장수)를 2배로 읽는다 (2005 묻고 따블로 가, 04 7절 「수치 2배」). 한 카드에 1회 — 두 번 걸려도 그대로다.</summary>
        public bool Doubled;

        public readonly HashSet<int> DamageToHeal = new HashSet<int>();                     // C03
        public readonly HashSet<int> HealToDamage = new HashSet<int>();                     // C09
        public readonly Dictionary<int, int> DamageReduction = new Dictionary<int, int>(); // C10 — 받는 사람 → 줄일 양
        /// <summary>이 카드의 피해를 받는 사람 모두 — 각각 이만큼 줄인다 (2001 침착, 04 7절 「피해 감소 (모두)」).</summary>
        public int DamageReductionAll;
        /// <summary>이 카드의 효과를 다 적용한 직후에 회복할 사람 → 양 (2001 침착의 회복, 04 7절 「아래 카드 처리 직후의 효과」).</summary>
        public readonly Dictionary<int, int> HealAfter = new Dictionary<int, int>();
        /// <summary>
        /// 「피해 같이 받기」 — 이 카드의 피해를 받는 사람 → 그 사람이 낸 물귀신 (2018, 04 7절). 그 사람이 실제로 받은 양만큼 물귀신의 대상도 받는다.
        /// 물귀신의 카드를 그대로 가리킨다 — 고른 사람이 물귀신에 건 표시(칼집의 감소 등)가 거기에 있다.
        /// </summary>
        public readonly Dictionary<int, ChainEntry> DamageSharers = new Dictionary<int, ChainEntry>();

        public readonly Dictionary<int, int> SurviveAt = new Dictionary<int, int>();       // C11 — 받는 사람 → 남길 HP
        public readonly HashSet<int> ProtectTraps = new HashSet<int>();                     // C12 — 함정 주인

        /// <summary>낼 때 정한 값 — 효과 → 값 (1016 뽑을 장수). CardEffect.Prepare 가 채운다.</summary>
        public readonly Dictionary<Effects.CardEffect, int> Rolled = new Dictionary<Effects.CardEffect, int>();

        /// <summary>이 카드의 피해로 HP 가 0 이 된 사람 — 그 순간에 정한다 (사망 대기에서 살아나도 남는다). 1005 드롭킥 「못 쓰러뜨리면」 판정 (11-card-list 4절 #2).</summary>
        public readonly HashSet<int> KnockedDown = new HashSet<int>();

        /// <summary>체인에서 바로 아래 카드 (맨 아래면 null) — 체인에 오를 때 정한다. 카운터가 반응한 카드다 (04 7절).</summary>
        public ChainEntry Below;

        /// <summary>처리 중 — 다음에 적용할 효과 번호. 0 이면 아직 처리 전. 대상의 고르기(1019)로 멈췄다 이어 갈 때 쓴다 (04 9.3).</summary>
        public int NextEffect;

        public ChainEntry(Card card, int user, int[] targets)
        {
            Card = card;
            User = user;
            Targets = targets;
        }
    }
}
