using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Chain.cs — 처리 체인 · 반응 시간 · 카운터 (04-card.md 7 ~ 9절)
    public partial class GameServer
    {
        // 확정 (04-card.md 2절).
        private const double ReactionTimeLimit = 5;
        // 고민 연장 — 낼 수 있는 카운터를 집으면 마감을 「그 순간 + 이 초」 까지 늦춘다. 한 사람당 반응 시간 한 번에 1회 (04-card.md 8절).
        private const double ReactionHoldSeconds = 3;

        private readonly List<ChainEntry> _chain = new List<ChainEntry>(); // [0] = 맨 아래(메인 행동 카드), 끝 = 맨 위
        private double _reactionOpensAt;  // 이 전은 연출 중 — 카운터를 받지 않는다 (04 8절)
        private double _reactionDeadline; // 방장만 안다 — 화면에 보내지 않는다 (고민 연장을 드러내지 않게)

        // 이번 반응 시간에 물어야 할 사람 → 낼 수 있는 카운터. 스킵하면 빠지고, 비면 기다리지 않고 체인을 처리한다 (04 8절).
        private readonly Dictionary<int, int[]> _reactionAsked = new Dictionary<int, int[]>();
        private readonly HashSet<int> _reactionSkipped = new HashSet<int>(); // 스킵했다 — 이번 반응 시간에는 카운터를 받지 않는다 (PM 2026-11-19)
        private readonly HashSet<int> _reactionHeld = new HashSet<int>();    // 고민 연장을 이미 썼다

        // 처리 중에 멈춘 카드들 — [0] 부터 이어서 처리한다. 대상이 고르는 동안(1019)만 남아 있다 (ContinueResolve).
        private readonly List<ChainEntry> _resolving = new List<ChainEntry>();
        private bool IsResolvePaused => _resolving.Count > 0;

        // 처리 중인 체인의 카드 — 체인에 오른 순서(맨 아래부터). 체인을 끝까지 처리한 뒤 이 순서로 버림 더미에 쌓는다 (04 9.1, 2026-11-15)
        private readonly List<ChainEntry> _chainOrder = new List<ChainEntry>();

        /// <summary>
        /// 카운터 사용 (04-card.md 7절). 턴과 관계없이 반응 시간 안이면 받는다. 체인 맨 위에 올리고 반응 시간을 새로 시작한다.
        /// 동시 요청은 도착 순서로 처리된다 — 먼저 온 것이 체인에 오르면 늦은 것은 연출 중이라 거절된다 (04 7절 「동시 사용」).
        /// </summary>
        public IReadOnlyList<GameEvent> PlayCounter(int requester, int cardInstanceId, int[] requestedTargets)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            if (!InGame(requester)) // 참가자가 아닌 요청(시작 전 · 방장이 바뀐 뒤)도 — 원본을 꺼내기 전에
                return Reject(requester, RejectCode.NotAlive);

            double now = _clock();
            if (_chain.Count == 0 || now < _reactionOpensAt || now >= _reactionDeadline)
                return Reject(requester, RejectCode.NotReactionTime);

            if (_reactionSkipped.Contains(requester))
                return Reject(requester, RejectCode.NotReactionTime); // 스킵한 사람에게는 이번 반응 시간이 끝났다

            List<Card> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotInHand);

            Card card = hand[index];
            if (card.Type != CardType.Counter)
                return Reject(requester, RejectCode.NotCounterCard);

            ChainEntry top = _chain[_chain.Count - 1];
            if (!CanPlayCounter(requester, card, top))
                return Reject(requester, RejectCode.CannotReact);

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets, top.User);
            if (targets == null)
                return Reject(requester, RejectCode.InvalidTarget);

            hand.RemoveAt(index);
            AddToChain(card, requester, targets);
            OnHandChanged(requester);
            return TakeEvents();
        }

        /// <summary>
        /// 반응 시간 스킵 — 물어 온 사람이 「안 낸다」 고 답한다. 물을 사람이 모두 스킵하면 남은 시간을 기다리지 않는다 (04-card.md 8절).
        /// 결과를 알리지 않고, 맞지 않는 요청은 조용히 버린다 (09-network.md 7절).
        /// </summary>
        public IReadOnlyList<GameEvent> SkipReaction(int requester)
        {
            double now = _clock();
            if (!_gameOver && IsReactionOpen(now) && _reactionAsked.Remove(requester))
            {
                _reactionSkipped.Add(requester);
                if (_reactionAsked.Count == 0)
                    _reactionDeadline = now; // 다음 Tick 에 체인을 처리한다
                Revision++; // 알림은 없지만 상태가 바뀌었다 (GameServer.Snapshot.cs)
            }

            return TakeEvents();
        }

        /// <summary>
        /// 고민 연장 — 낼 수 있는 카운터를 집었다. 마감이 「지금 + 3초」 보다 이르면 그 시각으로 늦춘다. 한 사람당 반응 시간 한 번에 1회.
        /// 아무에게도 알리지 않고, 맞지 않는 요청은 조용히 버린다 (04-card.md 8절 「고민 연장」).
        /// </summary>
        public IReadOnlyList<GameEvent> HoldReaction(int requester, int cardInstanceId)
        {
            double now = _clock();
            if (!_gameOver && IsReactionOpen(now) && _reactionAsked.TryGetValue(requester, out int[] usable)
                && usable.Contains(cardInstanceId) && _reactionHeld.Add(requester))
            {
                _reactionDeadline = System.Math.Max(_reactionDeadline, now + ReactionHoldSeconds);
                Revision++;
            }

            return TakeEvents();
        }

        // 물어야 할 사람이 게임에서 빠졌다 — 남은 사람이 없으면 기다리지 않는다.
        private void DropFromReaction(int actor)
        {
            if (_reactionAsked.Remove(actor) && _reactionAsked.Count == 0 && _chain.Count > 0)
                _reactionDeadline = System.Math.Min(_reactionDeadline, System.Math.Max(_clock(), _reactionOpensAt));
        }

        // 손패에서 나온 카드(행동 · 카운터)를 체인 맨 위에 올리고, 그 카드의 연출(1 ~ 3 단계) 뒤에 반응 시간을 연다 (04 8절). 함정은 체인에 오르지 않는다 (FireTraps).
        private ChainEntry AddToChain(Card card, int user, int[] targets)
        {
            var entry = new ChainEntry(card, user, targets) { Below = _chain.Count > 0 ? _chain[_chain.Count - 1] : null };
            foreach (CardEffect effect in card.Effects)
                effect.Prepare(this, entry); // 낼 때 정하는 값 (1016 뽑을 장수)
            _chain.Add(entry);
            AddStat(user, card.Type == CardType.Counter ? StatKind.Counters : StatKind.Actions); // 승인된 사용 — 무효화돼도 센다 (07 11-4)
            _events.Add(new CardPlayed(user, card.InstanceId, card.CardId, targets, _players[user].Hand.Count, _discardPile.Count));

            _reactionOpensAt = _clock() + card.RevealSeconds;
            _events.Add(new ReactionStarted(_reactionOpensAt));
            AddCountersUsable(entry);
            // 아무도 낼 수 없으면 반응 시간을 열자마자(0초) 처리한다 (04 8절)
            _reactionDeadline = _reactionAsked.Count > 0 ? _reactionOpensAt + ReactionTimeLimit : _reactionOpensAt;
            return entry;
        }

        // 반응 시간이 정해질 때마다, 지금 맨 위 카드에 낼 수 있는 카운터를 손패에 가진 사람에게 그 목록을 알린다 — 화면의 초록 빛용 (10-ui.md 5절, 09-network.md 3절: 본인에게만).
        // PlayCounter 와 같은 판정(CanPlayCounter)을 쓴다 (대상 확정은 뺀다 — 낼 때 판정). 낼 수 있는 것이 없는 사람에겐 보내지 않는다.
        // 낼 수 있는 사람이 이번 반응 시간에 「물어야 할 사람」 이다 — 나머지는 스킵한 것으로 본다 (04 8절).
        private void AddCountersUsable(ChainEntry top)
        {
            _reactionAsked.Clear();
            _reactionSkipped.Clear();
            _reactionHeld.Clear();
            foreach (KeyValuePair<int, PlayerState> pair in _players)
            {
                int actor = pair.Key;
                if (!IsInGame(actor))
                    continue;

                int[] usable = pair.Value.Hand
                    .Where(c => CanPlayCounter(actor, c, top))
                    .Select(c => c.InstanceId)
                    .ToArray();
                if (usable.Length > 0)
                {
                    _reactionAsked[actor] = usable;
                    _events.Add(new CountersUsable(actor, usable));
                }
            }
        }

        // actor 가 손패의 card 를 카운터로 top 에 낼 수 있는가 (대상 확정 전) — PlayCounter 와 AddCountersUsable 이 같이 쓴다.
        // 내가 낸 카드(행동 · 카운터)에는 낼 수 없다 (04 7절 「사용 조건」 5). 사망 대기 중엔 사망 관련 카운터만 (06 3 · 8절 — C11).
        private bool CanPlayCounter(int actor, Card card, ChainEntry top) =>
            card.Type == CardType.Counter
            && top.User != actor
            && (_players[actor].Life != LifeState.DeathPending || card.Condition == ReactCondition.WouldKillMe)
            && CanReactTo(card, actor, top);

        /// <summary>
        /// 카운터 card 로 맨 위 카드 top 에 반응할 수 있는가 — 반응 대상 종류 + 반응 조건 (04-card.md 7절).
        /// 「받는다」 = 그 카드의 효과가 나에게 들어간다 (본인 무효 · 나간 사람 제외).
        /// </summary>
        internal bool CanReactTo(Card card, int me, ChainEntry top)
        {
            if (!card.ReactsTo.Contains(top.Card.Type))
                return false;

            switch (card.Condition)
            {
                case ReactCondition.Any:            return true;
                case ReactCondition.ByOther:        return top.User != me;
                case ReactCondition.TargetsMe:      return top.User != me && top.Targets.Contains(me);
                case ReactCondition.DamagesMe:      return AffectedBy(top, EffectTag.Damage).Contains(me);
                case ReactCondition.WouldKillMe:
                    int damage = top.Card.Effects.Sum(e => e.PredictDamage(this, top, me));
                    return damage > 0 && damage >= _players[me].Hp;
                case ReactCondition.HealsOther:     return AffectedBy(top, EffectTag.Heal).Any(a => a != me);
                case ReactCondition.TakesMyHand:    return AffectedBy(top, EffectTag.TakeHand).Contains(me);
                case ReactCondition.DestroysMyTrap: return top.User != me && AffectedBy(top, EffectTag.DestroyTrap).Contains(me);
                case ReactCondition.SkipsMe:        return AffectedBy(top, EffectTag.SkipTurn).Contains(me);
                case ReactCondition.GivesMeChapchu: return top.User != me && AffectedBy(top, EffectTag.GiveChapchu).Contains(me);
                default:                            return false;
            }
        }

        // Tick 이 반응 마감 뒤에 부른다. 위에서부터 한 번에 처리하고 다시 기다리지 않는다 (04 8 · 9절).
        // 무효화 · 불발이어도 카드는 버림 더미로 간다 (04 4절) — 체인이 끝난 뒤 쓴 순서대로 (9.1). 맨 아래 카드의 후처리 연출 뒤에
        // 함정 (9.2) → 사망 대기 → 메인 행동의 체인이면 다음 턴, 메인 행동 전(사망 대기 중 카운터 등)이면 턴을 이어 간다.
        private void ResolveChain()
        {
            double animationEnd = _clock() + _chain[0].Card.ResolveSeconds;

            _chainOrder.Clear();
            _chainOrder.AddRange(_chain);
            for (int i = 0; i < _chain.Count; i++)
                _resolving.Insert(0, _chain[i]); // 위에서부터
            _chain.Clear();
            ContinueResolve(animationEnd);
        }

        // 처리할 카드가 남아 있는 동안 위에서부터 효과를 적용한다. 효과 사이에서는 멈추지 않는다 — 피해 뒤에 효과가 더 있어도 이어서 (04 9.3, 2026-11-15).
        // 대상이 고르는 효과(1019)에서만 멈추고, 고른 뒤 Advance 가 다시 부른다.
        // settleAt = 이번에 적용하는 결과의 후처리 연출(3 · 4 단계)이 끝나는 시각 — 처음엔 맨 아래 카드의, 이어 갈 때는 고른 결과의.
        // 함정 · 사망 대기는 이 시각 뒤에, 다음 턴(메인 행동 전이면 턴 재개)도 그 뒤로 미룬다 (04 8 · 9.2절 · 06 8절).
        private void ContinueResolve(double settleAt)
        {
            DelayTurnUntil(settleAt);

            while (_resolving.Count > 0)
            {
                ChainEntry entry = _resolving[0];
                if (entry.NextEffect == 0)
                {
                    if (entry.Negated)
                    {
                        _resolving.RemoveAt(0);
                        ResolveNegated(entry);
                        continue;
                    }

                    if (entry.Card.Type == CardType.Counter)
                        AddStat(entry.User, StatKind.CounterSuccess); // 무효화되지 않고 처리됐다 (07 11-4)

                    int[] targets = entry.Targets.Where(t => !entry.Excluded.Contains(t)).ToArray();
                    _events.Add(new CardResolved(entry.User, entry.Card.InstanceId, entry.Card.CardId, targets, _discardPile.Count));
                }

                IReadOnlyList<CardEffect> effects = entry.Card.Effects;
                while (entry.NextEffect < effects.Count)
                {
                    CardEffect effect = effects[entry.NextEffect++];
                    effect.Apply(this, entry);
                    SettleChapchu(); // 효과 하나가 끝난 뒤의 손패로 자동 선언 (07 4.1)
                    if (_restartPending)
                    {
                        CheckElimination(); // 체인 중에 나가 한 명만 남았다 — 새 판 · 턴 지연 전에 처치 승리 (07 3절)
                        return; // 대소동 — 남은 처리는 버린다 (RequestRestart)
                    }
                    if (IsChoosing)
                        return; // 대상이 고르는 중 (1019) — 고르면 Advance 가 이어 간다
                }

                _resolving.RemoveAt(0);
            }

            DiscardChain();
            double settled = FireTraps(settleAt); // 체인이 끝난 뒤 함정 — 설치한 순서대로 (04 9.2)
            if (_restartPending)
            {
                CheckElimination(); // 함정의 대소동 — 위와 같다
                return;
            }
            DelayTurnUntil(settled);
            CheckDeathPending(settled);           // HP 가 0 이 되는 연출(4 단계) · 함정 뒤부터 대응 5초 (06 8절)
            CheckElimination();                   // 사망 대기가 생겼으면 그것이 끝난 뒤 (07 3절) — 체인 중에 나간 경우
        }

        // 체인의 카드를 쓴 순서대로 버림 더미에 쌓는다 — 맨 위 = 마지막에 쓴 카드 (04 9.1, 2026-11-15). 손패로 돌아간 카드(C01)는 빠진다.
        private void DiscardChain()
        {
            List<Card> cards = _chainOrder.Where(e => !e.Returned).Select(e => e.Card).ToList();
            _chainOrder.Clear();
            if (cards.Count == 0)
                return;

            _discardPile.AddRange(cards);
            _events.Add(new ChainDiscarded(cards.Select(c => c.InstanceId).ToArray(), cards.Select(c => c.CardId).ToArray(), _discardPile.Count));
        }

        // 다음 턴(메인 행동 전이면 턴 재개)을 at 뒤로 미룬다.
        private void DelayTurnUntil(double at)
        {
            if (_mainActionUsed)
                _nextTurnAt = System.Math.Max(_nextTurnAt, at);
            else
                _resumeAt = System.Math.Max(_resumeAt, at);
        }

        // 무효된 카드 — 효과 없이, 체인이 끝나면 버림 더미로 (DiscardChain). 「손패로 반환」(C01)이면 시전자 손패로. 사건 기록: 카드 반환(T13)
        private void ResolveNegated(ChainEntry entry)
        {
            if (entry.ReturnToCaster && IsInGame(entry.User))
            {
                entry.Returned = true;
                PlayerState caster = _players[entry.User];
                caster.Hand.Add(entry.Card);
                _events.Add(new CardReturnedToHand(entry.User, entry.Card.InstanceId, entry.Card.CardId, caster.Hand.Count));
                OnHandChanged(entry.User);
                RecordAfter(AfterKind.CardReturned, entry.User, entry.NegatedBy, 1);
                return;
            }

            _events.Add(new CardNegated(entry.User, entry.Card.InstanceId, entry.Card.CardId, _discardPile.Count));
        }

        /// <summary>entry 바로 아래 카드를 무효화한다 — 카운터는 낼 때의 맨 위 카드에 반응한다 (04 7절). 무효화 효과(Effects/)가 부른다.</summary>
        internal void Negate(ChainEntry entry, bool selfOnly)
        {
            ChainEntry countered = Below(entry);
            if (countered == null)
                return; // 맨 아래 카드는 반응한 카드가 없다

            if (selfOnly)
            {
                countered.Excluded.Add(entry.User);
                return;
            }

            countered.Negated = true;
            countered.NegatedBy = entry.User;
        }

        /// <summary>효과를 받을 사람. 본인 무효화(Excluded)와 나간 사람을 뺀다 — 처리 전에 빠진 대상은 그 효과만 불발 (04 10절).</summary>
        internal int[] ReceiversOf(EffectSubject subject, ChainEntry entry)
        {
            IEnumerable<int> receivers = subject == EffectSubject.Self ? new[] { entry.User } : entry.Targets;
            return receivers.Where(actor => !entry.Excluded.Contains(actor) && IsInGame(actor)).ToArray();
        }
    }
}
