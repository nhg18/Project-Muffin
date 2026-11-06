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

            if (!IsInGame(requester))
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

            if (!card.ReactsTo.Contains(_chain[_chain.Count - 1].Card.Type))
                return Reject(requester, RejectCode.CannotReact);

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets);
            if (targets == null)
                return Reject(requester, RejectCode.InvalidTarget);

            hand.RemoveAt(index);
            AddToChain(card, requester, targets);
            OnHandChanged(requester);
            return TakeEvents();
        }

        // 손패에서 나온 카드를 체인 맨 위에 올리고, 그 카드의 연출(1 ~ 3 단계) 뒤에 5초 반응 시간을 연다 (04 8절).
        private void AddToChain(Card card, int user, int[] targets)
        {
            _chain.Add(new ChainEntry(card, user, targets));
            _events.Add(new CardPlayed(user, card.InstanceId, card.CardId, targets, _players[user].Hand.Count, _discardPile.Count));

            _reactionOpensAt = _clock() + card.RevealSeconds;
            _reactionDeadline = _reactionOpensAt + ReactionTimeLimit;
            _events.Add(new ReactionStarted(_reactionOpensAt, _reactionDeadline));
        }

        // Tick 이 반응 마감 뒤에 부른다. 위에서부터 한 번에 처리하고 다시 기다리지 않는다 (04 8 · 9절).
        // 무효화 · 불발이어도 카드는 버림 더미로 간다 (04 4절). 다음 턴은 맨 아래 카드의 후처리 연출 뒤 (04 8절) — 사망 대기가 생기면 그 뒤.
        private void ResolveChain()
        {
            for (int i = _chain.Count - 1; i >= 0; i--)
            {
                ChainEntry entry = _chain[i];
                _discardPile.Add(entry.Card);

                if (entry.Negated)
                {
                    _events.Add(new CardNegated(entry.User, entry.Card.InstanceId, entry.Card.CardId, _discardPile.Count));
                    continue;
                }

                int[] targets = entry.Targets.Where(t => !entry.Excluded.Contains(t)).ToArray();
                _events.Add(new CardResolved(entry.User, entry.Card.InstanceId, entry.Card.CardId, targets, _discardPile.Count));

                foreach (CardEffect effect in entry.Card.Effects)
                    effect.Apply(this, entry);
            }

            _nextTurnAt = _clock() + _chain[0].Card.ResolveSeconds;
            _chain.Clear();
            CheckDeathPending(_nextTurnAt); // HP 가 0 이 되는 연출(4 단계) 뒤부터 대응 5초 (06 8절)
            CheckElimination();             // 사망 대기가 생겼으면 그것이 끝난 뒤 (07 3절) — 체인 중에 나간 경우
        }

        /// <summary>entry 바로 아래 카드를 무효화한다 — 카운터는 낼 때의 맨 위 카드에 반응한다 (04 7절). 무효화 효과(Effects/)가 부른다.</summary>
        internal void Negate(ChainEntry entry, bool selfOnly)
        {
            int index = _chain.IndexOf(entry);
            if (index <= 0)
                return; // 맨 아래 카드는 반응한 카드가 없다

            ChainEntry countered = _chain[index - 1];
            if (selfOnly)
                countered.Excluded.Add(entry.User);
            else
                countered.Negated = true;
        }

        /// <summary>효과를 받을 사람. 본인 무효화(Excluded)와 나간 사람을 뺀다 — 처리 전에 빠진 대상은 그 효과만 불발 (04 10절).</summary>
        internal int[] ReceiversOf(EffectSubject subject, ChainEntry entry)
        {
            IEnumerable<int> receivers = subject == EffectSubject.Self ? new[] { entry.User } : entry.Targets;
            return receivers.Where(actor => !entry.Excluded.Contains(actor) && IsInGame(actor)).ToArray();
        }
    }
}
