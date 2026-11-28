using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Trap.cs — 함정 설치 · 자동 발동 (04-card.md 6절 · 9.2)
    public partial class GameServer
    {
        // 확정 (04-card.md 2절).
        internal const int TrapSlotCount = 3;

        /// <summary>함정 설치 (04-card.md 6절). 내 턴 · 메인 행동 전 · 빈 슬롯이 있으면 몇 장이든. 메인 행동이 아니다. 종류는 소유자만 안다.</summary>
        public IReadOnlyList<GameEvent> SetTrap(int requester, int cardInstanceId)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            if (requester != CurrentTurnActor)
                return Reject(requester, RejectCode.NotYourTurn);

            if (!IsInGame(requester) || _players[requester].Life != LifeState.Alive)
                return Reject(requester, RejectCode.NotAlive); // 사망 대기 중에도 설치는 못 한다 (06 3절)

            if (_mainActionUsed)
                return Reject(requester, RejectCode.MainActionUsed);

            // 처리 중(체인 · 함정 처리 · 사망 대기 · 고르기 · 개판 판정 · 후처리)에는 설치하지 않는다 (04-card.md 6절 조건 5, 2026-11-15)
            if (IsPaused(_clock()))
                return Reject(requester, RejectCode.Resolving);

            if (IsTurnTimeUp(_clock()))
                return Reject(requester, RejectCode.NotYourTurn);

            PlayerState player = _players[requester];
            int index = player.Hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotInHand);

            Card card = player.Hand[index];
            if (card.Type != CardType.Trap)
                return Reject(requester, RejectCode.NotTrapCard);

            if (player.Traps.Count >= TrapSlotCount)
                return Reject(requester, RejectCode.TrapSlotFull);

            player.Hand.RemoveAt(index);
            player.Traps.Add(card);
            _events.Add(new TrapSet(requester, card.InstanceId, card.CardId, player.Hand.Count, player.Traps.Count));
            OnHandChanged(requester);
            return TakeEvents();
        }

        // 함정의 읽는 시간 — 공개된 때부터 이 초가 지나야 효과 연출로 넘어간다. 체인의 읽는 시간(MinReadSeconds)과 같은 값이고, 화면(ChainCardsPresenter)도 이 값만큼 기다린다 (04 6절, PM 2026-11-28)
        public const double TrapReadSeconds = MinReadSeconds;

        // 함정 줄의 최대 단 — 첫 줄 + 연쇄 3번. 4단째 함정이 만든 사건으로는 새 줄을 열지 않는다 (04 6절 「함정 연쇄」, 사용자 2026-11-27)
        private const int MaxTrapLineDepth = 4;

        // 3007 두고 보자가 잡은 행동 카드 — 함정 줄이 다 끝난 뒤 잡은 사람 손패로 (04 9.2 · 11-card-list 1-4). 카드 InstanceId → 잡은 사람, 잡은 순서대로
        private readonly List<(int CardInstanceId, int Owner)> _claimedCards = new List<(int, int)>();

        // 처리(체인 · 턴 뽑기 · 턴 시작 · 피폭 · 개판 판정)가 끝난 뒤 — 사망 정리 → 찹츄 선언 → 함정 줄 (04 9.2, 사용자 2026-11-27 프로토타입 v3.4 기준).
        // at = 앞 연출이 끝나는 시각. 마지막 연출(사망 대기 능력 · 함정)이 끝나는 시각을 돌려준다 (없으면 at 그대로).
        private double SettleAfter(double at)
        {
            RecordHandReachedTen(_handBefore);
            List<AfterRecord> records = TakeAfterRecords();
            at = SettleDeaths(at);
            SettleChapchu();
            if (!_gameOver && !_restartPending)
                at = FireTraps(records, at, 1);
            DeliverClaimedCards();
            return at;
        }

        // 함정 줄 한 단 (04 6절 · 9.2). records = 이 줄이 보는 사건 — 첫 줄은 그 처리의 사건, 연쇄 줄은 방금 처리한 함정이 만든 사건만.
        // 줄은 지금 정한다: 현재 턴 플레이어부터 턴 방향, 한 사람 안에서는 슬롯 순서. 한 사람의 같은 이름 함정은 한 줄에 한 장 (2026-11-27).
        // 같은 사건에 맞는 함정은 사람이 달라도 · 한 사람이 여러 장이어도 모두 오른다 — 함정 한 장은 한 번만 (2026-11-18).
        // 한 장씩: 직전에 조건 다시 보기(풀렸으면 슬롯에 남는다) → 공개 → 효과 → 버림 더미 → 사망 정리 → 그 함정이 만든 사건으로 다음 단 → 줄의 다음 함정.
        private double FireTraps(List<AfterRecord> records, double at, int depth)
        {
            if (records.Count == 0)
                return at;

            var line = new List<(int Owner, Card Trap, AfterRecord After)>();
            var taken = new List<AfterRecord>(); // 앞 함정이 이미 쓴 사건 — 맞는 사건이 여럿이면 함정마다 다른 사건부터 쓴다 (FindAfterRecord)
            foreach (int owner in TurnOrderFromCurrent())
            {
                if (!IsInGame(owner) || _players[owner].Life != LifeState.Alive)
                    continue;

                var names = new HashSet<int>(); // 이 사람이 이 줄에 올린 카드 종류
                foreach (Card trap in _players[owner].Traps)
                {
                    if (!IsAfterTrigger(trap.Trigger) || names.Contains(trap.CardId))
                        continue;

                    AfterRecord after = FindAfterRecord(records, owner, trap, taken);
                    if (after == null)
                        continue;

                    names.Add(trap.CardId);
                    taken.Add(after);
                    line.Add((owner, trap, after));
                }
            }

            foreach (var (owner, trap, after) in line)
            {
                // 앞 함정(과 그 연쇄)의 효과로 주인이 나갔거나 · 그 함정이 파괴 · 이동됐거나 · 조건이 풀렸으면 발동하지 않는다 — 슬롯에 남는다 (04 6절)
                if (!IsInGame(owner) || _players[owner].Life != LifeState.Alive || !_players[owner].Traps.Contains(trap) || !StillHolds(trap, after))
                    continue;

                Dictionary<int, int> handBefore = HandCounts();
                at = ResolveTrap(owner, trap, after, at);
                if (_restartPending || _gameOver)
                    return at;

                RecordHandReachedTen(handBefore);
                List<AfterRecord> made = TakeAfterRecords();
                at = SettleDeaths(at);
                SettleChapchu();
                if (_restartPending || _gameOver)
                    return at;

                if (depth < MaxTrapLineDepth)
                    at = FireTraps(made, at, depth + 1);
                if (_restartPending || _gameOver)
                    return at;
            }

            return at;
        }

        /// <summary>3007 두고 보자 — 그 행동 카드를 잡는다. 손패로는 함정 줄이 다 끝난 뒤 (DeliverClaimedCards).</summary>
        internal void ClaimTriggerCard(int owner, int cardInstanceId)
        {
            if (cardInstanceId >= 0 && !IsClaimed(cardInstanceId))
                _claimedCards.Add((cardInstanceId, owner));
        }

        private bool IsClaimed(int cardInstanceId) => _claimedCards.Any(c => c.CardInstanceId == cardInstanceId);

        // 함정 줄이 다 끝났다 — 잡힌 카드를 잡은 사람 손패로. 그사이 최종 사망 · 나갔으면 버림 더미에 남는다 (11-card-list 1-4 「3007」)
        private void DeliverClaimedCards()
        {
            foreach (var (cardInstanceId, owner) in _claimedCards)
                if (!_gameOver && IsInGame(owner))
                    TakeFromDiscard(owner, cardInstanceId);
            _claimedCards.Clear();
        }

        // 사람마다 지금 손패 장수 — 함정 한 장의 「처리 전 장수」 (3004)
        private Dictionary<int, int> HandCounts() => _players.ToDictionary(p => p.Key, p => p.Value.Hand.Count);

        // 함정 한 장 — 슬롯에서 공개 → 효과 → 버림 더미. at 에 공개 연출이 시작되고, 돌려준 시각에 후처리 연출이 끝난다.
        private double ResolveTrap(int owner, Card trap, AfterRecord after, double at)
        {
            PlayerState player = _players[owner];
            int source = after != null ? TriggerTargetOf(after, trap) : -1; // 발동시킨 사람 — 대상 TriggerSource
            int[] targets = ResolveTargets(owner, trap.Target, RandomPicks(owner, trap.Target), source, fromHand: false) ?? new int[0];

            player.Traps.Remove(trap);
            AddStat(owner, StatKind.Traps);
            _events.Add(new TrapActivated(owner, trap.InstanceId, trap.CardId, targets, player.Traps.Count));

            var entry = new ChainEntry(trap, owner, targets) { TriggerAmount = after?.Amount ?? 0, TriggerCardInstanceId = after?.CardInstanceId ?? -1 };
            foreach (CardEffect effect in trap.Effects)
                effect.Prepare(this, entry);

            _events.Add(new CardResolved(owner, trap.InstanceId, trap.CardId, targets, _discardPile.Count));
            foreach (CardEffect effect in trap.Effects)
            {
                effect.Apply(this, entry);
                SettleChapchu(); // 효과 하나가 끝난 뒤의 손패로 자동 선언 (07 4.1)
                if (_restartPending)
                    return at; // 대소동 — 남은 처리는 버린다
            }

            _discardPile.Add(trap);
            _events.Add(new ChainDiscarded(new[] { trap.InstanceId }, new[] { trap.CardId }, _discardPile.Count));
            return at + System.Math.Max(trap.RevealSeconds, TrapReadSeconds) + trap.ResolveSeconds;
        }

        // 함정은 고르는 입력이 없다 — 고르는 대상이면 무작위 (04 6절). 고르지 않는 타입이면 null (ResolveTargets 가 정한다).
        private int[] RandomPicks(int owner, TargetType type)
        {
            int count = PickCount(type);
            if (count == 0)
                return null;

            List<int> pool = _turnOrder.Where(a => IsInGame(a) && (type == TargetType.OneAny || a != owner)).ToList();
            Shuffle(pool);
            return pool.Take(count).ToArray();
        }
    }
}
