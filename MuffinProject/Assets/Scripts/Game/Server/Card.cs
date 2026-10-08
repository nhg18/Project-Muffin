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
    /// 게임 안의 카드 한 장. 장마다 고유한 InstanceId 와 종류 규칙(Type · Target · Damage)을 함께 든다 (09-network.md 7절).
    /// InstanceId 0 은 종류 템플릿 — CardData.ToCard(0) 이 만들고, 서버가 덱을 만들 때 번호를 매겨 실제 장을 찍는다.
    /// 순수 C# — 서버(GameServer)와 화면(CardView) 둘 다 쓴다. UnityEngine 참조 금지.
    /// </summary>
    public sealed class Card : IEquatable<Card>
    {
        /// <summary>장 고유 번호. 게임 시작 때 덱을 만들며 매기고, 게임 내내 유지된다. 0 = 종류 템플릿.</summary>
        public int InstanceId { get; }

        /// <summary>종류 번호 = CardData.id. 같은 종류는 같은 값.</summary>
        public int CardId { get; }

        public CardType Type { get; }
        public TargetType Target { get; }

        /// <summary>대상마다 체력 감소량. 0 이면 데미지 없음.</summary>
        public int Damage { get; }

        public Card(int instanceId, int cardId, CardType type, TargetType target, int damage)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Type = type;
            Target = target;
            Damage = damage;
        }

        public bool IsAction => Type == CardType.Action;
        public bool IsCounter => Type == CardType.Counter;
        public bool IsTrap => Type == CardType.Trap;

        // 같음 비교는 InstanceId 만 본다. 손패 · 덱 · 체인에서 "그 한 장" 을 찾는 기준.
        public bool Equals(Card other) => other != null && InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => InstanceId;

        public override string ToString() => $"카드 {CardId} (#{InstanceId})";
    }
}
