using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.After.cs — 「~한 후」 함정의 조건이 되는 사건 기록 (04-card.md 6절 「발동 시점」).
    // 체인을 처리하면서(턴 스킵은 스킵할 때) 쌓이고, 그 뒤 함정을 발동하면(FireTraps) 비운다. 사건 한 번에 함정 한 장당 한 번 — 같은 사건에 맞는 함정은 모두 발동한다 (2026-11-18).
    public partial class GameServer
    {
        internal enum AfterKind
        {
            Damaged,       // Subject 가 Source 에게 피해를 받았다
            Stolen,        // Subject 의 손패를 Source 가 가져갔다
            Discarded,     // Source 의 효과로 Subject 의 손패 Amount 장이 버려졌다
            TrapDestroyed, // Source 가 Subject 의 함정을 파괴했다
            MyTrapNegated, // 쓰지 않는다 — 함정에는 카운터를 낼 수 없다 (04 6절, 2026-11-15). 순서를 지키려 남긴다
            CardReturned,  // Subject 가 낸 행동 카드가 Subject 손패로 반환됐다 (Source = 반환시킨 사람)
            DrewCards,     // Subject 가 카드 효과로 Amount 장을 뽑았다
            TurnSkipped,   // Subject 의 턴이 스킵됐다
            ChapchuGiven,  // Source 가 Subject 에게 찹츄를 부여했다
            ActionReceived, // Source 가 쓴 행동 카드(CardInstanceId)의 효과를 Subject 가 받았다. Amount 1 = 고르는 대상으로 지목했다
            HandReachedTen, // Subject 의 손패가 이 처리에서 정확히 10장이 됐다 (Source = 이 처리를 일으킨 사람)
            Blocked         // Source 의 카드가 Subject 에게 턴 스킵 · 행동 카드 금지를 걸었다
        }

        internal sealed class AfterRecord
        {
            public AfterKind Kind;
            public int Subject;
            public int Source;
            public int Amount;
            public int CardInstanceId = -1; // ActionReceived — 그 행동 카드
        }

        private readonly List<AfterRecord> _afterRecords = new List<AfterRecord>();

        private void RecordAfter(AfterKind kind, int subject, int source, int amount, int cardInstanceId = -1)
        {
            _afterRecords.Add(new AfterRecord { Kind = kind, Subject = subject, Source = source, Amount = amount, CardInstanceId = cardInstanceId });
        }

        // 빈 덱 피해(05 5절)처럼 사람이 준 것이 아닌 피해는 기록하지 않는다 — 「다른 플레이어에게」 받은 피해만
        private void RecordDamage(int victim, int source, int amount)
        {
            if (source >= 0 && source != victim)
                RecordAfter(AfterKind.Damaged, victim, source, amount);
        }

        // 처리된(무효되지 않은) 행동 카드 — 효과를 받는 다른 사람마다 「행동 카드에 당함」 (3002 멈춰 · 3007 두고 보자). 본인 무효(Excluded)한 사람은 받지 않았다
        private void RecordActionReceived(ChainEntry entry, int[] receivers)
        {
            if (entry.Card.Type != CardType.Action)
                return;

            int picked = PickCount(entry.Card.Target) > 0 ? 1 : 0; // 고르는 대상 — 전원 · 자동 대상은 「지목」 이 아니다 (3002)
            foreach (int actor in receivers)
                if (actor != entry.User)
                    RecordAfter(AfterKind.ActionReceived, actor, entry.User, picked, entry.Card.InstanceId);
        }

        /// <summary>턴 스킵 · 행동 카드 금지가 걸렸다 (3012 편안). 함정의 효과로 걸린 것은 함정을 부르지 않는다 — 함정 뒤 기록을 비운다 (FireTraps).</summary>
        internal void RecordBlocked(int actor, ChainEntry entry)
        {
            if (entry.User != actor)
                RecordAfter(AfterKind.Blocked, actor, entry.User, 1);
        }

        // ── 손패 10장 (3004 사이렌) ──
        // 처리(체인 · 턴 뽑기 등)가 끝날 때의 손패를 그 전에 끝난 처리 때의 손패와 비교한다 — 처리 중에 지나친 10장은 사건이 아니다 (04 6절 「체인 도중에는 상태가 바뀌지 않는다」).
        private readonly Dictionary<int, int> _settledHand = new Dictionary<int, int>(); // 지난 처리가 끝났을 때의 손패 장수
        private int _processActor = -1; // 지금 처리를 일으킨 사람 — 체인 맨 아래 카드를 쓴 사람 · 턴에 뽑은 사람. 없으면 -1

        private void RecordHandReachedTen()
        {
            foreach (int actor in _turnOrder)
            {
                if (!IsInGame(actor) || _players[actor].Hand.Count != ChapchuHandCount)
                    continue;
                if (_settledHand.TryGetValue(actor, out int before) && before == ChapchuHandCount)
                    continue; // 원래 10장 — 「된」 것이 아니다
                RecordAfter(AfterKind.HandReachedTen, actor, _processActor, ChapchuHandCount);
            }
        }

        private void SettleHands()
        {
            _settledHand.Clear();
            foreach (KeyValuePair<int, PlayerState> pair in _players)
                _settledHand[pair.Key] = pair.Value.Hand.Count;
            _processActor = -1;
        }

        /// <summary>
        /// actor 의 「~한 후」 함정 trap 이 지금 쓸 수 있는 사건 기록. 없으면 null.
        /// taken = 이 처리에서 앞 함정이 이미 쓴 사건. 맞는 사건이 여럿이면 아직 안 쓰인 것부터 주고, 모두 쓰였으면 가장 최근 것을 같이 쓴다 —
        /// 같은 사건에 맞는 함정은 모두 발동한다 (04-card.md 6절 「여러 함정 동시 발동」).
        /// </summary>
        internal AfterRecord FindAfterRecord(int actor, Card trap, ICollection<AfterRecord> taken)
        {
            AfterRecord shared = null; // 맞지만 앞 함정이 이미 쓴 사건 중 가장 최근 것
            for (int i = _afterRecords.Count - 1; i >= 0; i--) // 가장 최근 사건부터
            {
                AfterRecord r = _afterRecords[i];
                bool byOther = r.Source >= 0 && r.Source != actor;
                bool match = trap.Trigger switch
                {
                    TrapTrigger.AfterDamaged => r.Kind == AfterKind.Damaged && r.Subject == actor && byOther,
                    TrapTrigger.AfterStolen => r.Kind == AfterKind.Stolen && r.Subject == actor && byOther,
                    TrapTrigger.AfterDiscarded => r.Kind == AfterKind.Discarded && r.Subject == actor && byOther,
                    TrapTrigger.AfterTrapDestroyed => r.Kind == AfterKind.TrapDestroyed && r.Subject == actor && byOther,
                    TrapTrigger.AfterMyTrapNegated => r.Kind == AfterKind.MyTrapNegated && r.Subject == actor && byOther,
                    TrapTrigger.AfterCardReturned => r.Kind == AfterKind.CardReturned && r.Subject != actor,
                    TrapTrigger.AfterDrewMany => r.Kind == AfterKind.DrewCards && r.Subject != actor && r.Amount >= System.Math.Max(1, trap.TriggerValue),
                    TrapTrigger.AfterTurnSkipped => r.Kind == AfterKind.TurnSkipped && r.Subject == actor,
                    TrapTrigger.AfterChapchuGiven => r.Kind == AfterKind.ChapchuGiven && r.Subject == actor && byOther,
                    TrapTrigger.AfterTargetedByOther => r.Kind == AfterKind.ActionReceived && r.Subject == actor && byOther && r.Amount == 1,
                    TrapTrigger.AfterHitByAction => r.Kind == AfterKind.ActionReceived && r.Subject == actor && byOther,
                    TrapTrigger.AfterHandReachedTen => r.Kind == AfterKind.HandReachedTen && r.Subject != actor && byOther,
                    TrapTrigger.AfterBigDamage => r.Kind == AfterKind.Damaged && r.Subject != actor && byOther && r.Amount >= System.Math.Max(1, trap.TriggerValue),
                    TrapTrigger.AfterBlocked => r.Kind == AfterKind.Blocked && r.Subject == actor && byOther,
                    _ => false
                };

                if (!match)
                    continue;
                if (!taken.Contains(r))
                    return r;
                shared ??= r;
            }

            return shared;
        }

        /// <summary>
        /// 함정 대상 TriggerSource 가 될 사람 — 그 일을 한 사람. 카드 반환 · 여러 장 뽑기 · 10장이 됨 · 남이 받은 큰 피해는 그 일을 겪은 사람 (04 6절).
        /// </summary>
        private static int TriggerTargetOf(AfterRecord r, Card trap)
        {
            bool subject = r.Kind == AfterKind.CardReturned || r.Kind == AfterKind.DrewCards || r.Kind == AfterKind.HandReachedTen
                || trap.Trigger == TrapTrigger.AfterBigDamage;
            return subject ? r.Subject : r.Source;
        }

        /// <summary>
        /// 발동 직전에 조건을 다시 본다 — 앞 함정을 적용한 뒤 풀렸으면 발동하지 않고 슬롯에 남는다 (04 6절 「여러 함정 동시 발동」, 2026-11-18).
        /// 10장이 된 사람은 아직 10장 · 큰 피해를 받은 사람과 지목한 사람은 아직 게임에 · 가져올 카드는 아직 버림 더미에.
        /// </summary>
        private bool StillHolds(Card trap, AfterRecord r)
        {
            if (r == null)
                return true;

            switch (trap.Trigger)
            {
                case TrapTrigger.AfterHandReachedTen: return IsInGame(r.Subject) && _players[r.Subject].Hand.Count == ChapchuHandCount;
                case TrapTrigger.AfterBigDamage:      return IsInGame(r.Subject);
                case TrapTrigger.AfterTargetedByOther: return IsInGame(r.Source);
                case TrapTrigger.AfterHitByAction:    return _discardPile.Any(c => c.InstanceId == r.CardInstanceId);
                default:                              return true;
            }
        }

        private static bool IsAfterTrigger(TrapTrigger trigger)
        {
            return trigger == TrapTrigger.AfterDamaged || trigger >= TrapTrigger.AfterStolen;
        }
    }
}
