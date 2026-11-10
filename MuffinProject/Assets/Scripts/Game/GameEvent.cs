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

    /// <summary>
    /// 지금 열리는 반응 시간에 Actor 가 낼 수 있는 손패의 카운터들 (04-card.md 7절의 반응 조건을 방장이 판정한 결과). ReactionStarted 바로 뒤에, 낼 수 있는 것이 있는 사람마다 온다.
    /// 손패 내용이라 본인에게만 보낸다 (09-network.md 3절). 다음 ReactionStarted 가 오면 앞의 목록은 버린다.
    /// </summary>
    public sealed class CountersUsable : GameEvent
    {
        public int Actor { get; }
        public int[] CardInstanceIds { get; }

        public CountersUsable(int actor, int[] cardInstanceIds)
        {
            Actor = actor;
            CardInstanceIds = cardInstanceIds;
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

    /// <summary>덱 · 버림 더미가 모두 비어 카드 대신 피해를 받는다 (05-deck.md 5절). 체력 변화는 뒤따르는 HpChanged 로.</summary>
    public sealed class EmptyDeckDamaged : GameEvent
    {
        public int Actor { get; }
        public int Amount { get; }

        public EmptyDeckDamaged(int actor, int amount)
        {
            Actor = actor;
            Amount = amount;
        }
    }

    /// <summary>
    /// HP 0 — 사망 대기 (06-health.md 8절). 게임이 멈춘다. 대응 시간은 StartsAt(피해 연출이 끝나는 서버 시각)부터 Deadline 까지.
    /// 여럿이 함께 0 이 되면 같은 시각으로 하나씩 온다 (06 11절).
    /// </summary>
    public sealed class DeathPending : GameEvent
    {
        public int Actor { get; }
        public double StartsAt { get; }
        public double Deadline { get; }

        public DeathPending(int actor, double startsAt, double deadline)
        {
            Actor = actor;
            StartsAt = startsAt;
            Deadline = deadline;
        }
    }

    /// <summary>
    /// 최종 사망 (06-health.md 9절). 손패(와 함정)가 버림 더미로 갔다 — 손패 0. 턴 순서 · 대상 후보에서 빠진다.
    /// CardIds 는 버림 더미에 들어간 순서(손패 → 함정)의 카드 종류 — 버림 더미는 공개다 (05-deck.md 7절 · 09-network.md 3절). 없으면 빈 배열.
    /// </summary>
    public sealed class PlayerDied : GameEvent
    {
        public int Actor { get; }
        public int[] CardIds { get; }
        public int DiscardCount { get; }

        public PlayerDied(int actor, int[] cardIds, int discardCount)
        {
            Actor = actor;
            CardIds = cardIds;
            DiscardCount = discardCount;
        }
    }

    /// <summary>게임 중 방을 나갔다 (01-game-flow.md 6절) — 최종 사망과 같은 처리. 손패(와 함정)가 버림 더미로 갔다. CardIds 는 PlayerDied 와 같다.</summary>
    public sealed class PlayerLeft : GameEvent
    {
        public int Actor { get; }
        public int[] CardIds { get; }
        public int DiscardCount { get; }

        public PlayerLeft(int actor, int[] cardIds, int discardCount)
        {
            Actor = actor;
            CardIds = cardIds;
            DiscardCount = discardCount;
        }
    }

    /// <summary>
    /// 메인 행동 전에 멈췄던 턴이 이어진다 (03-turn.md 4절 — 함정 · 함정 시간 · 사망 대기 뒤). 새 마감 서버 시각.
    /// 턴 시작이 아니다 — 찹츄 판정 · 연출은 TurnStarted 에만.
    /// </summary>
    public sealed class TurnResumed : GameEvent
    {
        public int Actor { get; }
        public double Deadline { get; }

        public TurnResumed(int actor, double deadline)
        {
            Actor = actor;
            Deadline = deadline;
        }
    }

    /// <summary>함정을 설치했다 (04-card.md 6절). 종류(InstanceId · CardId)는 소유자만 알아야 한다 — 다른 사람에겐 장수만.</summary>
    public sealed class TrapSet : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int HandCount { get; }
        public int TrapCount { get; }

        public TrapSet(int actor, int cardInstanceId, int cardId, int handCount, int trapCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            HandCount = handCount;
            TrapCount = trapCount;
        }
    }

    /// <summary>함정을 발동했다 — 공개되고 체인 맨 위에 올라갔다 (04-card.md 6절, 연출 1 단계는 함정 슬롯에서). 뒤에 ReactionStarted.</summary>
    public sealed class TrapActivated : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int[] Targets { get; }
        public int TrapCount { get; }

        public TrapActivated(int actor, int cardInstanceId, int cardId, int[] targets, int trapCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            Targets = targets;
            TrapCount = trapCount;
        }
    }

    /// <summary>「피해 받은 후」 함정을 낼 수 있는 함정 시간 (04-card.md 6절). 조건이 맞는 함정을 가진 사람이 있을 때만 온다.</summary>
    public sealed class TrapWindowStarted : GameEvent
    {
        public double OpensAt { get; }
        public double Deadline { get; }

        public TrapWindowStarted(double opensAt, double deadline)
        {
            OpensAt = opensAt;
            Deadline = deadline;
        }
    }

    /// <summary>찹츄 상태가 바뀌었다 (07-win-condition.md 4절) — 선언 또는 손패가 10장이 아니게 되어 해제.</summary>
    public sealed class ChapchuChanged : GameEvent
    {
        public int Actor { get; }
        public bool IsChapchu { get; }

        public ChapchuChanged(int actor, bool isChapchu)
        {
            Actor = actor;
            IsChapchu = isChapchu;
        }
    }

    /// <summary>게임이 끝나는 방식 (07-win-condition.md 2절 · 3절). 네트워크로는 byte 로 간다 — 순서를 바꾸지 않는다.</summary>
    public enum GameEndReason : byte
    {
        Elimination, // 처치 승리 — 마지막 1인
        Chapchu,     // 찹츄 승리
        Draw         // 무승부 — 전원 최종 사망
    }

    /// <summary>게임이 끝났다 (07-win-condition.md). 무승부면 Winner = -1. 이 뒤로는 모든 요청을 거절한다.</summary>
    public sealed class GameEnded : GameEvent
    {
        public int Winner { get; }
        public GameEndReason Reason { get; }

        public GameEnded(int winner, GameEndReason reason)
        {
            Winner = winner;
            Reason = reason;
        }
    }

    /// <summary>손패 카드가 한 사람에게서 다른 사람에게 갔다 (훔치기 · 교환, 09-network.md 3절). 어떤 카드인지는 두 사람만, 장수는 전원.</summary>
    public sealed class HandCardsMoved : GameEvent
    {
        public int From { get; }
        public int To { get; }
        public int[] CardInstanceIds { get; }
        public int[] CardIds { get; }
        public int FromHandCount { get; }
        public int ToHandCount { get; }

        public HandCardsMoved(int from, int to, int[] cardInstanceIds, int[] cardIds, int fromHandCount, int toHandCount)
        {
            From = from;
            To = to;
            CardInstanceIds = cardInstanceIds;
            CardIds = cardIds;
            FromHandCount = fromHandCount;
            ToHandCount = toHandCount;
        }
    }

    /// <summary>손패에서 카드가 버림 더미로 갔다 (강제 버림 · 생존 대가, 05-deck.md 7절). 버림 더미는 공개라 카드도 공개.</summary>
    public sealed class CardsDiscarded : GameEvent
    {
        public int Actor { get; }
        public int[] CardInstanceIds { get; }
        public int[] CardIds { get; }
        public int HandCount { get; }
        public int DiscardCount { get; }

        public CardsDiscarded(int actor, int[] cardInstanceIds, int[] cardIds, int handCount, int discardCount)
        {
            Actor = actor;
            CardInstanceIds = cardInstanceIds;
            CardIds = cardIds;
            HandCount = handCount;
            DiscardCount = discardCount;
        }
    }

    /// <summary>설치 함정이 파괴돼 버림 더미로 갔다 — 공개 (A03 · T17).</summary>
    public sealed class TrapDestroyed : GameEvent
    {
        public int Owner { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int TrapCount { get; }
        public int DiscardCount { get; }

        public TrapDestroyed(int owner, int cardInstanceId, int cardId, int trapCount, int discardCount)
        {
            Owner = owner;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            TrapCount = trapCount;
            DiscardCount = discardCount;
        }
    }

    /// <summary>설치 함정이 주인 손패로 돌아갔다 (A16 · C12). 종류는 주인만.</summary>
    public sealed class TrapReturned : GameEvent
    {
        public int Owner { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int TrapCount { get; }
        public int HandCount { get; }

        public TrapReturned(int owner, int cardInstanceId, int cardId, int trapCount, int handCount)
        {
            Owner = owner;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            TrapCount = trapCount;
            HandCount = handCount;
        }
    }

    /// <summary>설치 함정을 다른 사람이 빼앗아 자기 칸에 놓았다 (A17). 종류는 두 사람만.</summary>
    public sealed class TrapMoved : GameEvent
    {
        public int From { get; }
        public int To { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int FromTrapCount { get; }
        public int ToTrapCount { get; }

        public TrapMoved(int from, int to, int cardInstanceId, int cardId, int fromTrapCount, int toTrapCount)
        {
            From = from;
            To = to;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            FromTrapCount = fromTrapCount;
            ToTrapCount = toTrapCount;
        }
    }

    /// <summary>다음 턴 스킵이 걸렸다 (A01 · T05, 03-turn.md 7절).</summary>
    public sealed class TurnSkipMarked : GameEvent
    {
        public int Actor { get; }

        public TurnSkipMarked(int actor)
        {
            Actor = actor;
        }
    }

    /// <summary>턴이 스킵됐다 — 그 사람의 턴은 시작하지 않고 다음 사람으로.</summary>
    public sealed class TurnSkipped : GameEvent
    {
        public int Actor { get; }

        public TurnSkipped(int actor)
        {
            Actor = actor;
        }
    }

    /// <summary>체인의 카드가 효과 없이 시전자 손패로 돌아갔다 (C01). 체인에 공개된 카드라 전원.</summary>
    public sealed class CardReturnedToHand : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int HandCount { get; }

        public CardReturnedToHand(int actor, int cardInstanceId, int cardId, int handCount)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            HandCount = handCount;
        }
    }

    /// <summary>사망 대기에서 살아났다 (T17, 06-health.md 8절). 체력은 뒤따르거나 앞선 HpChanged.</summary>
    public sealed class DeathAvoided : GameEvent
    {
        public int Actor { get; }
        public int Hp { get; }

        public DeathAvoided(int actor, int hp)
        {
            Actor = actor;
            Hp = hp;
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
