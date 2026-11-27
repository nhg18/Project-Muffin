using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.After.cs — 「~한 후」 함정의 조건이 되는 사건 기록 (04-card.md 6절 「발동 시점」).
    // 처리(체인 · 턴 뽑기 등)를 하면서 쌓이고, 끝나면 그 줄이 가져간다(TakeAfterRecords) — 함정 한 장의 효과가 만든 사건은 그 함정 뒤의 연쇄 줄이 따로 가져간다 (04 6절, 2026-11-27).
    // 사건 한 번에 함정 한 장당 한 번 — 같은 사건에 맞는 함정은 모두 발동한다 (2026-11-18).
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
            ActionReceived, // Source 가 쓴 행동 카드(CardInstanceId)의 효과를 Subject 가 받았다 — 반사로 다시 쓰인 카드면 Source = 반사한 사람
            HandReachedTen, // Subject 의 손패가 이 처리에서 10장 미만 → 정확히 10장이 됐다 (Source 없음 — 누가 만들었는지 묻지 않는다, 3004)
            Blocked         // Source 의 카드(행동 · 함정)가 Subject 에게 막는 상태를 걸었다. Amount = 걸린 종류 (BlockKind)
        }

        internal sealed class AfterRecord
        {
            public AfterKind Kind;
            public int Subject;
            public int Source;
            public int Amount;
            public int CardInstanceId = -1; // ActionReceived — 그 행동 카드
        }

        // 「막는 카드에 당한 후」 의 걸린 종류 (3012 편안 — 방금 걸린 종류는 풀지 않는다)
        internal const int BlockKindSkipTurn = 1;
        internal const int BlockKindAction = 2;

        private readonly List<AfterRecord> _afterRecords = new List<AfterRecord>();

        // 지금까지 쌓인 사건을 가져가고 비운다 — 함정 줄은 줄마다 자기 사건만 본다 (04 6절)
        private List<AfterRecord> TakeAfterRecords()
        {
            var records = new List<AfterRecord>(_afterRecords);
            _afterRecords.Clear();
            return records;
        }

        private void RecordAfter(AfterKind kind, int subject, int source, int amount, int cardInstanceId = -1)
        {
            _afterRecords.Add(new AfterRecord { Kind = kind, Subject = subject, Source = source, Amount = amount, CardInstanceId = cardInstanceId });
        }

        // 피해 — 준 사람이 없는 피해(빈 덱 · 피폭 · 대가, source -1)도 적는다: 「큰 피해 받은 후」(3009)는 준 사람을 묻지 않는다.
        // 「피해 받은 후」 는 FindAfterRecord 가 준 사람이 다른 사람인 것만 본다
        private void RecordDamage(int victim, int source, int amount)
        {
            RecordAfter(AfterKind.Damaged, victim, source, amount);
        }

        // 처리된(무효되지 않은) 행동 카드 — 효과를 받는 다른 사람마다 「행동 카드에 당함」 (3002 멈춰 · 3007 두고 보자). 전체 대상 카드도 (v3.4).
        // 본인 무효(Excluded)한 사람은 받지 않았다. 반사로 다시 쓰인 카드는 entry.User 가 반사한 사람이다 (RecastOf, 사용자 2026-11-27)
        private void RecordActionReceived(ChainEntry entry, int[] receivers)
        {
            if (entry.Card.Type != CardType.Action)
                return;

            foreach (int actor in receivers)
                if (actor != entry.User)
                    RecordAfter(AfterKind.ActionReceived, actor, entry.User, 1, entry.Card.InstanceId);
        }

        /// <summary>막는 상태(kind)가 걸렸다 (3012 편안). 행동 카드 · 함정 모두 — 함정이 건 것도 연쇄 줄에서 조건이 된다 (04 6절, 2026-11-27).</summary>
        internal void RecordBlocked(int actor, ChainEntry entry, int kind)
        {
            if (entry.User != actor)
                RecordAfter(AfterKind.Blocked, actor, entry.User, kind);
        }

        // ── 손패 10장 (3004 사이렌) ──
        // 처리 전 장수(_handBefore — 처리를 시작할 때 MarkHandsBefore)와 처리 뒤 장수를 비교한다 — 처리 중에 지나친 10장은 사건이 아니다 (04 6절).
        // 함정 한 장의 처리는 그 함정 직전 장수와 비교한다 (FireTraps). 턴 뽑기 · 함정의 뽑기에도 (11-card-list 1-4 「3004」)
        private readonly Dictionary<int, int> _handBefore = new Dictionary<int, int>();

        private void MarkHandsBefore()
        {
            _handBefore.Clear();
            foreach (KeyValuePair<int, PlayerState> pair in _players)
                _handBefore[pair.Key] = pair.Value.Hand.Count;
        }

        private void RecordHandReachedTen(IReadOnlyDictionary<int, int> before)
        {
            foreach (int actor in _turnOrder)
            {
                if (!IsInGame(actor) || _players[actor].Hand.Count != ChapchuHandCount)
                    continue;
                if (before.TryGetValue(actor, out int count) && count >= ChapchuHandCount)
                    continue; // 원래 10장 이상 — 「10장 미만 → 10장」 이 아니다
                RecordAfter(AfterKind.HandReachedTen, actor, -1, ChapchuHandCount);
            }
        }

        /// <summary>
        /// actor 의 「~한 후」 함정 trap 이 지금 쓸 수 있는 사건 기록. 없으면 null.
        /// records = 이 줄이 보는 사건. taken = 이 줄에서 앞 함정이 이미 쓴 사건. 맞는 사건이 여럿이면 아직 안 쓰인 것부터 주고, 모두 쓰였으면 가장 최근 것을 같이 쓴다 —
        /// 같은 사건에 맞는 함정은 모두 발동한다 (04-card.md 6절 「여러 함정 동시 발동」).
        /// </summary>
        internal AfterRecord FindAfterRecord(IReadOnlyList<AfterRecord> records, int actor, Card trap, ICollection<AfterRecord> taken)
        {
            AfterRecord shared = null; // 맞지만 앞 함정이 이미 쓴 사건 중 가장 최근 것
            for (int i = records.Count - 1; i >= 0; i--) // 가장 최근 사건부터
            {
                AfterRecord r = records[i];
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
                    TrapTrigger.AfterTargetedByOther => r.Kind == AfterKind.ActionReceived && r.Subject == actor && byOther,
                    TrapTrigger.AfterHitByAction => r.Kind == AfterKind.ActionReceived && r.Subject == actor && byOther && !IsClaimed(r.CardInstanceId),
                    TrapTrigger.AfterHandReachedTen => r.Kind == AfterKind.HandReachedTen && r.Subject != actor, // 누가 만들었는지 묻지 않는다 (v3.4)
                    TrapTrigger.AfterBigDamage => r.Kind == AfterKind.Damaged && r.Subject != actor && r.Amount >= System.Math.Max(1, trap.TriggerValue), // 준 사람이 없어도 · 내가 줬어도 (v3.4)
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
        /// 대상이 「발동시킨 사람」 이면 그 사람이 아직 게임에 · 10장이 된 사람은 아직 10장 · 가져올 카드는 아직 아무도 안 잡았고 버림 더미에 (11-card-list 1-4 표).
        /// </summary>
        private bool StillHolds(Card trap, AfterRecord r)
        {
            if (r == null)
                return true;

            if (trap.Target == TargetType.TriggerSource && !IsInGame(TriggerTargetOf(r, trap)))
                return false;

            switch (trap.Trigger)
            {
                case TrapTrigger.AfterHandReachedTen: return _players[r.Subject].Hand.Count == ChapchuHandCount;
                case TrapTrigger.AfterHitByAction:    return !IsClaimed(r.CardInstanceId) && _discardPile.Any(c => c.InstanceId == r.CardInstanceId);
                default:                              return true;
            }
        }

        private static bool IsAfterTrigger(TrapTrigger trigger)
        {
            return trigger == TrapTrigger.AfterDamaged || trigger >= TrapTrigger.AfterStolen;
        }
    }
}
