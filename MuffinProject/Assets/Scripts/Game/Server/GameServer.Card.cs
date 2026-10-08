using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Game.Cards;

namespace Chapchu.Game
{
    // GameServer.Card.cs — 카드 규칙 원본과 카드 사용 처리 (04-card.md)
    public partial class GameServer
    {
        private readonly Dictionary<int, Card> _kinds = new Dictionary<int, Card>(); // CardId → 종류 템플릿 (InstanceId 0)

        /// <summary>게임 시작 1회: 이번 게임에 쓰는 카드 종류(템플릿)를 받는다. InitDeck 보다 먼저 부른다.</summary>
        public void InitCards(IEnumerable<Card> kinds)
        {
            _kinds.Clear();
            foreach (Card kind in kinds)
                _kinds[kind.CardId] = kind;
        }

        /// <summary>
        /// 행동 카드 사용 (04-card.md 5절). 검사 → 손패에서 버림 더미로 → 효과 → 알림 → 턴 넘김.
        /// 반응 5초 · 카운터는 기능 5 — 지금은 바로 처리한다.
        /// </summary>
        public void PlayCard(int requester, int cardInstanceId, int[] requestedTargets)
        {
            if (requester != CurrentTurnActor)
            {
                _outbox.Reject(requester, RejectCode.NotYourTurn);
                return;
            }

            List<Card> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
            {
                _outbox.Reject(requester, RejectCode.NotInHand);
                return;
            }

            Card card = hand[index];
            if (!card.IsAction)
            {
                _outbox.Reject(requester, RejectCode.NotActionCard);
                return;
            }

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets);
            if (targets == null)
            {
                _outbox.Reject(requester, RejectCode.InvalidTarget);
                return;
            }

            // 적용 — 검사가 모두 끝난 뒤에만 상태를 바꾼다
            hand.RemoveAt(index);
            _discardPile.Add(card);

            if (card.Damage > 0)
            {
                foreach (int target in targets)
                    SetHp(target, _players[target].Hp - card.Damage);
            }

            // 내보내기
            _outbox.SendCardUsed(requester, card.InstanceId, card.CardId, targets);
            _outbox.SetPlayerState(requester, PlayerProps.HandCount, hand.Count);
            _outbox.SetRoomState(RoomProps.DiscardCount, _discardPile.Count);

            // 행동 카드 사용은 메인 행동 — 끝나면 턴 종료 (03-turn.md 3절)
            AdvanceTurn();
        }

        // 대상 타입에 맞게 대상을 정한다. 자동 타입은 서버가 정하고, 고르는 타입은 요청을 검사한다. 맞지 않으면 null.
        // 살아 있는 사람 = 턴 순서에 있는 사람 (생존 상태는 기능 7).
        private int[] ResolveTargets(int requester, TargetType type, int[] requested)
        {
            int[] enemies = _turnOrder.Where(actor => actor != requester).ToArray();

            switch (type)
            {
                case TargetType.None:        return new int[0];
                case TargetType.Me:          return new[] { requester };
                case TargetType.AllEnemies:  return enemies;
                case TargetType.AllPlayers:  return _turnOrder.ToArray();
                case TargetType.SingleEnemy: return PickEnemies(requested, 1, enemies);
                case TargetType.TwoEnemy:    return PickEnemies(requested, 2, enemies);
                default:                     return null;
            }
        }

        // 고른 대상이 정확히 count 명이고, 서로 다르고, 모두 상대인지 본다.
        private static int[] PickEnemies(int[] requested, int count, int[] enemies)
        {
            if (requested == null || requested.Length != count) return null;
            if (requested.Distinct().Count() != count) return null;
            if (!requested.All(enemies.Contains)) return null;

            return requested;
        }
    }
}
