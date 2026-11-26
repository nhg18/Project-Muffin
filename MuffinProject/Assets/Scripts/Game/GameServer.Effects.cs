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
        internal void ApplyDamage(ChainEntry entry, int actor, int amount) =>
            ApplyDamage(entry, actor, Scaled(entry, amount), entry.User, true); // 2배가 먼저, 감소는 그 다음 (04 7절)

        // by = 피해를 준 사람. direct = 이 카드가 직접 준 피해 — 같이 받는 피해(2018)는 「쓰러뜨림」(1005)에 넣지 않는다.
        private void ApplyDamage(ChainEntry entry, int actor, int amount, int by, bool direct)
        {
            if (entry.DamageToHeal.Contains(actor))
            {
                ApplyHealRaw(actor, amount);
                return;
            }

            if (entry.DamageReduction.TryGetValue(actor, out int reduce))
                amount -= reduce;
            amount -= entry.DamageReductionAll; // 받는 사람 모두 (2001)

            if (amount <= 0)
                return; // 최종 피해 0 이면 사건 자체가 없다 (06 4절)

            int hp = _players[actor].Hp - amount;
            if (hp <= 0 && entry.SurviveAt.TryGetValue(actor, out int keep))
                hp = Math.Max(1, keep);

            CountDamage(actor, by, -SetHp(actor, hp, by));
            if (hp <= 0 && direct)
                entry.KnockedDown.Add(actor);
            if (hp > 0 && _players[actor].Life == LifeState.DeathPending)
            {
                _players[actor].Life = LifeState.Alive; // 사망 방지(C11)로 HP 1 이상 — 바로 생존 (06 3 · 8절)
                _events.Add(new DeathAvoided(actor, _players[actor].Hp));
                _handDirty.Add(actor); // 살아났다 — 지금 10장이면 찹츄 선언 (07 4.1, PM 2026-11-19)
            }
            RecordAfter(AfterKind.Damaged, actor, by, amount);

            // 피해 같이 받기 (2018) — 내가 실제로 받은 양만큼 물귀신의 대상도. 물귀신에 걸린 표시(감소 등)는 그 카드 기준으로 본다
            if (entry.DamageSharers.TryGetValue(actor, out ChainEntry share))
            {
                foreach (int other in ReceiversOf(EffectSubject.Target, share))
                    ApplyDamage(share, other, amount, by, false);
            }
        }

        /// <summary>entry 의 회복 1건. 회복 → 피해 전환(C09) 이면 같은 수치 피해. 사망 대기 · 사망한 사람은 일반 회복을 받지 않는다 (06 6절).</summary>
        internal void ApplyHeal(ChainEntry entry, int actor, int amount)
        {
            amount = Scaled(entry, amount);
            if (entry.HealToDamage.Contains(actor))
            {
                if (amount > 0)
                {
                    CountDamage(actor, entry.User, -SetHp(actor, _players[actor].Hp - amount, entry.User)); // 피해의 주인 = 회복 카드를 낸 사람
                    if (_players[actor].Hp <= 0)
                        entry.KnockedDown.Add(actor);
                    RecordAfter(AfterKind.Damaged, actor, entry.User, amount);
                }
                return;
            }

            ApplyHealRaw(actor, amount);
        }

        /// <summary>entry 의 효과를 다 적용한 직후의 회복 (2001 침착). 그 전에 HP 가 0 이 된 사람은 회복하지 않는다 — 그대로 사망 대기로 간다 (사용자 2026-11-21, 04 7절).</summary>
        internal void ApplyHealAfter(ChainEntry entry)
        {
            foreach (KeyValuePair<int, int> pair in entry.HealAfter)
                if (IsInGame(pair.Key) && _players[pair.Key].Hp > 0)
                    ApplyHealRaw(pair.Key, pair.Value);
        }

        private void ApplyHealRaw(int actor, int amount)
        {
            PlayerState player = _players[actor];
            if (amount <= 0 || player.Life != LifeState.Alive || player.Hp <= 0)
                return; // HP 0 에서 일반 회복은 불발 — 사망 대기 등록 전(같은 체인)도 같다 (06 15절)

            int hp = Math.Min(MaxHp, player.Hp + amount);
            if (hp != player.Hp)
                AddStat(actor, StatKind.Healed, SetHp(actor, hp)); // 실제 회복량 0 이면 사건 없음 (06 6절)
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
                _handDirty.Add(actor); // 살아났다 — 지금 10장이면 찹츄 선언 (07 4.1, PM 2026-11-19)
                if (!_turnOrder.Any(a => _players[a].Life == LifeState.DeathPending))
                    _deathDeadline = -1; // 남은 사망 대기자가 없다 — 게임을 멈추지 않는다 (06 8절)
            }
        }

        // ── 손패 ──

        /// <summary>count 장 뽑는다 (빈 덱이면 장마다 피해, 05 5절). 카드 효과의 드로우는 「여러 장 뽑은 후」(T14) 기록을 남긴다.</summary>
        internal void DrawCards(int actor, int count, ChainEntry entry)
        {
            if (count <= 0 || !IsInGame(actor))
                return;

            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (!DrawOne(actor))
                    continue;
                drawn++;
                if (entry != null)
                    AddStat(actor, StatKind.Drawn); // 다시 받기(113 — entry 없음)는 뽑기로 세지 않는다
            }

            if (entry != null && drawn > 0)
                RecordAfter(AfterKind.DrewCards, actor, entry.User, drawn); // 실제로 뽑은 장수 — 빈 덱 피해는 뽑기가 아니다 (04 6절)
        }

        /// <summary>손패에서 무작위 count 장을 버림 더미로 (05 7절). 다른 사람의 효과면 「버려진 후」(T09) 기록.</summary>
        internal void DiscardRandom(int actor, int count, int source)
        {
            DiscardCards(actor, PickRandom(_players[actor].Hand, count), source);
        }

        /// <summary>손패의 그 한 장을 버림 더미로 — 본인이 고른 대가 (2013 · 2021). 손패에 없으면 아무 일도 없다.</summary>
        internal void DiscardFromHand(int actor, int cardInstanceId)
        {
            if (!InGame(actor))
                return;
            Card card = _players[actor].Hand.Find(c => c.InstanceId == cardInstanceId);
            if (card != null)
                DiscardCards(actor, new List<Card> { card }, actor);
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

        /// <summary>from 의 손패에서 무작위 count 장을 to 에게 (A04 · T08). source = 카드를 쓴 사람 — 남이면 「훔쳐진 후」(T08) 기록 (04 6절 「내가 만든 일은 조건이 되지 않는다」).</summary>
        internal void StealRandom(int from, int to, int count, int source)
        {
            if (from == to || !IsInGame(to))
                return;

            List<Card> cards = PickRandom(_players[from].Hand, count);
            if (cards.Count == 0)
                return;

            MoveHandCards(from, to, cards);
            AddStat(to, StatKind.Stolen, cards.Count);
            AddStat(from, StatKind.Lost, cards.Count);
            if (source >= 0 && source != from)
                RecordAfter(AfterKind.Stolen, from, source, cards.Count);
        }

        /// <summary>
        /// 손패를 전부 버리고 시작 손패 장수만큼 새로 뽑는다 (1013 기억 삭제). 교환 · 재분배라 「버려진 후」 · 「여러 장 뽑은 후」 기록을 남기지 않는다
        /// — 훔침 · 버림에 반응하는 카드의 대상이 아니다 (11-card-list 1013).
        /// </summary>
        internal void RedealHand(int actor, int count)
        {
            if (!IsInGame(actor))
                return;

            DiscardCards(actor, _players[actor].Hand.ToList(), -1);
            DrawCards(actor, count, null);
        }

        /// <summary>
        /// 버림 더미 맨 위 1장을 손패로 (1009 아 맞다!). 체인의 카드는 체인이 끝난 뒤 쌓이므로 체인이 시작되기 전의 맨 위다 (04-card.md 9.1).
        /// 가져올 카드가 없으면 불발.
        /// </summary>
        internal void TakeDiscardTop(int actor, ChainEntry entry)
        {
            if (!IsInGame(actor))
                return;

            int index = _discardPile.Count - 1;
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
        /// 대상 손패에서 나머지 모두가 1장씩 가져간다 (1018 무야호). 받는 순서는 쓴 사람부터 턴 진행 방향 — 카드가 떨어지면 거기서 끝 (11-card-list 1018).
        /// 가져가기는 훔침이라 「훔쳐진 후」 기록이 남는다 — 한 사람은 카드를 쓴 사람, 대상이 쓴 사람 자신이면 남지 않는다.
        /// </summary>
        internal void ShareFromTarget(int target, ChainEntry entry)
        {
            foreach (int receiver in TurnOrderFrom(entry.User).Where(a => a != target && IsInGame(a)).ToList())
            {
                if (_players[target].Hand.Count == 0)
                    break;
                StealRandom(target, receiver, Scaled(entry, 1), entry.User);
            }
        }

        /// <summary>두 사람의 HP 를 서로 바꾼다 (1011 운명 뒤집기). HP 설정이라 피해 · 회복이 아니다 — 감소 · 반사 · 함정이 걸리지 않는다.</summary>
        internal void SwapHp(int a, int b)
        {
            if (a == b || !IsInGame(a) || !IsInGame(b))
                return;

            int hpA = _players[a].Hp, hpB = _players[b].Hp;
            SetHp(a, hpB);
            SetHp(b, hpA);
        }

        /// <summary>min ~ max 사이 무작위 정수 (둘 다 포함) — 효과가 낼 때 정하는 값 (1016).</summary>
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

        /// <summary>찹츄 부여 (A20 · T16). 손패 장수를 보지 않는다 — 부여된 찹츄도 그 사람의 턴이 시작될 때 판정한다 (07 7 · 8절, 2026-11-14). 다른 사람이 준 것이면 「찹츄 부여된 후」(T16) 기록.</summary>
        internal void GiveChapchu(int actor, int source)
        {
            PlayerState player = _players[actor];
            if (player.IsChapchu || player.Life != LifeState.Alive)
                return; // 바뀐 것이 없다 — 사건 없음 (04 6절)

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

        /// <summary>대상 변경 (C02 · C04 · 2015) — 대상 전체를 새 대상으로. toCaster 면 그 카드의 시전자, 아니면 이 카운터의 대상 모두. 남은 사람이 없으면 바꾸지 않는다.</summary>
        internal void RedirectBelow(ChainEntry entry, bool toCaster)
        {
            ChainEntry below = Below(entry);
            if (below == null)
                return;

            int[] targets = (toCaster ? new[] { below.User } : entry.Targets).Where(IsInGame).ToArray();
            if (targets.Length > 0)
                below.Targets = targets;
        }

        /// <summary>반사 (2002) — 반응한 카드를 무효로 하고, 그 카드가 처리될 차례에 이 카운터를 낸 사람이 다시 쓰게 표시한다 (RecastOf).</summary>
        internal void ReflectBelow(ChainEntry entry) => MarkBelow(entry, below =>
        {
            below.Negated = true;
            below.NegatedBy = entry.User;
            below.ReflectedBy = entry.User;
        });

        /// <summary>피해 같이 받기 (2018 물귀신) — 반응한 카드에 「이 카운터를 낸 사람이 받는 만큼 이 카운터의 대상도」 표시를 남긴다.</summary>
        internal void ShareDamageBelow(ChainEntry entry) => MarkBelow(entry, below => below.DamageSharers[entry.User] = entry);

        /// <summary>이 카운터를 낸 사람이 반응한 카드에서 받을 피해 (2배 뒤 · 감소 전 예측).</summary>
        internal int PredictDamageBelow(ChainEntry entry)
        {
            ChainEntry below = Below(entry);
            return below == null ? 0 : Scaled(below, below.Card.Effects.Sum(e => e.PredictDamage(this, below, entry.User)));
        }

        /// <summary>손패로 반환 — 반응한 카드를 무효로 하고, 처리 때 버림 더미 대신 정한 사람 손패로 (쓴 사람 2003 · 이 카운터를 낸 사람 2020).</summary>
        internal void ReturnBelowToHand(ChainEntry entry, ReturnReceiver to)
        {
            ChainEntry below = Below(entry);
            if (below == null)
                return;

            below.Negated = true;
            below.NegatedBy = entry.User;
            below.ReturnTo = to == ReturnReceiver.CounterUser ? entry.User : below.User;
        }

        /// <summary>
        /// 손패 − 내 몫 무효 (2019 안 놔!) — 반응한 카드의 손패 감소 효과에서 이 카운터를 낸 사람만 뺀다.
        /// 잃을 뻔한 장수는 지금(이 카운터를 처리할 때)의 손패로 세어 함께 남긴다.
        /// </summary>
        internal void BlockHandLossBelow(ChainEntry entry)
        {
            ChainEntry below = Below(entry);
            if (below == null || !IsInGame(entry.User) || below.HandLossBlocked.ContainsKey(entry.User))
                return;

            below.HandLossBlocked[entry.User] = PredictHandLoss(below, entry.User, _players[entry.User].Hand.Count);
        }

        /// <summary>이 카운터를 낸 사람이 반응한 카드에서 막은 손패 장수 (BlockHandLossBelow 가 센 값). 막지 않았으면 0.</summary>
        internal int BlockedHandLoss(ChainEntry entry)
        {
            ChainEntry below = Below(entry);
            return below != null && below.HandLossBlocked.TryGetValue(entry.User, out int count) ? count : 0;
        }

        /// <summary>actor 가 그 카드의 손패 감소 효과로 잃을 장수 (처리 전 예측) — 손패가 handCount 장일 때. 효과가 여럿이면 더하되 손패를 넘지 않는다.</summary>
        internal int PredictHandLoss(ChainEntry entry, int actor, int handCount) =>
            Math.Min(handCount, entry.Card.Effects.Sum(e => e.PredictHandLoss(this, entry, actor, handCount)));

        /// <summary>수치 2배 (2005 묻고 따블로 가) — 이 카운터가 무효로 한 카운터의 아래 카드(두 칸 아래)에 표시한다. 한 카드에 1회.</summary>
        internal void DoubleTwoBelow(ChainEntry entry)
        {
            ChainEntry target = Below(entry)?.Below;
            if (target != null)
                target.Doubled = true;
        }

        /// <summary>그 카드의 적힌 숫자 n 을 지금 읽을 값 — 2배 표시가 있으면 2n. 피해 · 회복은 ApplyDamage · ApplyHeal 이, 장수는 효과가 부른다.</summary>
        internal int Scaled(ChainEntry entry, int n) => entry != null && entry.Doubled ? n * 2 : n;

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
            List<Card> pool = source.ToList();
            Shuffle(pool);
            return pool.Take(Math.Max(0, Math.Min(count, pool.Count))).ToList();
        }

        private static int[] Ids(List<Card> cards) => cards.Select(c => c.InstanceId).ToArray();
        private static int[] CardIds(List<Card> cards) => cards.Select(c => c.CardId).ToArray();
    }
}
