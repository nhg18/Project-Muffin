using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    // GameServer.Chain.cs — 처리 체인 · 반응 5초 · 역순 처리 (04-card.md 7 · 8 · 9절). 체인이 있는 동안이 03-turn.md 의 Resolving
    public partial class GameServer
    {
        // 확정(04-card.md 2 · 8절). 카드가 체인에 올라갈 때마다 5초를 새로 시작한다.
        // 체인 깊이 제한은 미정(04-card.md 15절 5) — 제한 없음.
        private const double ReactionTimeLimit = 5;

        // 체인 카드 1장. _chain[0] 이 처음 낸 행동 카드, 마지막이 맨 위(가장 최근 카드).
        private class ChainEntry
        {
            public int Actor;
            public CardInstance Card;
            public CardRule Rule;
            public List<int> Targets; // 효과를 받을 사람 (사용 시점에 확정, 04-card.md 10절). 위 카운터의 '본인만 무효'가 여기서 사람을 뺀다
            public ChainEntry Countered; // 카운터가 반응한 카드 (행동 카드면 null)
            public bool Negated;         // 위 카드가 무효로 만들었다 — 효과 없이 버림 더미로
        }

        private readonly List<ChainEntry> _chain = new List<ChainEntry>();
        private readonly List<int> _lastChainCards = new List<int>(); // 방금 끝난 체인의 인스턴스 ID — 늦게 온 카운터를 '반응 시간 끝'으로 거절하려고
        private double _reactionDeadline;

        private bool IsChainOpen => _chain.Count > 0;

        /// <summary>
        /// 카운터 카드 사용 (04-card.md 7절). 내 턴이 아니어도 반응 시간 안이면 낼 수 있다.
        /// 반응 대상은 체인 맨 위 카드만 — 동시에 오면 먼저 도착한 1개만 맨 위가 되고, 늦은 요청은 대상이 바뀌어 거절된다.
        /// </summary>
        public void Counter(int requester, int cardInstanceId, int targetCardInstanceId)
        {
            if (!IsChainOpen)
            {
                // 방금 끝난 체인의 카드를 노렸으면 반응 시간이 지나 도착한 것이다 (04-card.md 8절)
                _outbox.Reject(requester, _lastChainCards.Contains(targetCardInstanceId) ? RejectCode.ReactionClosed : RejectCode.NothingToCounter);
                return;
            }

            if (_clock() >= _reactionDeadline)
            {
                _outbox.Reject(requester, RejectCode.ReactionClosed);
                return;
            }

            if (!_players.TryGetValue(requester, out PlayerState player))
            {
                _outbox.Reject(requester, RejectCode.NotInHand);
                return;
            }

            List<CardInstance> hand = player.Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
            {
                _outbox.Reject(requester, RejectCode.NotInHand);
                return;
            }

            CardInstance card = hand[index];
            CardRule rule = _cardRules[card.CardId];
            if (rule.Type != CardType.Counter)
            {
                _outbox.Reject(requester, RejectCode.NotCounterCard);
                return;
            }

            ChainEntry top = _chain[_chain.Count - 1];
            if (top.Card.InstanceId != targetCardInstanceId)
            {
                _outbox.Reject(requester, RejectCode.TargetChanged);
                return;
            }

            // 반응 조건 — 본인만 무효(C05)는 행동 카드에만 (11-card-list.md C05)
            if (rule.NegateForSelf && top.Rule.Type != CardType.Action)
            {
                _outbox.Reject(requester, RejectCode.CounterConditionNotMet);
                return;
            }

            int[] targets = ResolveTargets(requester, rule.Target, null);
            if (targets == null)
            {
                _outbox.Reject(requester, RejectCode.InvalidTarget);
                return;
            }

            // 적용 — 손패에서 빼 체인 맨 위에 올리고 반응 5초를 다시 시작한다 (04-card.md 7절)
            hand.RemoveAt(index);
            PushChain(requester, card, rule, targets, top);
            _outbox.SetPlayerState(requester, PlayerProps.HandCount, hand.Count);
        }

        // 게임 시작 1회: 지난 판 체인 · 반응 마감을 지운다 (방 상태는 방이 남아 있으면 다음 판까지 남는다).
        private void ResetChain()
        {
            _chain.Clear();
            _lastChainCards.Clear();
            _outbox.SetRoomState(RoomProps.ReactionDeadline, 0d);
        }

        // 손패에서 이미 뺀 카드를 체인 맨 위에 올리고 반응 5초를 (다시) 시작한다.
        // countered = 카운터가 반응한 카드 (행동 카드면 null).
        private void PushChain(int actor, CardInstance card, CardRule rule, int[] targets, ChainEntry countered)
        {
            _chain.Add(new ChainEntry { Actor = actor, Card = card, Rule = rule, Targets = targets.ToList(), Countered = countered });
            _reactionDeadline = _clock() + ReactionTimeLimit;

            _outbox.SendCardUsed(actor, card.InstanceId, card.CardId, targets);
            _outbox.SetRoomState(RoomProps.ReactionDeadline, _reactionDeadline);
        }

        // 반응 시간이 끝났다: 맨 위(마지막 카드)부터 처리 → 체인 카드 전부 버림 더미 → 턴 종료 (04-card.md 4 · 9절, 03-turn.md 3절).
        private void ResolveChain()
        {
            for (int i = _chain.Count - 1; i >= 0; i--)
                ResolveEntry(_chain[i]);

            // 무효 · 불발이어도 사용한 카드는 모두 버림 더미로 (04-card.md 4절)
            int owner = _chain[0].Actor;
            _discardPile.AddRange(_chain.Select(e => e.Card));
            _lastChainCards.Clear();
            _lastChainCards.AddRange(_chain.Select(e => e.Card.InstanceId));
            _chain.Clear();

            _outbox.SetRoomState(RoomProps.DiscardCount, _discardPile.Count);
            _outbox.SetRoomState(RoomProps.ReactionDeadline, 0d);

            // 행동 카드를 낸 사람이 체인 중에 나가 턴이 이미 넘어갔으면(RemoveFromTurnOrder) 다시 넘기지 않고, 지금 턴 주인에게 시간만 새로 준다.
            if (CurrentTurnActor == owner)
                AdvanceTurn();
            else
                SetTurn(CurrentTurnActor);
        }

        private void ResolveEntry(ChainEntry entry)
        {
            if (entry.Negated)
            {
                _outbox.SendCardResolved(entry.Actor, entry.Card.InstanceId, entry.Card.CardId, new int[0], true);
                return;
            }

            if (entry.Rule.Damage > 0)
            {
                foreach (int target in entry.Targets)
                    SetHp(target, _players[target].Hp - entry.Rule.Damage);
            }

            // 본인만 무효(C05): 반응한 카드의 대상에서 나를 뺀다. 그래서 대상이 하나도 안 남으면 그 카드는 무효
            if (entry.Rule.NegateForSelf && entry.Countered != null && entry.Countered.Targets.Remove(entry.Actor) && entry.Countered.Targets.Count == 0)
                entry.Countered.Negated = true;

            _outbox.SendCardResolved(entry.Actor, entry.Card.InstanceId, entry.Card.CardId, entry.Targets.ToArray(), false);
        }
    }
}
