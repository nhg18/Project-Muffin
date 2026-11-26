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
        internal const int LastDrawCount = 1; // 손패가 찹츄 장수 − 1 일 때 (05-deck.md 4절)

        // 턴 뽑기 장수 — 방장 판정과 봇 점수가 함께 쓴다 (05-deck.md 4절)
        internal static int TurnDrawCountFor(int handCount) => handCount == ChapchuHandCount - 1 ? LastDrawCount : TurnDrawCount;

        private readonly List<Card> _deck = new List<Card>();
        private readonly List<Card> _discardPile = new List<Card>();

        /// <summary>
        /// 드로우 요청 — TurnDrawCount 장(손패가 찹츄 장수 − 1 이면 1장). 메인 행동이라 끝나면 턴이 넘어간다 (03-turn.md 3절).
        /// 뽑아서 손패가 정확히 10장이 되면 찹츄가 자동 선언되고 턴은 보통대로 끝난다 (03-turn.md 4절 · 07 4.1).
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

            if (IsTurnTimeUp(_clock()))
                return Reject(requester, RejectCode.NotYourTurn);

            _players[requester].TimeoutStreak = 0; // 직접 턴을 끝냈다 (03-turn.md 4절)
            DrawForTurn(requester, TurnDrawCountFor(_players[requester].Hand.Count));
            return TakeEvents();
        }

        // 시간 초과 자동 뽑기 장수 — 자동 뽑기로는 찹츄 장수(10)가 되지 않게: 7장 이하 2 · 8장 1 · 9장 0 · 10장 이상 2 (03-turn.md 4절, PM 2026-11-19).
        private static int AutoDrawCount(int handCount) =>
            handCount < ChapchuHandCount ? System.Math.Min(TurnDrawCount, ChapchuHandCount - 1 - handCount) : TurnDrawCount;

        // 턴 뽑기(메인 행동)의 적용 — 직접 뽑기 · 시간 초과 자동 뽑기가 함께 쓴다. 끝나면 턴이 넘어간다.
        private void DrawForTurn(int actor, int count)
        {
            for (int i = 0; i < count && _players[actor].Hp > 0; i++)
                CountDraw(actor); // 덱 · 버림 더미가 모두 비면 못 뽑은 장마다 빈 덱 피해 (05-deck.md 5절) — 그 피해로 HP 가 0 이 되면 멈춘다
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

        // 뽑기 1장 — 실제로 뽑았으면 「카드 드로우」 지표에 센다 (07 11-4). 시작 손패 · 다시 받기는 DrawOne 을 바로 부른다.
        private void CountDraw(int actor)
        {
            if (DrawOne(actor))
                AddStat(actor, StatKind.Drawn);
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
