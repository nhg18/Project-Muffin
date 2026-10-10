using System;

namespace Chapchu.Game.Cards
{
    public enum CardType
    {
        Action,
        Counter,
        Trap
    }

    public enum TargetType
    {
        None,
        SingleEnemy,
        TwoEnemy,
        AllEnemies,
        Me,
        AllPlayers
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

        /// <summary>대상마다 체력 감소량. 0 이면 데미지 없음. 효과는 기능 9 에서 늘린다 (11-card-list.md 5절).</summary>
        public int Damage { get; }

        public Card(int instanceId, int cardId, CardType type, TargetType target, int damage)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Type = type;
            Target = target;
            Damage = damage;
        }

        // 같음 비교는 InstanceId 만 본다. 손패 · 덱 · 체인에서 "그 한 장" 을 찾는 기준.
        public bool Equals(Card other) => other != null && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => InstanceId;

        public override string ToString() => $"카드 {CardId} (#{InstanceId})";
    }
}
