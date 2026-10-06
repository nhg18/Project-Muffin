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
    /// 서버(GameServer)가 판정에 쓰는 카드 규칙. 서버는 순수 C# 이라 CardData(ScriptableObject)를 직접 보지 않고 이것만 받는다.
    /// 지금은 데미지(행동)와 본인만 무효(카운터 C05)만 둔다 (04-card.md · 11-card-list.md 5절). 효과 종류는 기능 9 에서 늘린다.
    /// </summary>
    public class CardRule
    {
        public int Id { get; }
        public CardType Type { get; }
        public TargetType Target { get; }
        public int Damage { get; } // 대상마다 체력 감소량. 0 이면 데미지 없음
        public bool NegateForSelf { get; } // 카운터: 반응한 행동 카드의 효과를 나에게만 무효 (C05). 반응 조건 = 행동 카드에만

        public CardRule(int id, CardType type, TargetType target, int damage, bool negateForSelf)
        {
            Id = id;
            Type = type;
            Target = target;
            Damage = damage;
            NegateForSelf = negateForSelf;
        }
    }
}
