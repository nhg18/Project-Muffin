using System.Collections.Generic;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    /// <summary>
    /// 플레이어 한 명의 원본 상태 (방장 메모리에만 있다). 필드만 둔다 — 로직 · 이벤트는 GameServer 에.
    /// 모두가 함께 쓰는 값(덱 · 버림 더미 · 턴)은 여기 두지 않고 GameServer 필드로 둔다.
    /// </summary>
    public class PlayerState
    {
        // 손패 내용 — 비공개. 주인에게만 보내고, 다른 사람에게는 장수(Hand.Count)만 공개한다 (09-network.md 3절).
        public readonly List<Card> Hand = new List<Card>();

        // 체력 — 공개. 0 ~ GameServer.MaxHp (06-health.md).
        public int Hp;
    }
}
