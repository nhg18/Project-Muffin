using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using NUnit.Framework;

namespace Chapchu.Game.Tests
{
    /// <summary>
    /// GameServer EditMode 테스트. Photon 없이 GameServer 만 만들어 규칙을 확인한다.
    /// 원본 상태는 private 이라 IServerOutbox 로 나간 결과만 본다 (UI 가 받는 것과 같다).
    /// </summary>
    public class GameServerTests
    {
        private const int A = 1;
        private const int B = 2;

        private FakeOutbox _outbox;
        private GameServer _server;

        [SetUp]
        public void SetUp()
        {
            _outbox = new FakeOutbox();
            _server = new GameServer(_outbox, () => 0);
        }

        // 카드 종류 ID 가 모두 다른 덱으로 A · B 2인 게임을 시작하고 5장씩 나눠 준다. 첫 턴은 무작위라 (턴 주인, 상대) 를 돌려준다.
        private (int current, int other) StartTwoPlayerGame(int deckSize)
        {
            _server.StartGame(new[] { A, B });
            _server.InitDeck(Enumerable.Range(100, deckSize).ToArray());
            _server.DealInitialHands(new[] { A, B });

            int current = _server.CurrentTurnActor;
            return (current, current == A ? B : A);
        }

        [Test]
        public void NewServer_HasNoTurnActor()
        {
            var server = new GameServer(null, () => 0);

            Assert.AreEqual(-1, server.CurrentTurnActor);
        }

        [Test]
        public void StartGame_RecordsMaxHpForEveryPlayer()
        {
            _server.StartGame(new[] { A, B });

            Assert.AreEqual(GameServer.MaxHp, _outbox.LastPlayerState(A, PlayerProps.Hp));
            Assert.AreEqual(GameServer.MaxHp, _outbox.LastPlayerState(B, PlayerProps.Hp));
        }

        [Test]
        public void DealInitialHands_GivesFiveUniqueCardsToEachPlayer()
        {
            StartTwoPlayerGame(20);

            Assert.AreEqual(5, _outbox.DrawnTo(A).Count);
            Assert.AreEqual(5, _outbox.DrawnTo(B).Count);
            Assert.AreEqual(10, _outbox.Drawn.Select(d => d.InstanceId).Distinct().Count());
            Assert.AreEqual(5, _outbox.LastPlayerState(A, PlayerProps.HandCount));
            Assert.AreEqual(5, _outbox.LastPlayerState(B, PlayerProps.HandCount));
            Assert.AreEqual(10, _outbox.LastRoomState(RoomProps.DeckCount));
        }

        [Test]
        public void Draw_AddsOneCardAndEndsTurn()
        {
            var (current, other) = StartTwoPlayerGame(20);

            _server.Draw(current);

            Assert.AreEqual(6, _outbox.DrawnTo(current).Count);
            Assert.AreEqual(6, _outbox.LastPlayerState(current, PlayerProps.HandCount));
            Assert.AreEqual(9, _outbox.LastRoomState(RoomProps.DeckCount));
            Assert.AreEqual(other, _server.CurrentTurnActor);
        }

        [Test]
        public void Draw_NotMyTurn_IsRejectedWithoutChange()
        {
            var (current, other) = StartTwoPlayerGame(20);

            _server.Draw(other);

            Assert.AreEqual(1, _outbox.RejectCount(other));
            Assert.AreEqual(5, _outbox.DrawnTo(other).Count);
            Assert.AreEqual(current, _server.CurrentTurnActor);
        }

        [Test]
        public void Discard_CardInHand_LowersHandCount()
        {
            var (current, _) = StartTwoPlayerGame(20);
            int cardId = _outbox.DrawnTo(current)[0].CardId;

            _server.Discard(current, cardId);

            Assert.AreEqual(0, _outbox.RejectCount(current));
            Assert.AreEqual(4, _outbox.LastPlayerState(current, PlayerProps.HandCount));
        }

        [Test]
        public void Discard_CardNotInHand_IsRejectedWithoutChange()
        {
            var (current, other) = StartTwoPlayerGame(20);
            int othersCardId = _outbox.DrawnTo(other)[0].CardId; // 종류 ID 가 모두 달라 내 손패에는 없다

            _server.Discard(current, othersCardId);

            Assert.AreEqual(1, _outbox.RejectCount(current));
            Assert.AreEqual(5, _outbox.LastPlayerState(current, PlayerProps.HandCount));
        }

        [Test]
        public void Discard_KeepsInstanceId_WhenRefilledIntoDeck()
        {
            var (current, other) = StartTwoPlayerGame(11); // 5 + 5 배분 → 덱 1장
            DrawnCard discarded = _outbox.DrawnTo(current)[0];

            _server.Discard(current, discarded.CardId);
            _server.Draw(current); // 덱 마지막 1장 → 턴은 상대
            _server.Draw(other); // 덱 0장 → 버림 더미(버린 1장) 회수 → 상대가 뽑는다

            DrawnCard refilled = _outbox.DrawnTo(other).Last();
            Assert.AreEqual(discarded.InstanceId, refilled.InstanceId);
            Assert.AreEqual(discarded.CardId, refilled.CardId);
        }

        private struct DrawnCard
        {
            public int Actor;
            public int InstanceId;
            public int CardId;
        }

        private class FakeOutbox : IServerOutbox
        {
            public readonly List<DrawnCard> Drawn = new List<DrawnCard>();
            private readonly List<(int actor, string key, object value)> _playerStates = new List<(int, string, object)>();
            private readonly List<(string key, object value)> _roomStates = new List<(string, object)>();
            private readonly List<int> _rejects = new List<int>();

            public void SetRoomState(string key, object value) => _roomStates.Add((key, value));
            public void SetPlayerState(int actorNumber, string key, object value) => _playerStates.Add((actorNumber, key, value));
            public void Reject(int actorNumber, string reason) => _rejects.Add(actorNumber);
            public void SendDrawnCard(int actorNumber, int cardInstanceId, int cardId) =>
                Drawn.Add(new DrawnCard { Actor = actorNumber, InstanceId = cardInstanceId, CardId = cardId });

            public List<DrawnCard> DrawnTo(int actor) => Drawn.Where(d => d.Actor == actor).ToList();
            public int RejectCount(int actor) => _rejects.Count(a => a == actor);
            public object LastPlayerState(int actor, string key) => _playerStates.Last(s => s.actor == actor && s.key == key).value;
            public object LastRoomState(string key) => _roomStates.Last(s => s.key == key).value;
        }
    }
}
