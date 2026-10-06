using System.Collections.Generic;
using Chapchu.Core;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    // GameServer.Deck.cs — 덱 · 버림 더미 원본과 드로우 · 버림 처리 (05-deck.md). 손패 원본은 PlayerState.Hand
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
            foreach (PlayerState player in _players.Values)
                player.Hand.Clear();
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

            if (!DrawOne(requester))
            {
                // TODO(미정): 덱 · 버림 더미가 모두 0장일 때 처리는 기획 미정 (05-deck.md 5절). 확정 전까지 상태 변경 없이 거절만 한다.
                _outbox.Reject(requester, "뽑을 카드가 없습니다.");
                return;
            }

            // 드로우는 메인 행동 — 끝나면 턴 종료 (03-turn.md 3절). 카드 효과 드로우는 DrawOne 을 직접 써서 턴을 끝내지 않는다.
            SetTurn(GetNextActor(CurrentTurnActor));
        }

        /// <summary>카드 사용 · 버림으로 손패에서 카드 1장이 빠졌음을 알린다. 카드 효과 자체는 다루지 않는다 — 그건 CardPlayManager 가 별도로 처리한다.</summary>
        public void Discard(int requester, int cardId)
        {
            if (requester != CurrentTurnActor)
            {
                _outbox.Reject(requester, "내 턴이 아닙니다.");
                return;
            }

            // 소유 검증: 요청자 손패에 그 종류의 카드가 있어야 한다 (09-network.md 4.1).
            // 요청이 아직 종류 ID 라 같은 종류 중 한 장을 꺼낸다 — 인스턴스 ID 요청은 기능 4 에서.
            List<CardInstance> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.CardId == cardId);
            if (index < 0)
            {
                _outbox.Reject(requester, "손패에 없는 카드입니다.");
                return;
            }

            CardInstance card = hand[index];
            hand.RemoveAt(index);
            _discardPile.Add(card);
            _outbox.SetPlayerState(requester, PlayerProps.HandCount, hand.Count);
        }

        // 덱 맨 위 1장을 actor 손패로. 모든 뽑기(시작 배분 · 뽑기 요청 · 카드 효과)가 여기를 지난다.
        // 덱이 비면 버림 더미로 다시 채운다 (05-deck.md 5절). 덱 · 버림 더미가 모두 비면 false.
        private bool DrawOne(int actor)
        {
            if (_deck.Count == 0)
                RefillFromDiscardPile();

            if (_deck.Count == 0)
                return false;

            CardInstance card = _deck[0];
            _deck.RemoveAt(0);

            List<CardInstance> hand = _players[actor].Hand;
            hand.Add(card);

            _outbox.SendDrawnCard(actor, card.InstanceId, card.CardId);
            _outbox.SetPlayerState(actor, PlayerProps.HandCount, hand.Count);
            _outbox.SetRoomState(RoomProps.DeckCount, _deck.Count);
            return true;
        }

        // 버림 더미를 섞어 새 덱으로. 화면이 섞는 연출을 하도록 따로 알린다.
        private void RefillFromDiscardPile()
        {
            if (_discardPile.Count == 0) return;

            _deck.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_deck);

            _outbox.SendDeckRefilled(_deck.Count);
        }
    }
}
