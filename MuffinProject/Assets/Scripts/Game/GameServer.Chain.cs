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

            // 사망 대기 중엔 사망 관련 카운터만 (06 3 · 8절 — C11)
            if (_players[requester].Life == LifeState.DeathPending && card.Condition != ReactCondition.WouldKillMe)
                return Reject(requester, RejectCode.CannotReact);

            ChainEntry top = _chain[_chain.Count - 1];
            if (!CanReactTo(card, requester, top))
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
            var entry = new ChainEntry(card, user, targets);
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
        // PlayCounter 의 검사와 같은 조건이다 (대상 확정은 뺀다 — 낼 때 판정). 낼 수 있는 것이 없는 사람에겐 보내지 않는다.
        private void AddCountersUsable(ChainEntry top)
        {
            foreach (KeyValuePair<int, PlayerState> pair in _players)
            {
                int actor = pair.Key;
                if (!IsInGame(actor))
                    continue;

                bool deathPending = pair.Value.Life == LifeState.DeathPending; // 사망 대기 중엔 사망 관련 카운터만 (06 3 · 8절)
                int[] usable = pair.Value.Hand
                    .Where(c => c.Type == CardType.Counter && (!deathPending || c.Condition == ReactCondition.WouldKillMe) && CanReactTo(c, actor, top))
                    .Select(c => c.InstanceId)
                    .ToArray();
                if (usable.Length > 0)
                    _events.Add(new CountersUsable(actor, usable));
            }
        }

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
            for (int i = _chain.Count - 1; i >= 0; i--)
            {
                ChainEntry entry = _chain[i];
                if (entry.Negated)
                {
                    ResolveNegated(entry);
                    continue;
                }

                _discardPile.Add(entry.Card);
                int[] targets = entry.Targets.Where(t => !entry.Excluded.Contains(t)).ToArray();
                _events.Add(new CardResolved(entry.User, entry.Card.InstanceId, entry.Card.CardId, targets, _discardPile.Count));

                foreach (CardEffect effect in entry.Card.Effects)
                    effect.Apply(this, entry);
            }

            double animationEnd = _clock() + _chain[0].Card.ResolveSeconds;
            if (_mainActionUsed)
                _nextTurnAt = System.Math.Max(_nextTurnAt, animationEnd);
            else
                _resumeAt = animationEnd;

            _chain.Clear();
            CheckDeathPending(animationEnd);       // HP 가 0 이 되는 연출(4 단계) 뒤부터 대응 5초 (06 8절)
            OpenTrapWindowIfNeeded(animationEnd);  // 「피해 받은 후」 함정 (04 6절)
            CheckElimination();                    // 함정 시간 · 사망 대기가 생겼으면 그것이 끝난 뒤 (07 3절) — 체인 중에 나간 경우
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
