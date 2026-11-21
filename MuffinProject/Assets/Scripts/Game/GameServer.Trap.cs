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

        // 함정을 설치한 순서 — 여럿이 함께 발동할 때 방 전체에서 먼저 설치한 것부터 (04 6절 「여러 함정 동시 발동」, 2026-11-15)
        private readonly Dictionary<int, int> _trapSetOrder = new Dictionary<int, int>(); // 카드 InstanceId → 설치 번호
        private int _nextTrapSetOrder;

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
            _trapSetOrder[card.InstanceId] = _nextTrapSetOrder++;
            _events.Add(new TrapSet(requester, card.InstanceId, card.CardId, player.Hand.Count, player.Traps.Count));
            OnHandChanged(requester);
            return TakeEvents();
        }

        // 행동(체인) · 턴 끝 피폭 · 턴 스킵이 끝난 뒤 — 그사이 생긴 사건에 맞는 함정을 모두 자동 발동한다 (04 6절 · 9.2, 2026-11-15 PM).
        // 같은 사건에 맞는 함정이 여럿이면 사람이 달라도 · 한 사람이 여러 장이어도 모두 발동한다 — 함정 한 장은 한 번만 (04 6절 「여러 함정 동시 발동」, 2026-11-18).
        // 발동할 함정은 지금 정하고, 설치한 순서대로 한 장씩: 공개 → 효과 → 버림 더미. 카운터 · 반응 시간은 없다.
        // 함정의 효과로 생긴 사건은 다른 함정을 부르지 않는다 — 끝나면 사건 기록을 비운다.
        // at = 앞 연출이 끝나는 시각. 마지막 함정의 연출이 끝나는 시각을 돌려준다 (발동한 것이 없으면 at 그대로).
        private double FireTraps(double at)
        {
            var fired = new List<(int Owner, Card Trap, AfterRecord After)>();
            var taken = new List<AfterRecord>(); // 앞 함정이 이미 쓴 사건 — 맞는 사건이 여럿이면 함정마다 다른 사건부터 쓴다 (FindAfterRecord)
            foreach (int owner in _turnOrder)
            {
                if (!IsInGame(owner))
                    continue;

                PlayerState player = _players[owner];
                bool dying = player.Hp <= 0; // 그 체인에서 HP 0 — 사망 대기 판정 전이어도 「본인 사망 대기」 함정만 (06 3절)
                foreach (Card trap in player.Traps)
                {
                    if (trap.Trigger == TrapTrigger.SelfDeathPending)
                    {
                        if (dying)
                            fired.Add((owner, trap, null));
                        continue;
                    }

                    if (dying || !IsAfterTrigger(trap.Trigger))
                        continue;

                    AfterRecord after = FindAfterRecord(owner, trap, taken);
                    if (after == null)
                        continue;

                    taken.Add(after);
                    fired.Add((owner, trap, after));
                }
            }
            _afterRecords.Clear();

            foreach (var (owner, trap, after) in fired.OrderBy(f => _trapSetOrder.TryGetValue(f.Trap.InstanceId, out int order) ? order : int.MaxValue))
            {
                // 앞 함정의 효과로 주인이 나갔거나 그 함정이 파괴 · 이동됐으면 발동하지 않는다 (04 6절)
                if (!IsInGame(owner) || !_players[owner].Traps.Contains(trap))
                    continue;

                at = ResolveTrap(owner, trap, after, at);
                if (_restartPending || _gameOver)
                    break;
            }

            _afterRecords.Clear(); // 함정의 효과로 생긴 사건은 쓰지 않는다
            return at;
        }

        // 함정 한 장 — 슬롯에서 공개 → 효과 → 버림 더미. at 에 공개 연출이 시작되고, 돌려준 시각에 후처리 연출이 끝난다.
        private double ResolveTrap(int owner, Card trap, AfterRecord after, double at)
        {
            PlayerState player = _players[owner];
            int source = after != null ? TriggerTargetOf(after) : -1; // 발동시킨 사람 — 대상 TriggerSource
            int[] targets = ResolveTargets(owner, trap.Target, RandomPicks(owner, trap.Target), source, fromHand: false) ?? new int[0];

            player.Traps.Remove(trap);
            _trapSetOrder.Remove(trap.InstanceId);
            AddStat(owner, StatKind.Traps);
            _events.Add(new TrapActivated(owner, trap.InstanceId, trap.CardId, targets, player.Traps.Count));

            var entry = new ChainEntry(trap, owner, targets) { TriggerAmount = after?.Amount ?? 0 };
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
            return at + trap.RevealSeconds + trap.ResolveSeconds;
        }

        // 함정은 고르는 입력이 없다 — 고르는 대상이면 무작위 (04 6절). 고르지 않는 타입이면 null (ResolveTargets 가 정한다).
        private int[] RandomPicks(int owner, TargetType type)
        {
            int count = type == TargetType.OneOther || type == TargetType.OneAny ? 1 : type == TargetType.TwoOthers ? 2 : 0;
            if (count == 0)
                return null;

            List<int> pool = _turnOrder.Where(a => IsInGame(a) && (type == TargetType.OneAny || a != owner)).ToList();
            Shuffle(pool);
            return pool.Take(count).ToArray();
        }
    }
}
