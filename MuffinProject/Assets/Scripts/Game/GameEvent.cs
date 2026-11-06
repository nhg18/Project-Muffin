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

    /// <summary>
    /// 카드를 냈다 — 행동 · 카운터 (04-card.md 5 · 7절). 손패에서 나와 체인 맨 위에 올라갔다 (연출 1 ~ 3 단계).
    /// 효과는 체인을 처리할 때 CardResolved 뒤에 온다. DiscardCount 는 지금 버림 더미 장수(이 카드는 아직 들어가지 않았다).
    /// </summary>
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

    /// <summary>반응 시간이 정해졌다 (04-card.md 8절). 카드가 체인에 오를 때마다 다시 온다. 시각은 서버 시각(초) — OpensAt 전은 연출 중이라 카운터를 받지 않는다.</summary>
    public sealed class ReactionStarted : GameEvent
    {
        public double OpensAt { get; }
        public double Deadline { get; }

        public ReactionStarted(double opensAt, double deadline)
        {
            OpensAt = opensAt;
            Deadline = deadline;
        }
    }

    /// <summary>체인의 카드 하나를 처리했다 — 효과 적용 (04-card.md 9절, 연출 4 단계). 카드는 버림 더미로 갔고, 효과는 뒤따르는 일(HpChanged 등)로 온다. Targets 는 본인 무효화를 뺀 대상.</summary>
    public sealed class CardResolved : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int[] Targets { get; }
        public int DiscardCount { get; }

        public CardResolved(int actor, int cardInstanceId, int cardId, int[] targets, int discardCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            Targets = targets;
            DiscardCount = discardCount;
        }
    }

    /// <summary>체인의 카드 하나가 무효화됐다 (04-card.md 9절). 효과 없이 버림 더미로 갔다 — 연출 4 단계 없음.</summary>
    public sealed class CardNegated : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int DiscardCount { get; }

        public CardNegated(int actor, int cardInstanceId, int cardId, int discardCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
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
