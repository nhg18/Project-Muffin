using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Effects.cs — 카드 효과(Effects/)가 부르는 상태 변경 (11-card-list.md 5절). 효과는 무엇을 할지 정하고, 바꾸는 일은 여기서만 한다.
    // 체인 표시(감소 · 전환 · 사망 방지 · 함정 보호, 04-card.md 7절)는 여기서 반영한다 — 06-health.md 4절 「모두 계산한 뒤 HP 에 한 번만」.
    public partial class GameServer
    {
        // ── 체력 ──

        /// <summary>entry 의 피해 1건. 피해 → 회복 전환(C03) · 감소(C10) · 사망 방지(C11)를 계산한 뒤 한 번만 반영한다.</summary>
        internal void ApplyDamage(ChainEntry entry, int actor, int amount)
        {
            if (entry.DamageToHeal.Contains(actor))
            {
                ApplyHealRaw(actor, amount);
                return;
            }

            if (entry.DamageReduction.TryGetValue(actor, out int reduce))
                amount -= reduce;

            if (amount <= 0)
                return; // 최종 피해 0 이면 사건 자체가 없다 (06 4절)

            int hp = _players[actor].Hp - amount;
            if (hp <= 0 && entry.SurviveAt.TryGetValue(actor, out int keep))
                hp = Math.Max(1, keep);

            SetHp(actor, hp);
            if (hp <= 0)
                entry.KnockedDown.Add(actor);
            if (hp > 0 && _players[actor].Life == LifeState.DeathPending)
            {
                _players[actor].Life = LifeState.Alive; // 사망 방지(C11)로 HP 1 이상 — 바로 생존 (06 3 · 8절)
                _events.Add(new DeathAvoided(actor, _players[actor].Hp));
            }
            RecordAfter(AfterKind.Damaged, actor, entry.User, amount);
        }

        /// <summary>entry 의 회복 1건. 회복 → 피해 전환(C09) 이면 같은 수치 피해. 사망 대기 · 사망한 사람은 일반 회복을 받지 않는다 (06 6절).</summary>
        internal void ApplyHeal(ChainEntry entry, int actor, int amount)
        {
            if (entry.HealToDamage.Contains(actor))
            {
                if (amount > 0)
                {
                    SetHp(actor, _players[actor].Hp - amount);
                    if (_players[actor].Hp <= 0)
                        entry.KnockedDown.Add(actor);
                    RecordAfter(AfterKind.Damaged, actor, entry.User, amount);
                }
                return;
            }

            ApplyHealRaw(actor, amount);
        }

        private void ApplyHealRaw(int actor, int amount)
        {
            PlayerState player = _players[actor];
            if (amount <= 0 || player.Life != LifeState.Alive)
                return;

            int hp = Math.Min(MaxHp, player.Hp + amount);
            if (hp != player.Hp)
                SetHp(actor, hp); // 실제 회복량 0 이면 사건 없음 (06 6절)
        }

        /// <summary>「HP N 으로 생존」 지금 (T17) — 손패 · 다른 설치 함정을 버리고 HP N, 사망 대기면 바로 생존으로 (06 6 · 8절).</summary>
        internal void SurviveNow(int actor, int hp, bool discardHandAndTraps)
        {
            PlayerState player = _players[actor];
            if (!IsInGame(actor))
                return; // 처리 중에 나갔다 — 불발 (06 10절)

            if (discardHandAndTraps)
            {
                DiscardCards(actor, player.Hand.ToList(), -1);
                foreach (Card trap in player.Traps.ToList())
                    DestroyTrap(actor, trap, null);
            }

            SetHp(actor, Math.Max(1, hp));
            if (player.Life == LifeState.DeathPending)
            {
                player.Life = LifeState.Alive;
                _events.Add(new DeathAvoided(actor, player.Hp));
            }
        }

        // ── 손패 ──

        /// <summary>count 장 뽑는다 (빈 덱이면 장마다 피해, 05 5절). 카드 효과의 드로우는 「여러 장 뽑은 후」(T14) 기록을 남긴다.</summary>
        internal void DrawCards(int actor, int count, ChainEntry entry)
        {
            if (count <= 0 || !IsInGame(actor))
                return;

            for (int i = 0; i < count; i++)
                DrawOne(actor);

            if (entry != null)
                RecordAfter(AfterKind.DrewCards, actor, entry.User, count);
        }

        /// <summary>손패에서 무작위 count 장을 버림 더미로 (05 7절). 다른 사람의 효과면 「버려진 후」(T09) 기록.</summary>
        internal void DiscardRandom(int actor, int count, int source)
        {
            DiscardCards(actor, PickRandom(_players[actor].Hand, count), source);
        }

        private void DiscardCards(int actor, List<Card> cards, int source)
        {
            if (cards.Count == 0)
                return;

            PlayerState player = _players[actor];
            foreach (Card card in cards)
                player.Hand.Remove(card);
            _discardPile.AddRange(cards);

            _events.Add(new CardsDiscarded(actor, cards.Select(c => c.InstanceId).ToArray(), cards.Select(c => c.CardId).ToArray(), player.Hand.Count, _discardPile.Count));
            OnHandChanged(actor);
            if (source >= 0 && source != actor)
                RecordAfter(AfterKind.Discarded, actor, source, cards.Count);
        }

        /// <summary>from 의 손패에서 무작위 count 장을 to 에게 (A04 · T08). 「훔쳐진 후」(T08) 기록.</summary>
        internal void StealRandom(int from, int to, int count)
        {
            if (from == to || !IsInGame(to))
                return;

            List<Card> cards = PickRandom(_players[from].Hand, count);
            if (cards.Count == 0)
                return;

            MoveHandCards(from, to, cards);
            RecordAfter(AfterKind.Stolen, from, to, cards.Count);
        }

        /// <summary>
        /// 손패를 전부 버리고 시작 손패 장수만큼 새로 뽑는다 (113 기억 삭제). 교환 · 재분배라 「버려진 후」 · 「여러 장 뽑은 후」 기록을 남기지 않는다
        /// — 훔침 · 버림에 반응하는 카드의 대상이 아니다 (11-card-list 113).
        /// </summary>
        internal void RedealHand(int actor)
        {
            if (!IsInGame(actor))
                return;

            DiscardCards(actor, _players[actor].Hand.ToList(), -1);
            DrawCards(actor, InitialHandCount, null);
        }

        /// <summary>
        /// 처리되는 시점의 버림 더미 맨 위 1장을 손패로 (109 아 맞다!). 처리 중인 이 카드 자신은 이미 더미에 있지만 가져올 수 없다 — 그 아래 장.
        /// 가져올 카드가 없으면 불발.
        /// </summary>
        internal void TakeDiscardTop(int actor, ChainEntry entry)
        {
            if (!IsInGame(actor))
                return;

            int index = _discardPile.Count - 1;
            if (index >= 0 && _discardPile[index] == entry.Card)
                index--;
            if (index < 0)
                return;

            Card card = _discardPile[index];
            _discardPile.RemoveAt(index);
            _players[actor].Hand.Add(card);
            int top = _discardPile.Count > 0 ? _discardPile[_discardPile.Count - 1].CardId : 0;
            _events.Add(new DiscardTaken(actor, card.InstanceId, card.CardId, _players[actor].Hand.Count, _discardPile.Count, top));
            OnHandChanged(actor);
        }

        /// <summary>
        /// 대상 손패에서 나머지 모두가 1장씩 가져간다 (118 무야호). 받는 순서는 쓴 사람부터 턴 진행 방향 — 카드가 떨어지면 거기서 끝 (11-card-list 118).
        /// 가져가기는 훔침이라 「훔쳐진 후」 기록이 남는다.
        /// </summary>
        internal void ShareFromTarget(int target, ChainEntry entry)
        {
            foreach (int receiver in TurnOrderFrom(entry.User).Where(a => a != target && IsInGame(a)).ToList())
            {
                if (_players[target].Hand.Count == 0)
                    break;
                StealRandom(target, receiver, 1);
            }
        }

        /// <summary>두 사람의 HP 를 서로 바꾼다 (111 운명 뒤집기). HP 설정이라 피해 · 회복이 아니다 — 감소 · 반사 · 함정이 걸리지 않는다.</summary>
        internal void SwapHp(int a, int b)
        {
            if (a == b || !IsInGame(a) || !IsInGame(b))
                return;

            int hpA = _players[a].Hp, hpB = _players[b].Hp;
            SetHp(a, hpB);
            SetHp(b, hpA);
        }

        /// <summary>min ~ max 사이 무작위 정수 (둘 다 포함) — 효과가 낼 때 정하는 값 (116).</summary>
        internal int RollRange(int min, int max) => _random.Next(min, max + 1);

        /// <summary>두 사람의 손패 전체를 바꾼다 (A11 · A18).</summary>
        internal void SwapHands(int a, int b)
        {
            if (a == b || !IsInGame(a) || !IsInGame(b))
                return;

            List<Card> handA = _players[a].Hand.ToList();
            List<Card> handB = _players[b].Hand.ToList();
            _players[a].Hand.Clear();
            _players[b].Hand.Clear();
            _players[a].Hand.AddRange(handB);
            _players[b].Hand.AddRange(handA);

            _events.Add(new HandCardsMoved(a, b, Ids(handA), CardIds(handA), _players[a].Hand.Count, _players[b].Hand.Count));
            _events.Add(new HandCardsMoved(b, a, Ids(handB), CardIds(handB), _players[b].Hand.Count, _players[a].Hand.Count));
            OnHandChanged(a);
            OnHandChanged(b);
        }

        private void MoveHandCards(int from, int to, List<Card> cards)
        {
            foreach (Card card in cards)
                _players[from].Hand.Remove(card);
            _players[to].Hand.AddRange(cards);

            _events.Add(new HandCardsMoved(from, to, Ids(cards), CardIds(cards), _players[from].Hand.Count, _players[to].Hand.Count));
            OnHandChanged(from);
            OnHandChanged(to);
        }

        // ── 함정 ──

        /// <summary>owner 의 설치 함정 무작위 count 장 파괴 (A03). 함정 보호(C12)면 파괴 대신 손패로. 「함정 파괴된 후」(T10) 기록.</summary>
        internal void DestroyRandomTraps(int owner, int count, ChainEntry entry)
        {
            foreach (Card trap in PickRandom(_players[owner].Traps, count))
            {
                if (entry.ProtectTraps.Contains(owner))
                    ReturnTrap(owner, trap);
                else
                    DestroyTrap(owner, trap, entry);
            }
        }

        private void DestroyTrap(int owner, Card trap, ChainEntry entry)
        {
            _players[owner].Traps.Remove(trap);
            _discardPile.Add(trap);
            _events.Add(new TrapDestroyed(owner, trap.InstanceId, trap.CardId, _players[owner].Traps.Count, _discardPile.Count));
            if (entry != null)
                RecordAfter(AfterKind.TrapDestroyed, owner, entry.User, 1);
        }

        /// <summary>owner 의 설치 함정 무작위 count 장을 owner 손패로 (A16).</summary>
        internal void ReturnRandomTraps(int owner, int count)
        {
            foreach (Card trap in PickRandom(_players[owner].Traps, count))
                ReturnTrap(owner, trap);
        }

        private void ReturnTrap(int owner, Card trap)
        {
            PlayerState player = _players[owner];
            player.Traps.Remove(trap);
            player.Hand.Add(trap);
            _events.Add(new TrapReturned(owner, trap.InstanceId, trap.CardId, player.Traps.Count, player.Hand.Count));
            OnHandChanged(owner);
        }

        /// <summary>from 의 설치 함정 무작위 1장을 to 의 빈 칸으로 (A17). 빈 칸이 없으면 불발.</summary>
        internal void StealRandomTrap(int from, int to)
        {
            if (from == to || !IsInGame(to) || _players[to].Traps.Count >= TrapSlotCount)
                return;

            Card trap = PickRandom(_players[from].Traps, 1).FirstOrDefault();
            if (trap == null)
                return;

            _players[from].Traps.Remove(trap);
            _players[to].Traps.Add(trap);
            _events.Add(new TrapMoved(from, to, trap.InstanceId, trap.CardId, _players[from].Traps.Count, _players[to].Traps.Count));
        }

        // ── 턴 ──

        /// <summary>다음 턴 스킵을 건다 — 겹쳐도 1회 (03-turn.md 7절).</summary>
        internal void MarkSkipTurn(int actor)
        {
            _players[actor].SkipNextTurn = true;
            _events.Add(new TurnSkipMarked(actor));
        }

        /// <summary>다음 실제 턴 시작에 count 장 더 뽑는다 (T15).</summary>
        internal void AddTurnStartDraws(int actor, int count)
        {
            if (count > 0)
                _players[actor].TurnStartDraws += count;
        }

        // ── 찹츄 ──

        /// <summary>찹츄 부여 (A20 · T16). 손패가 정확히 10장이 아니면 붙지 않는다 (07 7절). 다른 사람이 준 것이면 「찹츄 부여된 후」(T16) 기록.</summary>
        internal void GiveChapchu(int actor, int source)
        {
            PlayerState player = _players[actor];
            if (!player.IsChapchu && player.Life == LifeState.Alive && player.Hand.Count == ChapchuHandCount)
                SetChapchu(actor, true);

            if (source >= 0 && source != actor)
                RecordAfter(AfterKind.ChapchuGiven, actor, source, 1);
        }

        internal void ClearChapchu(int actor)
        {
            if (_players[actor].IsChapchu)
                SetChapchu(actor, false);
        }

        /// <summary>첫 사람의 찹츄를 지우고 둘째에게 부여 (A20). 첫 사람이 찹츄가 아니면 아무 일도 없다.</summary>
        internal void MoveChapchu(int from, int to, int source)
        {
            if (!_players[from].IsChapchu)
                return;

            SetChapchu(from, false);
            GiveChapchu(to, source);
        }

        // ── 아래 카드를 바꾸는 카운터 (04-card.md 7절) ──

        /// <summary>체인에서 entry 바로 아래 카드 — 카운터는 낼 때의 맨 위 카드에 반응한다. 맨 아래면 null.</summary>
        internal ChainEntry Below(ChainEntry entry) => entry.Below;

        /// <summary>반응한 카드(바로 아래)에 표시를 남긴다 — 그 카드가 처리될 때 반영된다 (04 7절).</summary>
        internal void MarkBelow(ChainEntry entry, Action<ChainEntry> mark)
        {
            ChainEntry below = Below(entry);
            if (below != null)
                mark(below);
        }

        /// <summary>대상 변경 (C02 · C04) — 대상 전체를 새 1명으로. toCaster 면 그 카드의 시전자, 아니면 이 카운터의 대상.</summary>
        internal void RedirectBelow(ChainEntry entry, bool toCaster)
        {
            ChainEntry below = Below(entry);
            if (below == null)
                return;

            int target = toCaster ? below.User : (entry.Targets.Length > 0 ? entry.Targets[0] : -1);
            if (target >= 0 && IsInGame(target))
                below.Targets = new[] { target };
        }

        /// <summary>손패로 반환 (C01) — 반응한 카드를 무효로 하고, 처리 때 버림 더미 대신 시전자 손패로.</summary>
        internal void ReturnBelowToHand(ChainEntry entry)
        {
            ChainEntry below = Below(entry);
            if (below == null)
                return;

            below.Negated = true;
            below.NegatedBy = entry.User;
            below.ReturnToCaster = true;
        }

        /// <summary>찹츄 이동(A20)에서 받을 사람이 빠졌을 때 — 지우는 쪽만 한다.</summary>
        internal void ClearChapchuIfMoved(int from) => ClearChapchu(from);

        /// <summary>그 카드에서 tag 의 일을 받는 사람 (처리 전 예측).</summary>
        internal IEnumerable<int> AffectedBy(ChainEntry entry, EffectTag tag)
        {
            return entry.Card.Effects.Where(e => e.Tag == tag).SelectMany(e => e.Affected(this, entry)).Distinct();
        }

        internal int PlayerHp(int actor) => _players[actor].Hp;
        internal int HandCountOf(int actor) => _players[actor].Hand.Count;
        internal int TrapCountOf(int actor) => _players[actor].Traps.Count;

        // ── 내부 ──

        private List<Card> PickRandom(List<Card> source, int count)
        {
            var pool = source.ToList();
            Shuffle(pool);
            return pool.Take(Math.Max(0, Math.Min(count, pool.Count))).ToList();
        }

        private static int[] Ids(List<Card> cards) => cards.Select(c => c.InstanceId).ToArray();
        private static int[] CardIds(List<Card> cards) => cards.Select(c => c.CardId).ToArray();
    }
}
