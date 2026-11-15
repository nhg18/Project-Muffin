using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.After.cs — 「~한 후」 함정의 조건이 되는 사건 기록 (04-card.md 6절 「발동 시점」).
    // 체인을 처리하면서(턴 스킵은 스킵할 때) 쌓이고, 함정 시간이 열리지 않거나 끝나면 비운다. 사건 한 번에 함정 한 번 — 발동하면 그 기록은 쓴 것이 된다.
    public partial class GameServer
    {
        internal enum AfterKind
        {
            Damaged,       // Subject 가 Source 에게 피해를 받았다
            Stolen,        // Subject 의 손패를 Source 가 가져갔다
            Discarded,     // Source 의 효과로 Subject 의 손패 Amount 장이 버려졌다
            TrapDestroyed, // Source 가 Subject 의 함정을 파괴했다
            MyTrapNegated, // Subject 가 낸 함정을 Source 의 카운터가 무효화했다
            CardReturned,  // Subject 가 낸 행동 카드가 Subject 손패로 반환됐다 (Source = 반환시킨 사람)
            DrewCards,     // Subject 가 카드 효과로 Amount 장을 뽑았다
            TurnSkipped,   // Subject 의 턴이 스킵됐다
            ChapchuGiven   // Source 가 Subject 에게 찹츄를 부여했다
        }

        internal sealed class AfterRecord
        {
            public AfterKind Kind;
            public int Subject;
            public int Source;
            public int Amount;
        }

        private readonly List<AfterRecord> _afterRecords = new List<AfterRecord>();

        private void RecordAfter(AfterKind kind, int subject, int source, int amount)
        {
            _afterRecords.Add(new AfterRecord { Kind = kind, Subject = subject, Source = source, Amount = amount });
        }

        // 빈 덱 피해(05 5절)처럼 사람이 준 것이 아닌 피해는 기록하지 않는다 — 「다른 플레이어에게」 받은 피해만
        private void RecordDamage(int victim, int source)
        {
            if (source >= 0 && source != victim)
                RecordAfter(AfterKind.Damaged, victim, source, 0);
        }

        /// <summary>actor 의 「~한 후」 함정 trap 이 지금 쓸 수 있는 사건 기록. 없으면 null.</summary>
        internal AfterRecord FindAfterRecord(int actor, Card trap)
        {
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
                    _ => false
                };

                if (match)
                    return r;
            }

            return null;
        }

        /// <summary>함정 대상 TriggerSource 가 될 사람 — 그 일을 한 사람. 카드 반환 · 여러 장 뽑기는 그 일을 겪은 사람 (04 6절).</summary>
        private static int TriggerTargetOf(AfterRecord r)
        {
            return r.Kind == AfterKind.CardReturned || r.Kind == AfterKind.DrewCards ? r.Subject : r.Source;
        }

        private static bool IsAfterTrigger(TrapTrigger trigger)
        {
            return trigger == TrapTrigger.AfterDamaged || trigger >= TrapTrigger.AfterStolen;
        }

        // 함정 시간을 열지 정한다 — 「~한 후」 조건이 맞는 함정을 가진 생존자가 있으면 opensAt 부터 5초 (04 6절). 없으면 기록을 비운다.
        private void OpenTrapWindowIfNeeded(double opensAt)
        {
            bool anyone = _turnOrder.Any(actor => IsInGame(actor) && _players[actor].Life == LifeState.Alive
                                                  && _players[actor].Traps.Any(t => IsAfterTrigger(t.Trigger) && FindAfterRecord(actor, t) != null));
            if (!anyone)
            {
                _afterRecords.Clear();
                return;
            }

            _trapWindowOpensAt = opensAt;
            _trapWindowDeadline = opensAt + TrapWindowTime;
            _events.Add(new TrapWindowStarted(_trapWindowOpensAt, _trapWindowDeadline));
        }

        // 아무도 내지 않고 함정 시간이 끝났다 — 그 기회는 사라진다.
        private void CloseTrapWindow()
        {
            _trapWindowDeadline = -1;
            _afterRecords.Clear();
        }
    }
}
