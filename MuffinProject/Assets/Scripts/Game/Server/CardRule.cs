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
    /// 카드 종류 1개의 규칙 — 서버(GameServer)가 판정에 쓴다. 서버는 순수 C# 이라 CardData(ScriptableObject)를 직접 보지 못하고,
    /// 게임 시작 때 CardData.ToRule() 로 뽑은 이것을 InitCards 로 받아 CardId → CardRule 테이블에 둔다. 한 장(Card)은 CardId 로 여기를 찾는다.
    /// 지금은 기능 4 경로 검증에 필요한 데미지 효과만 둔다 (04-card.md · 11-card-list.md 5절). 효과 종류는 기능 9 에서 늘린다.
    /// </summary>
    public class CardRule
    {
        public int Id { get; }
        public CardType Type { get; }
        public TargetType Target { get; }
        public int Damage { get; } // 대상마다 체력 감소량. 0 이면 데미지 없음

        public CardRule(int id, CardType type, TargetType target, int damage)
        {
            Id = id;
            Type = type;
            Target = target;
            Damage = damage;
        }
    }
}
