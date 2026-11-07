using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Card.cs — 카드 사용 처리 (04-card.md). 카드 한 장의 규칙값은 Card 가 들고 있다
    public partial class GameServer
    {
        /// <summary>
        /// 행동 카드 사용 (04-card.md 5절). 검증 → 손패에서 체인 맨 아래로 → 반응 시간. 효과와 턴 넘김은 체인을 처리한 뒤 (GameServer.Chain.cs).
        /// 요청은 인스턴스 ID 만 받는다 — 무슨 카드인지는 손패에서 찾은 장이 안다 (09-network.md 7절).
        /// </summary>
        public IReadOnlyList<GameEvent> PlayCard(int requester, int cardInstanceId, int[] requestedTargets)
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

            List<Card> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotInHand);

            Card card = hand[index];
            if (card.Type != CardType.Action)
                return Reject(requester, RejectCode.NotActionCard);

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets);
            if (targets == null)
                return Reject(requester, RejectCode.InvalidTarget);

            // 적용 — 검증이 모두 끝난 뒤에만 상태를 바꾼다. 행동 카드 사용은 메인 행동 (03-turn.md 3절)
            _mainActionUsed = true;
            hand.RemoveAt(index);
            AddToChain(card, requester, targets);
            OnHandChanged(requester);
            return TakeEvents();
        }

        // 대상 타입에 맞게 대상을 정한다. 자동 타입은 서버가 정하고, 고르는 타입은 요청을 검사한다. 맞지 않으면 null.
        // 대상 후보 = 게임에 남은 사람. source = 발동시킨 사람(TriggerSource) — 없으면 -1.
        private int[] ResolveTargets(int requester, TargetType type, int[] requested, int source = -1, bool fromHand = true)
        {
            int[] inGame = _turnOrder.Where(IsInGame).ToArray();
            int[] others = inGame.Where(actor => actor != requester).ToArray();

            switch (type)
            {
                case TargetType.None:      return new int[0];
                case TargetType.Self:      return new[] { requester };
                case TargetType.AllOthers: return others;
                case TargetType.Everyone:  return inGame;
                case TargetType.OneOther:  return PickOthers(requested, 1, others);
                case TargetType.TwoOthers: return PickOthers(requested, 2, others);
                case TargetType.TriggerSource: return source >= 0 && others.Contains(source) ? new[] { source } : null;
                case TargetType.ChapchuPlayers: return inGame.Where(a => _players[a].IsChapchu).ToArray();
                case TargetType.ChapchuOthers:  return others.Where(a => _players[a].IsChapchu).ToArray();
                case TargetType.MoreHandThanMe:
                    // 이 카드를 낸 뒤의 내 장수와 비교 — 손패에서 낸 카드면 1장 줄어든 값 (04 11절)
                    int mine = _players[requester].Hand.Count - (fromHand ? 1 : 0);
                    return others.Where(a => _players[a].Hand.Count > mine).ToArray();
                default:                   return null;
            }
        }

        // 고른 대상이 정확히 count 명이고, 서로 다르고, 모두 나를 뺀 사람인지 본다.
        private static int[] PickOthers(int[] requested, int count, int[] others)
        {
            if (requested == null || requested.Length != count)
                return null;

            if (requested.Distinct().Count() != count)
                return null;

            if (!requested.All(others.Contains))
                return null;

            return requested;
        }
    }
}
