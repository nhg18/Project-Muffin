using System;

namespace Chapchu.Game.Cards
{

    /// <summary>
    /// 게임 내 카드 1장. 같은 종류(CardId)가 여러 장이어도 InstanceId로 구분한다 (09-network.md 7절).
    /// </summary>
    [Serializable]
    public struct CardInstance : IEquatable<CardInstance>
    {
        public int InstanceId;
        public int CardId;

        public CardInstance(int instanceId, int cardId)
        {
            InstanceId = instanceId;
            CardId = cardId;
        }

        public bool Equals(CardInstance other) => InstanceId == other.InstanceId;
        public override bool Equals(object obj) => obj is CardInstance other && Equals(other);
        public override int GetHashCode() => InstanceId;
    }
}
