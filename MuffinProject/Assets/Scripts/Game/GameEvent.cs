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

    /// <summary>버림 더미 맨 위 카드를 손패로 가져왔다 (109 아 맞다! — 05-deck.md 7절). 버림 더미는 공개라 카드도 전원에게. TopCardId = 가져간 뒤 맨 위 (비면 0).</summary>
    public sealed class DiscardTaken : GameEvent
    {
        public int Actor { get; }
        public int CardInstanceId { get; }
        public int CardId { get; }
        public int HandCount { get; }
        public int DiscardCount { get; }
        public int TopCardId { get; }

        public DiscardTaken(int actor, int cardInstanceId, int cardId, int handCount, int discardCount, int topCardId)
        {
            Actor = actor;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            HandCount = handCount;
            DiscardCount = discardCount;
            TopCardId = topCardId;
        }
    }

    /// <summary>행동 카드 금지 상태 (107 짖기, 11-card-list 107). 네트워크로는 byte 로 간다 — 순서를 바꾸지 않는다.</summary>
    public enum ActionBlock : byte
    {
        None,     // 없음
        NextTurn, // 다음 실제 턴에 금지 — 걸렸다
        ThisTurn  // 이번 턴 금지 — 그 턴이 끝나면 None
    }

    /// <summary>행동 카드 금지 상태가 바뀌었다 (107 짖기). 공개 — 전원.</summary>
    public sealed class ActionBlockChanged : GameEvent
    {
        public int Actor { get; }
        public ActionBlock State { get; }

        public ActionBlockChanged(int actor, ActionBlock state)
        {
            Actor = actor;
            State = state;
        }
    }

    /// <summary>「기다려」 를 건 사람들이 바뀌었다 (123 기다려!, 07 4.3). 비어 있지 않은 동안 아무도 찹츄로 이기지 않는다. 공개 — 전원.</summary>
    public sealed class WaitChanged : GameEvent
    {
        public int[] Casters { get; }

        public WaitChanged(int[] casters)
        {
            Casters = casters;
        }
    }

    /// <summary>찹츄 상태로 자기 턴이 왔지만 「기다려」 라 판정을 건너뛰었다 (123). 찹츄는 그대로 — 다음 자기 턴에 다시 판정한다.</summary>
    public sealed class ChapchuWinHeld : GameEvent
    {
        public int Actor { get; }

        public ChapchuWinHeld(int actor)
        {
            Actor = actor;
        }
    }

    /// <summary>턴 진행 방향이 바뀌었다 (145 뒤로 돌아 — 03-turn.md 6절 #4). Reversed = 역방향. ExtraTurn = 생존자 2명이라 다음 턴이 다시 이 사람 (없으면 -1).</summary>
    public sealed class TurnDirectionChanged : GameEvent
    {
        public bool Reversed { get; }
        public int ExtraTurn { get; }

        public TurnDirectionChanged(bool reversed, int extraTurn)
        {
            Reversed = reversed;
            ExtraTurn = extraTurn;
        }
    }

    /// <summary>
    /// 대소동(112)으로 게임을 새로 시작한다 — 이 뒤로 HP · 손패 · 첫 턴이 시작 때처럼 온다. 화면은 판 상태(손패 · 함정 · 버림 더미 · 찹츄 · 사망 · 방향 · 판 효과)를 처음으로 되돌린다.
    /// User = 대소동을 쓴 사람. Count = 이 판에서 대소동을 쓴 횟수 n. Order = 새 턴 순서 (나간 사람 제외, 죽은 사람 포함). 통계 · 기록 서랍은 이어진다 (11-card-list 112).
    /// </summary>
    public sealed class GameRestarted : GameEvent
    {
        public int User { get; }
        public int Count { get; }
        public int[] Order { get; }

        public GameRestarted(int user, int count, int[] order)
        {
            User = user;
            Count = count;
            Order = order;
        }
    }

    /// <summary>
    /// 피폭 상태가 바뀌었다 (112 대소동). N = 대소동 횟수, Turns = 새로 시작한 뒤 피폭이 들어간 턴 수 k. Turns 가 1 이상이면 방금 모두가 k^N 피해를 입었다.
    /// 다음 턴 끝의 피해는 (k+1)^N — 판 효과 줄 「피폭 · 턴이 끝날 때마다 모두 −N」. N 이 0 이면 피폭 없음. 공개 — 전원.
    /// </summary>
    public sealed class FalloutChanged : GameEvent
    {
        public int N { get; }
        public int Turns { get; }

        public FalloutChanged(int n, int turns)
        {
            N = n;
            Turns = turns;
        }
    }
    /// <summary>
    /// 개판 오분전 상태가 바뀌었다 (121). Caster = 건 사람 (-1 이면 112 대소동으로 지워짐), Left = 남은 바퀴 (0 이면 끝 — 판정이 따르면 GaepanJudged),
    /// Again = 개판 중에 다시 써서 처음부터. 판 효과 줄 「개판 오분전 · N바퀴 남음」. 공개 — 전원.
    /// </summary>
    public sealed class GaepanChanged : GameEvent
    {
        public int Caster { get; }
        public int Left { get; }
        public bool Again { get; }

        public GaepanChanged(int caster, int left, bool again)
        {
            Caster = caster;
            Left = left;
            Again = again;
        }
    }

    /// <summary>
    /// 개판 오분전 판정 (121). ByHp = 기준 HP 꼴찌 (false 면 손패 꼴찌), Value = 그 꼴찌 값, Tied = 같은 값의 사람들 (2명 이상이면 그중 무작위).
    /// Actors · Values = 판정 때 살아 있던 사람과 그 기준 값 (같은 순서) — 각자 머리 위 수치 표. 공개 GameServer.GaepanJudgeSeconds 뒤 Loser 의 HP 가 0 이 된다 (HpChanged → 사망 대기). 공개 — 전원.
    /// </summary>
    public sealed class GaepanJudged : GameEvent
    {
        public int Loser { get; }
        public bool ByHp { get; }
        public int Value { get; }
        public int[] Tied { get; }
        public int[] Actors { get; }
        public int[] Values { get; }

        public GaepanJudged(int loser, bool byHp, int value, int[] tied, int[] actors, int[] values)
        {
            Loser = loser;
            ByHp = byHp;
            Value = value;
            Tied = tied;
            Actors = actors;
            Values = values;
        }
    }
    /// <summary>
    /// 대상이 고르기 시작 (119 목표 조준). Actor = 고르는 사람, User = 카드를 쓴 사람, HandCount = 고르는 사람의 손패 장수 (「손패 전부 버리기 (n장)」),
    /// Damage = 피해 쪽 수치, StartsAt · Deadline = 서버 시각. 그동안 게임 · 턴 시간이 멈춘다. 공개 — 전원 (고르는 사람은 팝업, 나머지는 「고르는 중」 타이머).
    /// </summary>
    public sealed class ChoiceStarted : GameEvent
    {
        public int Actor { get; }
        public int User { get; }
        public int CardId { get; }
        public int HandCount { get; }
        public int Damage { get; }
        public double StartsAt { get; }
        public double Deadline { get; }

        public ChoiceStarted(int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline)
        {
            Actor = actor;
            User = user;
            CardId = cardId;
            HandCount = handCount;
            Damage = damage;
            StartsAt = startsAt;
            Deadline = deadline;
        }
    }

    /// <summary>
    /// 고르기가 끝났다 (119). Option 0 손패 전부 버리기 · 1 피해 받기 · -1 고르는 사람이 나가 불발. TimedOut = 시간 초과로 방장이 무작위. 결과(버림 · 피해)는 뒤따르는 사건들. 공개 — 전원.
    /// </summary>
    public sealed class ChoiceMade : GameEvent
    {
        public int Actor { get; }
        public int Option { get; }
        public bool TimedOut { get; }

        public ChoiceMade(int actor, int option, bool timedOut)
        {
            Actor = actor;
            Option = option;
            TimedOut = timedOut;
        }
    }
}
