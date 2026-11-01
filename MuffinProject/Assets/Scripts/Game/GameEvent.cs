namespace Chapchu.Game
{
    /// <summary>
    /// GameServer 가 돌려주는 "일어난 일" 하나 (CODE_CONVENTION 9절). 누구에게 보낼지(전체 · 주인만)는 네트워크 계층이 정한다.
    /// 화면은 이 값으로 연출하고 숫자를 바꾼다 — 연출 순서 = 목록 순서.
    /// </summary>
    public abstract class GameEvent
    {
    }

    /// <summary>체력이 바뀌었다 (06-health.md). 바뀐 뒤의 값.</summary>
    public sealed class HpChanged : GameEvent
    {
        public int Actor { get; }
        public int Hp { get; }

        public HpChanged(int actor, int hp)
        {
            Actor = actor;
            Hp = hp;
        }
    }

    /// <summary>덱 맨 위 1장이 손패로 왔다 (05-deck.md 3 · 4절). 카드 내용(InstanceId · CardId)은 주인만 알아야 한다.</summary>
    public sealed class CardDrawn : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int HandCount { get; }
        public int DeckCount { get; }

        public CardDrawn(int actor, int cardInstanceId, int cardId, int handCount, int deckCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            HandCount = handCount;
            DeckCount = deckCount;
        }
    }

    /// <summary>버림 더미를 섞어 덱을 다시 채웠다 (05-deck.md 5절).</summary>
    public sealed class DeckRefilled : GameEvent
    {
        public int DeckCount { get; }

        public DeckRefilled(int deckCount)
        {
            DeckCount = deckCount;
        }
    }

    /// <summary>턴이 시작됐다 (03-turn.md). Deadline 은 마감 서버 시각(초) — 남은 시간은 각 클라이언트가 계산한다 (09-network.md 8절).</summary>
    public sealed class TurnStarted : GameEvent
    {
        public int Actor { get; }
        public double Deadline { get; }

        public TurnStarted(int actor, double deadline)
        {
            Actor = actor;
            Deadline = deadline;
        }
    }

    /// <summary>행동 카드를 썼다 (04-card.md 5절). 카드는 버림 더미로 갔고, 효과는 뒤따르는 일(HpChanged 등)로 온다.</summary>
    public sealed class CardPlayed : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int[] Targets { get; }
        public int HandCount { get; }
        public int DiscardCount { get; }

        public CardPlayed(int actor, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            Targets = targets;
            HandCount = handCount;
            DiscardCount = discardCount;
        }
    }

    /// <summary>요청을 거절했다 (09-network.md 4.3). 상태는 바뀌지 않았다. 요청자에게만 보낸다.</summary>
    public sealed class RequestRejected : GameEvent
    {
        public int Actor { get; }
        public int Code { get; }

        public RequestRejected(int actor, int code)
        {
            Actor = actor;
            Code = code;
        }
    }
}
