using System.Collections.Generic;

namespace Chapchu.Game
{
    /// <summary>생존 상태 (06-health.md 3절). 네트워크로는 byte 로 간다 — 순서를 바꾸지 않는다.</summary>
    public enum LifeState : byte
    {
        Alive,
        DeathPending, // HP 0, 사망 대응 시간 — 게임이 멈춘다
        Dead          // 최종 사망 — 턴 순서 · 대상 후보에서 빠진다
    }

    /// <summary>
    /// 플레이어 한 명의 원본 상태 (방장 메모리에만 있다, 02-player.md 2절). 필드만 둔다 — 규칙은 GameServer 에.
    /// </summary>
    public class PlayerState
    {
        // 손패 내용 — 비공개. 주인에게만 보내고, 다른 사람에게는 장수만 공개한다 (09-network.md 3절).
        public readonly List<Card> Hand = new List<Card>();

        // 설치한 함정 — 내용은 소유자만, 개수는 공개. 최대 3 (04-card.md 6절).
        public readonly List<Card> Traps = new List<Card>();

        // 체력 — 공개. 0 ~ GameServer.MaxHp (06-health.md).
        public int Hp;

        // 방을 나갔다 — 턴 순서 · 대상 후보에서 빠진다 (01-game-flow.md 6절). 턴 순서 목록에는 남겨 다음 사람 찾기의 기준으로 쓴다.
        // 최종 사망과 결과는 같지만 따로 기록한다 (06-health.md 12절).
        public bool HasLeft;

        // 생존 상태 — 공개 (06-health.md 3절).
        public LifeState Life;

        // 빈 덱에서 뽑은 횟수 — 빈 덱 피해량 (05-deck.md 5절).
        public int EmptyDraws;

        // 찹츄 상태 — 공개. 손패 10장에서 파생되지 않고 따로 부여 · 제거된다. 10장이 아니게 되면 즉시 해제 (07-win-condition.md 7절).
        public bool IsChapchu;
    }
}
