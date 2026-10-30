using System;
using System.Collections.Generic;

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
            if (playerId != _turns.Current)
            {
                _events.OnRejected(playerId, RejectCode.NotYourTurn);
                return;
            }

            // 덱 · 버림 더미가 모두 비었을 때의 피해량은 미정(05-deck.md 5절)이라, 정해질 때까지 거절한다.
            if (!DrawOne(playerId))
            {
                _events.OnRejected(playerId, RejectCode.NoCardToDraw);
                return;
            }

            _events.OnPileCountChanged(_deck.DrawCount, _deck.DiscardCount);
            EndTurn(now);
        }

        /// <summary>시간 진행. 턴 마감이 지났으면 아무것도 하지 않은 채 턴을 넘긴다 (03-turn.md 4절).</summary>
        public void Tick(double now)
        {
            if (_turns.IsExpired(now))
                EndTurn(now);
        }

        private bool DrawOne(int playerId)
        {
            if (!_deck.TryDraw(out Card card))
                return false;

            PlayerState player = _players[playerId];
            player.Hand.Add(card);
            _events.OnCardDrawn(playerId, card);
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
