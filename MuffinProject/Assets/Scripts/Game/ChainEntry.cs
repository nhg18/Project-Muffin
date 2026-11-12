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

        /// <summary>효과 전체가 무효화됐다 — 효과 없이 버림 더미로 (ReturnToCaster 면 시전자 손패로).</summary>
        public bool Negated;
        public int NegatedBy = -1;
        public bool ReturnToCaster; // C01

        /// <summary>「본인에게만 무효화」 한 사람들 (C05 · C08 · C13 · C15). 효과를 받는 사람에서 뺀다.</summary>
        public readonly HashSet<int> Excluded = new HashSet<int>();

        public readonly HashSet<int> DamageToHeal = new HashSet<int>();                     // C03
        public readonly HashSet<int> HealToDamage = new HashSet<int>();                     // C09
        public readonly Dictionary<int, int> DamageReduction = new Dictionary<int, int>(); // C10 — 받는 사람 → 줄일 양
        public readonly Dictionary<int, int> SurviveAt = new Dictionary<int, int>();       // C11 — 받는 사람 → 남길 HP
        public readonly HashSet<int> ProtectTraps = new HashSet<int>();                     // C12 — 함정 주인

        /// <summary>낼 때 정한 값 — 효과 → 값 (116 뽑을 장수). CardEffect.Prepare 가 채운다.</summary>
        public readonly Dictionary<Effects.CardEffect, int> Rolled = new Dictionary<Effects.CardEffect, int>();

        public ChainEntry(Card card, int user, int[] targets)
        {
            Card = card;
            User = user;
            Targets = targets;
        }
    }
}
