using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// 한 판. 상태를 들고, 요청을 판정하고, 결과를 IMatchEvents 로 내보낸다.
    /// Unity · 네트워크를 모른다 — 시간(now)과 랜덤 시드는 밖에서 넣어 준다.
    /// </summary>
    public class Match
    {
        private readonly IMatchEvents _events;
        private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>();
        private Deck _deck;
        private TurnOrder _turns;
        private readonly ResolveChain _chain = new ResolveChain();

        /// <summary>지금 턴인 플레이어 ID.</summary>
        public int CurrentPlayer => _turns.Current;

        public Match(IMatchEvents events)
        {
            _events = events;
        }

        /// <summary>플레이어 상태 (읽기용).</summary>
        public PlayerState GetPlayer(int playerId) => _players[playerId];

        /// <summary>
        /// 게임 시작: 턴 순서를 섞고, HP 를 채우고, 턴 순서대로 한 장씩 돌려 시작 장수만큼 나눈 뒤 첫 턴을 연다.
        /// </summary>
        public void Start(IReadOnlyList<int> playerIds, IEnumerable<Card> cards, int seed, double now)
        {
            var random = new Random(seed);
            _deck = new Deck(cards, random);
            _turns = new TurnOrder(playerIds, random);

            foreach (int id in playerIds)
                _players[id] = new PlayerState { Hp = Rules.MaxHp };

            _events.OnGameStarted(_turns.Order);
            foreach (int id in _turns.Order)
                _events.OnHpChanged(id, Rules.MaxHp);

            for (int round = 0; round < Rules.StartingHandSize; round++)
                foreach (int id in _turns.Order)
                    DrawOne(id);
            _events.OnPileCountChanged(_deck.DrawCount, _deck.DiscardCount);

            _turns.Begin(now);
            _events.OnTurnChanged(_turns.Current, _turns.Deadline);
        }

        /// <summary>
        /// 메인 행동 — 드로우 1장. 내 턴이 아니면 거절. 뽑으면 턴이 끝난다 (03-turn.md 4절).
        /// </summary>
        public void Draw(int playerId, double now)
        {
            if (!CanTakeMainAction(playerId))
                return;

            // 덱 · 버림 더미가 모두 비었을 때의 피해량은 미정(05-deck.md 5절)이라, 정해질 때까지 거절한다.
            if (!DrawOne(playerId))
            {
                _events.OnRejected(playerId, RejectCode.NoCardToDraw);
                return;
            }

            _events.OnPileCountChanged(_deck.DrawCount, _deck.DiscardCount);
            EndTurn(now);
        }

        /// <summary>
        /// 메인 행동 — 행동 카드 1장 (04-card.md 5절). 바로 처리하지 않고 체인에 올려 반응 시간을 연다.
        /// 대상은 이때 확정된다. 자동 대상 카드는 보낸 대상을 무시하고 여기서 정한다.
        /// </summary>
        public void PlayCard(int playerId, int instanceId, IReadOnlyList<int> requestedTargets, double now)
        {
            if (!CanTakeMainAction(playerId))
                return;

            PlayerState player = _players[playerId];
            Card card = player.Hand.Find(c => c.InstanceId == instanceId);
            if (card == null)
            {
                _events.OnRejected(playerId, RejectCode.NotInHand);
                return;
            }

            if (card.Type != CardType.Action)
            {
                _events.OnRejected(playerId, RejectCode.NotActionCard);
                return;
            }

            IReadOnlyList<int> targets = ResolveTargets(card.Target, playerId, requestedTargets);
            if (targets == null)
            {
                _events.OnRejected(playerId, RejectCode.InvalidTarget);
                return;
            }

            player.Hand.Remove(card);
            _chain.Push(new ChainLink(card, playerId, targets), now);
            _events.OnCardPlayed(playerId, card.InstanceId, card.CardId, targets);
            _events.OnHandCountChanged(playerId, player.Hand.Count);
            _events.OnReactionStarted(_chain.ReactionDeadline);
        }

        /// <summary>
        /// 시간 진행. 체인이 열려 있으면 반응 시간만 본다 — 끝나면 체인을 한 번에 처리하고 턴을 넘긴다.
        /// 체인이 없으면 턴 마감이 지났을 때 아무것도 하지 않은 채 턴을 넘긴다 (03-turn.md 4절).
        /// </summary>
        public void Tick(double now)
        {
            if (_chain.IsOpen)
            {
                if (_chain.IsReactionOver(now))
                    ResolveAll(now);
                return;
            }

            if (_turns.IsExpired(now))
                EndTurn(now);
        }

        // 내 턴이고, 이번 턴 메인 행동(드로우 · 행동 카드)을 아직 안 했는가. 아니면 거절을 보낸다.
        private bool CanTakeMainAction(int playerId)
        {
            if (playerId != _turns.Current)
            {
                _events.OnRejected(playerId, RejectCode.NotYourTurn);
                return false;
            }

            if (_chain.IsOpen)
            {
                _events.OnRejected(playerId, RejectCode.MainActionUsed);
                return false;
            }

            return true;
        }

        // 카드의 대상 종류에 맞는 대상 목록. 고르는 대상이 잘못됐으면 null (04-card.md 10절).
        private IReadOnlyList<int> ResolveTargets(TargetType type, int playerId, IReadOnlyList<int> requested)
        {
            requested = requested ?? Array.Empty<int>();
            switch (type)
            {
                case TargetType.None:
                    return Array.Empty<int>();
                case TargetType.Self:
                    return new[] { playerId };
                case TargetType.AllOthers:
                    return _turns.Order.Where(id => id != playerId).ToArray();
                case TargetType.Everyone:
                    return _turns.Order.ToArray();
                case TargetType.OneOther:
                    return requested.Count == 1 && IsOther(requested[0], playerId) ? new[] { requested[0] } : null;
                case TargetType.TwoOthers:
                    return requested.Count == 2 && requested[0] != requested[1]
                           && IsOther(requested[0], playerId) && IsOther(requested[1], playerId)
                        ? new[] { requested[0], requested[1] }
                        : null;
                default:
                    return null;
            }
        }

        private bool IsOther(int targetId, int playerId) => targetId != playerId && _players.ContainsKey(targetId);

        // 맨 위부터 꺼내 처리하고 버림 더미로 보낸다. 처리 중에는 다시 기다리지 않는다 (04-card.md 8절).
        // 무효화 · 불발이어도 턴은 끝난다 (04-card.md 5절).
        private void ResolveAll(double now)
        {
            while (_chain.IsOpen)
            {
                ChainLink link = _chain.Pop();
                if (!link.IsNegated)
                    ApplyEffects(link);

                _deck.Discard(link.Card);
                _events.OnCardResolved(link.Card.InstanceId, link.IsNegated);
            }

            _events.OnPileCountChanged(_deck.DrawCount, _deck.DiscardCount);
            EndTurn(now);
        }

        private void ApplyEffects(ChainLink link)
        {
            foreach (CardEffect effect in link.Card.Effects)
            {
                IReadOnlyList<int> receivers = effect.Subject == EffectSubject.Self ? new[] { link.OwnerId } : link.Targets;
                switch (effect.Type)
                {
                    case EffectType.Damage:
                        foreach (int id in receivers)
                            SetHp(id, _players[id].Hp - effect.Amount);
                        break;
                }
            }
        }

        // HP 는 0 ~ 최대 사이. 0 이 됐을 때의 사망 대기 · 사망은 6단계에서 넣는다.
        private void SetHp(int playerId, int hp)
        {
            PlayerState player = _players[playerId];
            int clamped = Math.Max(0, Math.Min(Rules.MaxHp, hp));
            if (clamped == player.Hp)
                return;

            player.Hp = clamped;
            _events.OnHpChanged(playerId, clamped);
        }

        private bool DrawOne(int playerId)
        {
            if (!_deck.TryDraw(out Card card))
                return false;

            PlayerState player = _players[playerId];
            player.Hand.Add(card);
            _events.OnCardDrawn(playerId, card.InstanceId, card.CardId);
            _events.OnHandCountChanged(playerId, player.Hand.Count);
            return true;
        }

        private void EndTurn(double now)
        {
            // 사망으로 턴 순서에서 빠지는 규칙은 6단계에서 넣는다. 지금은 모두 생존.
            _turns.Advance(now, _ => true);
            _events.OnTurnChanged(_turns.Current, _turns.Deadline);
        }
    }
}
