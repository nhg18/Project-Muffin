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

            if (IsTurnTimeUp(_clock()))
                return Reject(requester, RejectCode.NotYourTurn);

            List<Card> hand = _players[requester].Hand;
            int index = hand.FindIndex(c => c.InstanceId == cardInstanceId);
            if (index < 0)
                return Reject(requester, RejectCode.NotInHand);

            Card card = hand[index];
            if (card.Type != CardType.Action)
                return Reject(requester, RejectCode.NotActionCard);

            if (_players[requester].ActionBlocked)
                return Reject(requester, RejectCode.ActionBlocked); // 1007 짖기 — 이번 턴은 행동 카드만 막는다. 뽑기 · 함정 설치 · 카운터는 된다

            int[] targets = ResolveTargets(requester, card.Target, requestedTargets, condition: card.TargetCondition);
            if (targets == null || (targets.Length == 0 && card.Target != TargetType.None))
                return Reject(requester, RejectCode.InvalidTarget); // 대상이 필요한 카드인데 0명(찹츄 · 손패 조건) — 거절, 카드는 손패에 (04 5절 5 · 10절)

            // 적용 — 검증이 모두 끝난 뒤에만 상태를 바꾼다. 행동 카드 사용은 메인 행동 (03-turn.md 3절)
            _mainActionUsed = true;
            _players[requester].TimeoutStreak = 0; // 직접 턴을 끝냈다 (03-turn.md 4절)
            hand.RemoveAt(index);
            AddToChain(card, requester, targets);
            OnHandChanged(requester);
            return TakeEvents();
        }

        // 대상 타입에 맞게 대상을 정한다. 자동 타입은 서버가 정하고, 고르는 타입은 요청을 검사한다. 맞지 않으면 null.
        // 대상 후보 = 게임에 남은 사람. source = 발동시킨 사람(TriggerSource) — 없으면 -1.
        // condition = 고르는 타입의 대상 후보 조건 (11-card-list.md 1-2 「추가 사용 조건」) — 맞지 않는 사람을 고르면 null.
        // top = 카운터가 반응하는 맨 위 카드 — 대상이 그 카드에서 정해지는 카운터(2015 · 2018)가 쓴다.
        private int[] ResolveTargets(int requester, TargetType type, int[] requested, int source = -1, bool fromHand = true,
            TargetCondition condition = TargetCondition.None, ChainEntry top = null)
        {
            int[] inGame = _turnOrder.Where(IsInGame).ToArray();
            int[] others = inGame.Where(actor => actor != requester).ToArray();
            int[] pickable = (type == TargetType.OneAny ? inGame : others).Where(actor => MeetsTargetCondition(actor, condition)).ToArray();

            switch (type)
            {
                case TargetType.None:      return new int[0];
                case TargetType.Self:      return new[] { requester };
                case TargetType.AllOthers: return others;
                case TargetType.Everyone:  return inGame;
                case TargetType.OneOther:  return PickOthers(requested, 1, pickable);
                case TargetType.TwoOthers: return PickOthers(requested, 2, pickable);
                case TargetType.OneAny:    return PickOthers(requested, 1, pickable);
                case TargetType.TriggerSource: return source >= 0 && others.Contains(source) ? new[] { source } : null;
                case TargetType.ChapchuPlayers: return inGame.Where(a => _players[a].IsChapchu).ToArray();
                case TargetType.ChapchuOthers:  return others.Where(a => _players[a].IsChapchu).ToArray();
                case TargetType.MostHand:
                    // 나를 포함한 생존자 중 손패 최다 1명 — 낸 카드는 뺀 장수, 동률이면 무작위 (11-card-list 1018). 낼 때 정해 공개한다
                    System.Func<int, int> handAfter = a => _players[a].Hand.Count - (a == requester && fromHand ? 1 : 0);
                    int[] alive = inGame.Where(a => _players[a].Life == LifeState.Alive).ToArray();
                    if (alive.Length == 0)
                        return new int[0];
                    int most = alive.Max(handAfter);
                    int[] tied = alive.Where(a => handAfter(a) == most).ToArray();
                    return new[] { tied[_random.Next(tied.Length)] };
                case TargetType.NextPlayer:
                    // 턴 진행 방향으로 다음 차례 (1020 얼음!). 나밖에 없으면 대상 0명 — 거절
                    int nextActor = GetNextActor(requester);
                    return nextActor != requester ? new[] { nextActor } : new int[0];
                case TargetType.MoreHandThanMe:
                    // 이 카드를 낸 뒤의 내 장수와 비교 — 손패에서 낸 카드면 1장 줄어든 값 (04 11절)
                    int mine = _players[requester].Hand.Count - (fromHand ? 1 : 0);
                    return others.Where(a => _players[a].Hand.Count > mine).ToArray();
                case TargetType.SameCountAny:
                    // 아래 카드의 인원 수만큼, 살아 있는 누구나 — 대상 후보 조건은 걸지 않는다 (11-card-list 1-3 「2015」)
                    int picks = top == null ? 0 : PickCount(top.Card.Target);
                    return picks > 0 ? PickOthers(requested, picks, inGame) : null;
                case TargetType.OneNotDamaged:
                    return top == null ? null : PickOthers(requested, 1, ShareCandidates(requester, top));
                default:                   return null;
            }
        }

        /// <summary>고르는 대상 타입이 고르는 인원 수 — 고르지 않는 타입이면 0.</summary>
        internal static int PickCount(TargetType type) =>
            type == TargetType.OneOther || type == TargetType.OneAny ? 1 : type == TargetType.TwoOthers ? 2 : 0;

        /// <summary>
        /// 「피해 같이 받기」(2018 물귀신)로 me 가 고를 수 있는 사람 — 그 카드의 피해를 받고 있지 않은, 나를 뺀 살아 있는 사람.
        /// 조건부로 돌아오는 피해(1005 의 반동)는 받고 있는 피해가 아니다 — 그 카드를 쓴 사람도 고를 수 있다 (11-card-list 1-3).
        /// </summary>
        internal int[] ShareCandidates(int me, ChainEntry top)
        {
            int[] damaged = top.Card.Effects.Where(e => e.Tag == Effects.EffectTag.Damage && !e.IsRecoil).SelectMany(e => e.Affected(this, top)).ToArray();
            return _turnOrder.Where(actor => IsInGame(actor) && actor != me && !damaged.Contains(actor)).ToArray();
        }

        /// <summary>actor 가 이 대상 후보 조건에 맞는가 (11-card-list.md 1-2). 봇 · 시험이 고르기 전에 같은 판정을 쓴다.</summary>
        internal bool MeetsTargetCondition(int actor, TargetCondition condition)
        {
            PlayerState player = _players[actor];
            switch (condition)
            {
                case TargetCondition.HasHand:    return player.Hand.Count > 0;
                case TargetCondition.HpBelowMax: return player.Hp < MaxHp;
                case TargetCondition.HasTrap:    return player.Traps.Count > 0;
                default:                         return true;
            }
        }

        // 고른 대상이 정확히 count 명이고, 서로 다르고, 모두 후보(others) 안의 사람인지 본다.
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
