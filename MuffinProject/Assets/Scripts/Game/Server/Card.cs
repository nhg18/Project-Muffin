using System;

namespace Chapchu.Game.Cards
{
    /// <summary>
    /// 게임 안의 카드 한 장 (09-network.md 7절). 같은 종류(CardId)가 여러 장이어도 InstanceId 로 구분한다.
    /// 종류 규칙(타입 · 대상 · 데미지)은 들지 않는다 — 서버는 CardId 로 CardRule 을, 화면은 CardId 로 CardData 를 찾는다.
    /// 네트워크에는 이 객체가 아니라 InstanceId · CardId 두 int 만 다닌다. 순수 C# (서버 어셈블리).
    /// </summary>
    [Serializable]
    public struct Card : IEquatable<Card>
    {
        /// <summary>장 고유 번호. 서버가 덱을 만들 때 1부터 매기고, 게임 내내 유지된다.</summary>
        public int InstanceId;

        /// <summary>종류 번호 = CardData.id = CardRule.Id. 같은 종류는 같은 값.</summary>
        public int CardId;

        public Card(int instanceId, int cardId)
        {
            InstanceId = instanceId;
            CardId = cardId;
        }

        // 같음 비교는 InstanceId 만 본다. 손패 · 덱 · 체인에서 "그 한 장" 을 찾는 기준.
        public bool Equals(Card other) => InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => InstanceId;

        public override string ToString() => $"카드 {CardId} (#{InstanceId})";
    }
}
