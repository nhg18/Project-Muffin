using System;
using System.Collections.Generic;

namespace Chapchu.Game.Cards
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

    /// <summary>효과 종류 (11-card-list.md 5절). 필요한 것부터 늘린다 — 실행은 GameServer.ApplyEffect 의 switch 한 곳. 끝에만 추가한다.</summary>
    public enum EffectType
    {
        Damage      // amount 만큼 HP 감소
    }

    /// <summary>효과를 받는 사람. 한 카드 안에서도 효과마다 다르다 (예: A19 대상에게 피해 · 본인 회복).</summary>
    public enum EffectSubject
    {
        Target,     // 카드의 대상 (TargetType 으로 정해진 사람들)
        Self        // 카드를 쓴 사람
    }

    /// <summary>
    /// 카드 효과 하나 = 종류 · 받는 사람 · 수치. CardData 인스펙터에 바로 입력하고, 방장이 덱을 만들 때 Card 로 그대로 옮긴다.
    /// 수치의 뜻은 종류마다 정해진다 (데미지량 · 회복량 · 장수 …). 순수 데이터 — 실행은 서버.
    /// </summary>
    [Serializable]
    public struct CardEffect
    {
        public EffectType type;
        public EffectSubject subject;
        public int amount;
    }

    /// <summary>
    /// 게임 안의 카드 한 장. 서버(GameServer)가 판정에 쓰는 전부가 여기 있다 — 어느 장(InstanceId) · 무슨 카드(CardId) · 그 카드의 규칙값.
    /// 방장이 게임 시작 때 DeckData × CardData 로 한 번에 만들고(PunGameServer.BuildDeck), 그 뒤로는 존(덱 · 손패 · 버림 더미) 사이로 옮기기만 한다.
    /// 네트워크에는 이 객체가 아니라 InstanceId · CardId 두 int 만 다닌다. 화면은 CardId 로 CardData 를 찾아 그리고, 이 객체를 쓰지 않는다.
    /// 순수 C# (서버 어셈블리) — UnityEngine 참조 금지.
    /// </summary>
    public sealed class Card : IEquatable<Card>
    {
        /// <summary>장 고유 번호 (09-network.md 7절). 덱을 만들 때 1부터 매기고 게임 내내 유지된다. 같은 종류가 여러 장이어도 이것으로 구분한다.</summary>
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

        // 같음 비교는 InstanceId 만 본다. 손패 · 덱 · 체인에서 "그 한 장" 을 찾는 기준.
        public bool Equals(Card other) => other != null && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => InstanceId;

        public override string ToString() => $"카드 {CardId} (#{InstanceId})";
    }
}
