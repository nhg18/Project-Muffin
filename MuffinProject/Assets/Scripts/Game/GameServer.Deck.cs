using System.Collections.Generic;

namespace Chapchu.Game
{
    // GameServer.Deck.cs — 덱 · 버림 더미 원본과 드로우 (05-deck.md). 손패 원본은 PlayerState.Hand
    public partial class GameServer
    {
        // 확정 (05-deck.md 2절). 덱 총 구성(카드별 매수)은 미정이라 임시 덱 에셋이 대신한다.
        internal const int InitialHandCount = 5;
        // 턴 뽑기(메인 행동) 장수 — 손패가 찹츄 장수 − 1 이면 1장만 뽑아 딱 맞게 한다 (05-deck.md 4절, 2026-11-11). 카드 효과 뽑기는 효과의 장수 그대로.
        public const int TurnDrawCount = 2;

        private readonly List<Card> _deck = new List<Card>();
        private readonly List<Card> _discardPile = new List<Card>();

        /// <summary>
        /// 드로우 요청 — TurnDrawCount 장(손패가 찹츄 장수 − 1 이면 1장). 메인 행동이라 끝나면 턴이 넘어간다 (03-turn.md 3절).
        /// 뽑아서 손패가 정확히 10장이 되면 바로 넘기지 않고 선언 대기를 연다 (03-turn.md 4절).
        /// </summary>
        public IReadOnlyList<GameEvent> Draw(int requester)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);

            if (requester != CurrentTurnActor)
                return Reject(requester, RejectCode.NotYourTurn);

            if (!IsInGame(requester) || _players[requester].Life != LifeState.Alive)
                return Reject(requester, RejectCode.NotAlive);

            if (_mainActionUsed)
                return Reject(requester, RejectCode.MainActionUsed);

            if (IsPaused(_clock()))
                return Reject(requester, RejectCode.Resolving);

            int count = _players[requester].Hand.Count == ChapchuHandCount - 1 ? 1 : TurnDrawCount;
            for (int i = 0; i < count && _players[requester].Hp > 0; i++)
                DrawOne(requester); // 덱 · 버림 더미가 모두 비면 못 뽑은 장마다 빈 덱 피해 (05-deck.md 5절) — 그 피해로 HP 가 0 이 되면 멈춘다
            CheckDeathPending(_clock()); // 빈 덱 피해에는 카드 연출이 없다 — 대응 시간은 바로 (06 8절)

            // 드로우는 끝나면 턴 종료. 빈 덱 피해로 사망 대기가 생기면 그것이 끝난 뒤 (03-turn.md 4절)
            if (IsDeathPending)
            {
                _mainActionUsed = true;
                _nextTurnAt = _clock();
            }
            else if (_players[requester].Hand.Count == ChapchuHandCount)
            {
                // 뽑아서 정확히 10장 — 턴이 바로 끝나지 않고 선언 대기가 열린다. 선언이 승인되면 그 순간, 아니면 대기가 끝날 때 턴 종료 (03-turn.md 4절 · 07 4.1)
                _mainActionUsed = true; // 대기 중 다른 입력(카드 · 다시 뽑기 · 함정 설치)은 메인 행동을 한 것으로 거절된다
                _declareWait = true;
                _nextTurnAt = _clock() + DeclareWaitSeconds;
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
                DealDamage(actor, player.EmptyDraws, -1);
                return false;
            }

            Card card = _deck[0];
            _deck.RemoveAt(0);

            List<Card> hand = _players[actor].Hand;
            hand.Add(card);

            _events.Add(new CardDrawn(actor, card.InstanceId, card.CardId, hand.Count, _deck.Count));
            OnHandChanged(actor);
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
