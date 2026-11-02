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
        Everyone    // 나를 포함한 전원 (자동)
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

        public Card(int instanceId, int cardId, CardType type, TargetType target, IReadOnlyList<CardEffect> effects)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Type = type;
            Target = target;
            Effects = effects ?? Array.Empty<CardEffect>();
        }

        // 같음 비교는 InstanceId 만 본다. 손패 · 덱에서 "그 한 장" 을 찾는 기준.
        public bool Equals(Card other) => other != null && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => InstanceId;

        public override string ToString() => $"카드 {CardId} (#{InstanceId})";
    }
}
