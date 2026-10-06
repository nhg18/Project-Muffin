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
            public List<int> Targets; // 효과를 받을 사람 (사용 시점에 확정, 04-card.md 10절)
        }

        private readonly List<ChainEntry> _chain = new List<ChainEntry>();
        private double _reactionDeadline;

        private bool IsChainOpen => _chain.Count > 0;

        // 게임 시작 1회: 지난 판 체인 · 반응 마감을 지운다 (방 상태는 방이 남아 있으면 다음 판까지 남는다).
        private void ResetChain()
        {
            _chain.Clear();
            _outbox.SetRoomState(RoomProps.ReactionDeadline, 0d);
        }

        // 손패에서 이미 뺀 카드를 체인 맨 위에 올리고 반응 5초를 (다시) 시작한다.
        private void PushChain(int actor, CardInstance card, CardRule rule, int[] targets)
        {
            _chain.Add(new ChainEntry { Actor = actor, Card = card, Rule = rule, Targets = targets.ToList() });
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
            if (entry.Rule.Damage > 0)
            {
                foreach (int target in entry.Targets)
                    SetHp(target, _players[target].Hp - entry.Rule.Damage);
            }

            _outbox.SendCardResolved(entry.Actor, entry.Card.InstanceId, entry.Card.CardId, entry.Targets.ToArray(), false);
        }
    }
}
