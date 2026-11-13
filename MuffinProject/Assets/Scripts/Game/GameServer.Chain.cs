using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Chain.cs — 처리 체인 · 반응 시간 · 카운터 (04-card.md 7 ~ 9절)
    public partial class GameServer
    {
        // 확정 (04-card.md 2절).
        private const double ReactionTimeLimit = 10;

        private readonly List<ChainEntry> _chain = new List<ChainEntry>(); // [0] = 맨 아래(메인 행동 카드), 끝 = 맨 위
        private double _reactionOpensAt;  // 이 전은 연출 중 — 카운터를 받지 않는다 (04 8절)
        private double _reactionDeadline;

        // 처리 중에 멈춘 카드들 — [0] 부터 이어서 처리한다. 피해 뒤 사망 대기 · 함정 시간을 기다리는 동안만 남아 있다 (ContinueResolve).
        private readonly List<ChainEntry> _resolving = new List<ChainEntry>();
        private bool IsResolvePaused => _resolving.Count > 0;

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

        // 손패(함정은 슬롯)에서 나온 카드를 체인 맨 위에 올리고, 그 카드의 연출(1 ~ 3 단계) 뒤에 10초 반응 시간을 연다 (04 8절).
        private ChainEntry AddToChain(Card card, int user, int[] targets, bool fromTrapSlot = false)
        {
            var entry = new ChainEntry(card, user, targets) { Below = _chain.Count > 0 ? _chain[_chain.Count - 1] : null };
            foreach (CardEffect effect in card.Effects)
                effect.Prepare(this, entry); // 낼 때 정하는 값 (116 뽑을 장수)
            _chain.Add(entry);
            if (fromTrapSlot)
                _events.Add(new TrapActivated(user, card.InstanceId, card.CardId, targets, _players[user].Traps.Count));
            else
                _events.Add(new CardPlayed(user, card.InstanceId, card.CardId, targets, _players[user].Hand.Count, _discardPile.Count));

            _reactionOpensAt = _clock() + card.RevealSeconds;
            _reactionDeadline = _reactionOpensAt + ReactionTimeLimit;
            _events.Add(new ReactionStarted(_reactionOpensAt, _reactionDeadline));
            AddCountersUsable(entry);
            return entry;
        }

        // 반응 시간이 정해질 때마다, 지금 맨 위 카드에 낼 수 있는 카운터를 손패에 가진 사람에게 그 목록을 알린다 — 화면의 초록 빛용 (10-ui.md 5절, 09-network.md 3절: 본인에게만).
        // PlayCounter 와 같은 판정(CanPlayCounter)을 쓴다 (대상 확정은 뺀다 — 낼 때 판정). 낼 수 있는 것이 없는 사람에겐 보내지 않는다.
        private void AddCountersUsable(ChainEntry top)
        {
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
                    _events.Add(new CountersUsable(actor, usable));
            }
        }

        // actor 가 손패의 card 를 카운터로 top 에 낼 수 있는가 (대상 확정 전) — PlayCounter 와 AddCountersUsable 이 같이 쓴다.
        // 사망 대기 중엔 사망 관련 카운터만 (06 3 · 8절 — C11).
        private bool CanPlayCounter(int actor, Card card, ChainEntry top) =>
            card.Type == CardType.Counter
            && (_players[actor].Life != LifeState.DeathPending || card.Condition == ReactCondition.WouldKillMe)
            && CanReactTo(card, actor, top);

        /// <summary>
        /// card(카운터 · 「카드 반응」 함정)로 맨 위 카드 top 에 반응할 수 있는가 — 반응 대상 종류 + 반응 조건 (04-card.md 7절).
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
        // 무효화 · 불발이어도 카드는 버림 더미로 간다 (04 4절). 맨 아래 카드의 후처리 연출 뒤에 —
        // 메인 행동의 체인이면 다음 턴, 메인 행동 전에 끼어든 체인(함정)이면 턴을 이어 간다. 함정 시간 · 사망 대기가 생기면 그 뒤.
        private void ResolveChain()
        {
            double animationEnd = _clock() + _chain[0].Card.ResolveSeconds;

            // 위에서부터 처리 — 멈춰 있던 처리(_resolving)가 있으면 이 체인을 먼저 마치고 그것으로 돌아간다 (06 5절 「기존 처리로 복귀」)
            for (int i = 0; i < _chain.Count; i++)
                _resolving.Insert(0, _chain[i]);
            _chain.Clear();
            ContinueResolve(animationEnd);
        }

        // 처리할 카드가 남아 있는 동안 위에서부터 효과를 적용한다. 피해 효과 뒤에 효과가 더 있고 그 피해로 사망 대기 · 함정 시간이 생기면
        // 거기서 멈추고, 그것이 끝난 뒤 Advance 가 다시 부른다 (06 5절 「HP 반영 → 사망 대기 판정 → 함정·카운터 처리 → 기존 처리로 복귀」 · 11-card-list 115).
        // settleAt = 이번에 적용하는 결과의 후처리 연출(3 · 4 단계)이 끝나는 시각 — 처음엔 맨 아래 카드의, 이어 갈 때는 멈췄던 카드의 (남은 효과 · 고른 결과).
        // 사망 대기 · 함정 시간은 이 시각에 시작하고, 다음 턴(메인 행동 전이면 턴 재개)도 이 시각 뒤로 미룬다 (04 8 · 9.3절 · 06 8절).
        private void ContinueResolve(double settleAt)
        {
            if (_mainActionUsed)
                _nextTurnAt = System.Math.Max(_nextTurnAt, settleAt);
            else
                _resumeAt = System.Math.Max(_resumeAt, settleAt);

            while (_resolving.Count > 0)
            {
                ChainEntry entry = _resolving[0];
                if (entry.NextEffect > 0 && Settle(settleAt))
                    return; // 멈췄던 카드 — 그사이 끼어든 체인(사망 대기 중 함정 등)을 처리했어도 사망 대기 · 함정 시간이 남았으면 더 기다린다

                if (entry.NextEffect == 0)
                {
                    if (entry.Negated)
                    {
                        _resolving.RemoveAt(0);
                        ResolveNegated(entry);
                        continue;
                    }

                    _discardPile.Add(entry.Card);
                    int[] targets = entry.Targets.Where(t => !entry.Excluded.Contains(t)).ToArray();
                    _events.Add(new CardResolved(entry.User, entry.Card.InstanceId, entry.Card.CardId, targets, _discardPile.Count));
                }

                IReadOnlyList<CardEffect> effects = entry.Card.Effects;
                while (entry.NextEffect < effects.Count)
                {
                    CardEffect effect = effects[entry.NextEffect++];
                    effect.Apply(this, entry);
                    if (_restartPending)
                        return; // 대소동 — 남은 처리는 버린다 (RequestRestart)
                    if (IsChoosing)
                        return; // 대상이 고르는 중 (119) — 고르면 Advance 가 이어 간다
                    if (effect.Tag == EffectTag.Damage && entry.NextEffect < effects.Count && Settle(settleAt))
                        return;
                }

                _resolving.RemoveAt(0);
            }

            Settle(settleAt);
            CheckElimination();                    // 함정 시간 · 사망 대기가 생겼으면 그것이 끝난 뒤 (07 3절) — 체인 중에 나간 경우
        }

        // 피해의 뒤처리를 연다. 사망 대기 · 함정 시간 중 하나라도 생겼으면 true — 처리를 멈추고 그것이 끝나길 기다린다.
        private bool Settle(double startsAt)
        {
            CheckDeathPending(startsAt);       // HP 가 0 이 되는 연출(4 단계) 뒤부터 대응 5초 (06 8절)
            OpenTrapWindowIfNeeded(startsAt);  // 「피해 받은 후」 함정 (04 6절)
            return IsDeathPending || IsTrapWindowOpen;
        }

        // 무효된 카드 — 효과 없이 버림 더미로, 「손패로 반환」(C01)이면 시전자 손패로. 사건 기록: 내 함정 무효(T12) · 카드 반환(T13)
        private void ResolveNegated(ChainEntry entry)
        {
            if (entry.ReturnToCaster && IsInGame(entry.User))
            {
                PlayerState caster = _players[entry.User];
                caster.Hand.Add(entry.Card);
                _events.Add(new CardReturnedToHand(entry.User, entry.Card.InstanceId, entry.Card.CardId, caster.Hand.Count));
                OnHandChanged(entry.User);
                RecordAfter(AfterKind.CardReturned, entry.User, entry.NegatedBy, 1);
                return;
            }

            _discardPile.Add(entry.Card);
            _events.Add(new CardNegated(entry.User, entry.Card.InstanceId, entry.Card.CardId, _discardPile.Count));
            if (entry.Card.Type == CardType.Trap && entry.NegatedBy >= 0)
                RecordAfter(AfterKind.MyTrapNegated, entry.User, entry.NegatedBy, 1);
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
