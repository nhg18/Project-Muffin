using System.Collections.Generic;

namespace Chapchu.Game
{
    // GameServer.Deck.cs — 덱 · 버림 더미 원본과 드로우 (05-deck.md). 손패 원본은 PlayerState.Hand
    public partial class GameServer
    {
        // 확정 (05-deck.md 2절). 덱 총 구성(카드별 매수)은 미정이라 임시 덱 에셋이 대신한다.
        private const int InitialHandCount = 5;

        private readonly List<Card> _deck = new List<Card>();
        private readonly List<Card> _discardPile = new List<Card>();

        /// <summary>드로우 요청 — 메인 행동이라 끝나면 턴이 넘어간다 (03-turn.md 3절).</summary>
        public IReadOnlyList<GameEvent> Draw(int requester)
        {
            if (requester != CurrentTurnActor)
                return Reject(requester, RejectCode.NotYourTurn);

            if (_mainActionUsed)
                return Reject(requester, RejectCode.MainActionUsed);

            DrawOne(requester);
            CheckDeathPending(_clock()); // 빈 덱 피해에는 카드 연출이 없다 — 대응 시간은 바로 (06 8절)

            // 드로우는 끝나면 턴 종료. 빈 덱 피해로 사망 대기가 생기면 그것이 끝난 뒤 (03-turn.md 4절)
            if (IsDeathPending)
            {
                _mainActionUsed = true;
                _nextTurnAt = _clock();
            }
            else
            {
                AdvanceTurn();
            }

            return TakeEvents();
        }

        private void InitDeck(IReadOnlyList<Card> cards)
        {
            _deck.Clear();
            _discardPile.Clear();
            _deck.AddRange(cards);
            Shuffle(_deck);
        }

        private void DealInitialHands(IReadOnlyList<int> actors)
        {
            foreach (int actor in actors)
            {
                for (int i = 0; i < InitialHandCount; i++)
                    DrawOne(actor);
            }
        }

        // 덱 맨 위 1장을 actor 손패로. 모든 뽑기(시작 배분 · 뽑기 요청 · 카드 효과)가 여기를 지난다.
        // 덱이 비면 버림 더미로 다시 채운다. 덱 · 버림 더미가 모두 비면 카드 대신 피해 1 · 2 · 3 … 를 받고 false (05-deck.md 5절).
        private bool DrawOne(int actor)
        {
            if (_deck.Count == 0)
                RefillFromDiscardPile();

            if (_deck.Count == 0)
            {
                PlayerState player = _players[actor];
                player.EmptyDraws++;
                _events.Add(new EmptyDeckDamaged(actor, player.EmptyDraws));
                DealDamage(actor, player.EmptyDraws);
                return false;
            }

            Card card = _deck[0];
            _deck.RemoveAt(0);

            List<Card> hand = _players[actor].Hand;
            hand.Add(card);

            _events.Add(new CardDrawn(actor, card.InstanceId, card.CardId, hand.Count, _deck.Count));
            return true;
        }

        private void RefillFromDiscardPile()
        {
            if (_discardPile.Count == 0)
                return;

            _deck.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_deck);

            _events.Add(new DeckRefilled(_deck.Count));
        }
    }
}
