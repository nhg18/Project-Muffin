using System;
using System.Collections.Generic;
using Chapchu.Core;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    // GameServer.Deck.cs — 덱 · 버림 더미 원본과 드로우 처리 (05-deck.md)
    public partial class GameServer
    {
        // 확정(05-deck.md 2절). 덱 총 구성(카드별 매수)은 미정이라 InitDeck 인자로 받는 더미 DeckRecipe 가 대신한다.
        private const int InitialHandCount = 5;

        private readonly List<CardInstance> _deck = new List<CardInstance>();
        private readonly List<CardInstance> _discardPile = new List<CardInstance>();
        private int _nextInstanceId = 0;

        /// <summary>덱을 카드 ID 목록으로 채우고 인스턴스 ID를 부여한 뒤 섞는다.</summary>
        public void InitDeck(IReadOnlyList<int> cardIds)
        {
            _deck.Clear();
            _discardPile.Clear();
            _nextInstanceId = 0;

            foreach (int cardId in cardIds)
                _deck.Add(new CardInstance(_nextInstanceId++, cardId));

            Shuffle(_deck);
            _outbox.SetRoomState(RoomProps.DeckCount, _deck.Count);
        }

        /// <summary>게임 시작 1회: 모든 플레이어에게 확정 수치(5장)만큼 나눠 준다. 턴 검증 없이 마스터가 바로 실행한다.</summary>
        public void DealInitialHands(IReadOnlyList<int> actors)
        {
            foreach (int actor in actors)
            {
                for (int i = 0; i < InitialHandCount; i++)
                    DrawOne(actor);
            }
        }

        public void Draw(int requester)
        {
            if (requester != CurrentTurnActor)
            {
                _outbox.Reject(requester, "내 턴이 아닙니다.");
                return;
            }

            // TODO(손패 상한): 값이 확정되면 여기서 검사해 거절한다 (05-deck.md 6절 — 지금은 상한 없음).

            if (_deck.Count == 0)
                RefillFromDiscardPile();

            if (_deck.Count == 0)
            {
                // 덱 · 버림 더미가 모두 0장 → 0장으로 처리하고 거절만 한다 (05-deck.md 5절).
                _outbox.Reject(requester, "뽑을 카드가 없습니다.");
                return;
            }

            DrawOne(requester);
        }

        private void DrawOne(int actor)
        {
            CardInstance card = _deck[0];
            _deck.RemoveAt(0);

            _outbox.SendDrawnCard(actor, card.CardId);
            _outbox.SetRoomState(RoomProps.DeckCount, _deck.Count);
        }

        // 버림 더미를 채우는 경로(카드 사용 · 강제 버림)는 이 리팩토링 범위 밖이라 아직 비어 있다. 회수 규칙만 미리 준비해 둔다.
        private void RefillFromDiscardPile()
        {
            if (_discardPile.Count == 0) return;

            _deck.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_deck);
        }

        private static readonly Random _shuffleRng = new Random();

        private static void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _shuffleRng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
