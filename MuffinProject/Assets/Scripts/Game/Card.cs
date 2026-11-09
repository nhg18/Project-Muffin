using System;
using System.Collections.Generic;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    public enum CardType
    {
        Action,
        Counter,
        Trap
    }

    // 카드 에셋에는 이름이 아니라 순서 번호로 저장된다. 이름은 바꿔도 되지만 순서를 바꾸거나 중간에 끼우지 않는다 (04-card.md 11절).
    public enum TargetType
    {
        None,       // 대상 없음 — 효과 1회
        OneOther,   // 나를 뺀 1명 고름
        TwoOthers,  // 나를 뺀 2명 고름
        AllOthers,  // 나를 뺀 전원 (자동)
        Self,       // 나 (자동)
        Everyone,   // 나를 포함한 전원 (자동)
        TriggerSource, // 함정을 발동시킨 사람 — 피해를 준 · 반응한 카드를 낸 플레이어 (자동, 04-card.md 11절)
        ChapchuPlayers, // 찹츄 상태 플레이어 전원, 나 포함 (자동 — A02)
        ChapchuOthers,  // 나를 뺀 찹츄 상태 플레이어 전원 (자동 — T04)
        MoreHandThanMe  // 나보다 손패가 많은 모든 플레이어 — 이 카드를 낸 뒤의 내 장수와 비교 (자동 — A23)
    }

    // 함정의 발동 조건 종류 (04-card.md 6절 「발동 시점」). 카드 에셋에는 순서 번호로 저장된다 — 끝에만 추가한다.
    public enum TrapTrigger
    {
        None,             // 함정 아님
        ReactToCard,      // 체인 맨 위 카드의 종류가 ReactsTo 에 있다 — 반응 시간에 (T03 · T05)
        AfterDamaged,     // 직전 체인에서 다른 플레이어에게 피해를 받았다 — 함정 시간에 (T06 · T07)
        HpAtMost,         // 본인 HP 가 TriggerValue 이하 — 언제든 (T02)
        SelfDeathPending, // 본인이 사망 대기 — 사망 대기 동안 (T01 · T17)
        ChapchuExists,    // 나 아닌 찹츄 상태 플레이어가 있다 — 언제든 (T04)
        AfterStolen,      // 다른 사람이 내 손패를 가져간 뒤 — 함정 시간에 (T08)
        AfterDiscarded,   // 다른 사람의 효과로 내 손패가 버려진 뒤 — 버려진 장수를 효과가 쓴다 (T09)
        AfterTrapDestroyed, // 다른 사람이 내 함정을 파괴한 뒤 (T10)
        AfterMyTrapNegated, // 내가 낸 다른 함정이 카운터로 무효된 뒤 (T12)
        AfterCardReturned,  // 행동 카드가 시전자 손패로 반환된 뒤 — 누구든 (T13)
        AfterDrewMany,    // 다른 사람이 카드 효과로 한 번에 TriggerValue 장 이상 뽑은 뒤 (T14)
        AfterTurnSkipped, // 내 턴이 스킵된 뒤 (T15)
        AfterChapchuGiven // 다른 사람이 나에게 찹츄를 부여한 뒤 (T16)
    }

    // 카운터 · 「카드 반응」 함정의 반응 조건 (04-card.md 7절 「반응 조건」). 맨 위 카드를 보고 판정한다. 끝에만 추가한다.
    public enum ReactCondition
    {
        Any,            // 반응 대상 종류만 맞으면 (C01 · C05 · C06)
        ByOther,        // 다른 사람이 낸 카드 (C02 · C04 · C07 · T03)
        TargetsMe,      // 다른 사람이 나를 대상으로 낸 카드 (T05)
        DamagesMe,      // 그 카드의 피해를 내가 받는다 (C03 · C10)
        WouldKillMe,    // 그 카드에서 받을 피해 합이 지금 HP 이상 (C11)
        HealsOther,     // 그 카드의 회복을 나 아닌 사람이 받는다 (C09)
        TakesMyHand,    // 그 카드가 내 손패를 훔치거나 버리게 한다 (C08)
        DestroysMyTrap, // 다른 사람이 낸 그 카드가 내 함정을 파괴한다 (C12)
        SkipsMe,        // 그 카드가 나에게 턴 스킵을 건다 (C13)
        GivesMeChapchu  // 다른 사람이 낸 그 카드가 나에게 찹츄를 준다 (C15)
    }

    /// <summary>
    /// 게임 안의 카드 한 장 (09-network.md 7절). 방장이 게임 시작 때 덱 에셋으로 한 번에 만들고,
    /// 그 뒤로는 덱 · 손패 · 버림 더미 사이로 옮기기만 한다. 네트워크에는 InstanceId · CardId 두 int 만 다닌다.
    /// </summary>
    public sealed class Card : IEquatable<Card>
    {
        /// <summary>장 고유 번호. 덱을 만들 때 1부터 매기고 게임 내내 유지된다. 같은 종류가 여러 장이어도 이것으로 구분한다.</summary>
        public int InstanceId { get; }

        /// <summary>종류 번호 = CardData.id. 같은 종류는 같은 값.</summary>
        public int CardId { get; }

        public CardType Type { get; }
        public TargetType Target { get; }

        /// <summary>카드에 적힌 순서대로 실행할 효과들. 비어 있으면 효과 없음.</summary>
        public IReadOnlyList<CardEffect> Effects { get; }

        /// <summary>카운터 · 「카드 반응」 함정: 체인 맨 위 카드가 이 종류일 때 낼 수 있다 (04-card.md 6 · 7절). 비어 있으면 어디에도 못 낸다.</summary>
        public IReadOnlyList<CardType> ReactsTo { get; }

        /// <summary>함정만: 발동 조건 종류와 그 값 (HpAtMost 의 HP · AfterDrewMany 의 장수, 04-card.md 6절).</summary>
        public TrapTrigger Trigger { get; }
        public int TriggerValue { get; }

        /// <summary>카운터 · 「카드 반응」 함정: 반응 조건 (04-card.md 7절).</summary>
        public ReactCondition Condition { get; }

        /// <summary>연출 1 ~ 3 단계 길이(초). 반응 시간이 이만큼 뒤에 열린다 (04-card.md 8절 · 10-ui.md 5-1).</summary>
        public double RevealSeconds { get; }

        /// <summary>연출 4 단계 + 버림 더미로 이동 길이(초). 체인 맨 아래 카드면 체인 처리 뒤 이만큼 지나 다음 턴 (04-card.md 8절).</summary>
        public double ResolveSeconds { get; }

        public Card(int instanceId, int cardId, CardType type, TargetType target, IReadOnlyList<CardEffect> effects,
            IReadOnlyList<CardType> reactsTo, double revealSeconds, double resolveSeconds,
            TrapTrigger trigger = TrapTrigger.None, int triggerValue = 0, ReactCondition condition = ReactCondition.Any)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Type = type;
            Target = target;
            Effects = effects ?? Array.Empty<CardEffect>();
            ReactsTo = reactsTo ?? Array.Empty<CardType>();
            RevealSeconds = revealSeconds;
            ResolveSeconds = resolveSeconds;
            Trigger = trigger;
            TriggerValue = triggerValue;
            Condition = condition;
        }

        // 같음 비교는 InstanceId 만 본다. 손패 · 덱에서 "그 한 장" 을 찾는 기준.
        public bool Equals(Card other) => other != null && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => InstanceId;

        public override string ToString() => $"카드 {CardId} (#{InstanceId})";
    }
}
