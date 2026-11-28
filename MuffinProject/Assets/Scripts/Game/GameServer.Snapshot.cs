using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Snapshot.cs — 방장 이주 (09-network.md 9.1): 상태 전체를 담기 · 되살리기 · 새 방장이 이어 가기.
    // 원본 상태 필드를 더하면 Capture · Restore 양쪽에 같은 자리에 더한다 — 빠뜨리면 rules 의 Migrate 시나리오가 잡는다 (필드를 리플렉션으로 견준다).
    public partial class GameServer
    {
        // 담는 순서가 바뀌면 올린다 — 다른 번호의 스냅샷은 되살리지 않는다 (앱 버전이 섞인 방)
        private const int SnapshotVersion = 3;

        /// <summary>
        /// 상태가 바뀔 때마다 는다 — 전송 층이 「예비 방장에게 다시 보낼 때」 를 아는 값. 거절만 일어난 요청 · 매 프레임 미루는 마감은 올리지 않는다
        /// (마감은 새 방장이 처음부터 다시 센다 — ResumeAfterMigration).
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>지금 상태 전체 — 손패 · 함정 · 덱 순서까지. 예비 방장에게만 보낸다 (09-network.md 9.1).</summary>
        public GameSnapshot Capture()
        {
            var w = new SnapshotWriter();
            w.Int(SnapshotVersion);

            // 사람
            w.Ints(_turnOrder);
            w.Int(_players.Count);
            foreach (KeyValuePair<int, PlayerState> pair in _players)
            {
                PlayerState p = pair.Value;
                w.Int(pair.Key);
                w.Ints(Ids(p.Hand));
                w.Ints(Ids(p.Traps));
                w.Int(p.Hp);
                w.Bool(p.HasLeft);
                w.Int((int)p.Life);
                w.Int(p.EmptyDraws);
                w.Bool(p.SkipNextTurn);
                w.Bool(p.BlockActionNextTurn);
                w.Bool(p.ActionBlocked);
                w.Int(p.TurnStartDraws);
                w.Int(p.TimeoutStreak);
                w.Bool(p.IsChapchu);
                w.Int(p.OutTurn);
                w.Bool(p.OutByLeaving);
                w.Int(p.DownedBy);
            }

            // 턴
            w.Int(CurrentTurnActor);
            w.Int(TurnSerial);
            w.Ints(_noTimeoutActors);
            w.Time(_turnDeadline);
            w.Bool(_mainActionUsed);
            w.Time(_nextTurnAt);
            w.Time(_resumeAt);
            w.Bool(_turnPaused);
            w.Time(_lastTickAt);
            w.Bool(_reversed);
            w.Int(_extraTurnActor);
            w.Int(_heldNext);
            w.Int(_turnTaker);

            // 체인 — 카드 한 장(ChainEntry)을 번호로 바꿔 적는다. 세 목록 · 고르기 · 「아래 카드」 가 같은 것을 가리킨다
            List<ChainEntry> entries = _chainOrder.Concat(_chain).Concat(_resolving).Distinct().ToList();
            if (_choiceEntry != null && !entries.Contains(_choiceEntry))
                entries.Add(_choiceEntry);
            for (int i = 0; i < entries.Count; i++) // 아래 카드가 목록 밖이면(이미 끝난 체인) 뒤에 더한다
            {
                if (entries[i].Below != null && !entries.Contains(entries[i].Below))
                    entries.Add(entries[i].Below);
                foreach (ChainEntry share in entries[i].DamageSharers.Values)
                    if (!entries.Contains(share))
                        entries.Add(share);
            }

            w.Int(entries.Count);
            foreach (ChainEntry e in entries)
            {
                w.Int(e.Card.InstanceId);
                w.Int(e.User);
                w.Ints(e.Targets);
                w.Int(e.TriggerAmount);
                w.Bool(e.Negated);
                w.Int(e.NegatedBy);
                w.Int(e.ReturnTo);
                w.Bool(e.Returned);
                w.Ints(e.Excluded);
                w.Ints(e.DamageToHeal);
                w.Ints(e.HealToDamage);
                w.Map(e.DamageReduction);
                w.Int(e.DamageReductionAll);
                w.Map(e.HealAfter);
                w.Map(e.HandLossBlocked);
                w.Bool(e.Doubled);
                w.Int(e.ReflectedBy);
                w.Map(e.DamageSharers.ToDictionary(pair => pair.Key, pair => entries.IndexOf(pair.Value))); // 물귀신 카드는 번호로
                w.Map(e.SurviveAt);
                w.Ints(e.ProtectTraps);
                w.Map(e.Rolled.ToDictionary(pair => IndexOfEffect(e.Card, pair.Key), pair => pair.Value)); // 효과는 그 카드의 몇 번째인지로
                w.Ints(e.KnockedDown);
                w.Int(e.Below == null ? -1 : entries.IndexOf(e.Below));
                w.Int(e.NextEffect);
            }
            w.Ints(_chain.Select(e => entries.IndexOf(e)).ToArray());
            w.Ints(_resolving.Select(e => entries.IndexOf(e)).ToArray());
            w.Ints(_chainOrder.Select(e => entries.IndexOf(e)).ToArray());

            // 반응 시간
            w.Time(_reactionOpensAt);
            w.Time(_reactionDeadline);
            w.Int(_reactionAsked.Count);
            foreach (KeyValuePair<int, int[]> pair in _reactionAsked)
            {
                w.Int(pair.Key);
                w.Ints(pair.Value);
            }
            w.Ints(_reactionSkipped);
            w.Ints(_reactionHeld);

            // 고르기 (1019)
            w.Int(_choiceActor);
            w.Int(_choiceEntry == null ? -1 : entries.IndexOf(_choiceEntry));
            w.Int(_choiceEntry == null ? -1 : IndexOfEffect(_choiceEntry.Card, _choiceEffect));
            w.Time(_choiceDeadline);
            w.Int(_choiceMore);

            // 덱
            w.Ints(Ids(_deck));
            w.Ints(Ids(_discardPile));
            w.Ints(Ids(_allCards));

            // 개판 · 대폭발
            w.Int(_gaepanCaster);
            w.Int(_gaepanLeft);
            w.Int(_gaepanTurns);
            w.Bool(_gaepanFresh);
            w.Time(_gaepanJudgeAt);
            w.Int(_gaepanLoser);
            w.Int(_restartCount);
            w.Int(_falloutTurns);
            w.Bool(_restartPending);
            w.Int(_turnEndStage);

            // 「~한 후」 기록 · 처리 전 손패 · 지표 · 기다려
            w.Int(_afterRecords.Count);
            foreach (AfterRecord r in _afterRecords)
            {
                w.Int((int)r.Kind);
                w.Int(r.Subject);
                w.Int(r.Source);
                w.Int(r.Amount);
                w.Int(r.CardInstanceId);
            }
            w.Map(_handBefore);
            w.Int(_stats.Count);
            foreach (KeyValuePair<int, int[]> pair in _stats)
            {
                w.Int(pair.Key);
                w.Ints(pair.Value);
            }
            w.Ints(_waitCasters);

            // 승리
            w.Bool(_gameOver);
            w.Bool(_judgesElimination);

            return w.ToSnapshot();
        }

        /// <summary>
        /// 스냅샷으로 상태를 되살린다 — 새 방장이 막 만든 GameServer 에 한 번 부른다. cards = 이 게임의 덱 전체 (방장과 같은 덱 에셋으로 만든 것).
        /// 읽을 수 없으면 false — 그때는 상태가 반쯤 채워져 있으니 이 GameServer 를 쓰지 않는다.
        /// </summary>
        public bool Restore(GameSnapshot snapshot, IReadOnlyList<Card> cards)
        {
            try
            {
                Read(new SnapshotReader(snapshot), cards.ToDictionary(c => c.InstanceId));
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is KeyNotFoundException
                                              || exception is ArgumentException || exception is InvalidCastException)
            {
                RestoreError = exception.Message;
                return false;
            }
        }

        /// <summary>Restore 가 false 일 때의 이유 (로그용).</summary>
        public string RestoreError { get; private set; }

        private void Read(SnapshotReader r, Dictionary<int, Card> cards)
        {
            if (r.Int() != SnapshotVersion)
                throw new InvalidOperationException("스냅샷 버전이 다르다");

            Card CardOf(int instanceId) => cards[instanceId];
            void ReadCards(List<Card> into)
            {
                into.Clear();
                foreach (int id in r.Ints())
                    into.Add(CardOf(id));
            }
            void ReadSet(HashSet<int> into)
            {
                into.Clear();
                into.UnionWith(r.Ints());
            }

            // 사람
            _turnOrder.Clear();
            _turnOrder.AddRange(r.Ints());
            _players.Clear();
            int playerCount = r.Count();
            for (int i = 0; i < playerCount; i++)
            {
                var p = new PlayerState();
                _players[r.Int()] = p;
                ReadCards(p.Hand);
                ReadCards(p.Traps);
                p.Hp = r.Int();
                p.HasLeft = r.Bool();
                p.Life = (LifeState)r.Int();
                p.EmptyDraws = r.Int();
                p.SkipNextTurn = r.Bool();
                p.BlockActionNextTurn = r.Bool();
                p.ActionBlocked = r.Bool();
                p.TurnStartDraws = r.Int();
                p.TimeoutStreak = r.Int();
                p.IsChapchu = r.Bool();
                p.OutTurn = r.Int();
                p.OutByLeaving = r.Bool();
                p.DownedBy = r.Int();
            }
            if (_turnOrder.Any(actor => !_players.ContainsKey(actor)))
                throw new InvalidOperationException("턴 순서에 없는 사람이 있다");

            // 턴
            CurrentTurnActor = r.Int();
            TurnSerial = r.Int();
            ReadSet(_noTimeoutActors);
            _turnDeadline = r.Time();
            _mainActionUsed = r.Bool();
            _nextTurnAt = r.Time();
            _resumeAt = r.Time();
            _turnPaused = r.Bool();
            _lastTickAt = r.Time();
            _reversed = r.Bool();
            _extraTurnActor = r.Int();
            _heldNext = r.Int();
            _turnTaker = r.Int();
            if (CurrentTurnActor != -1 && !_players.ContainsKey(CurrentTurnActor))
                throw new InvalidOperationException("턴 주인이 참가자가 아니다");

            // 체인
            int entryCount = r.Count();
            var entries = new List<ChainEntry>(entryCount);
            var belows = new int[entryCount];
            var sharers = new Dictionary<int, int>[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                Card card = CardOf(r.Int());
                int user = r.Int();
                var e = new ChainEntry(card, user, r.Ints());
                e.TriggerAmount = r.Int();
                e.Negated = r.Bool();
                e.NegatedBy = r.Int();
                e.ReturnTo = r.Int();
                e.Returned = r.Bool();
                e.Excluded.UnionWith(r.Ints());
                e.DamageToHeal.UnionWith(r.Ints());
                e.HealToDamage.UnionWith(r.Ints());
                r.Map(e.DamageReduction);
                e.DamageReductionAll = r.Int();
                r.Map(e.HealAfter);
                r.Map(e.HandLossBlocked);
                e.Doubled = r.Bool();
                e.ReflectedBy = r.Int();
                sharers[i] = new Dictionary<int, int>();
                r.Map(sharers[i]);
                r.Map(e.SurviveAt);
                e.ProtectTraps.UnionWith(r.Ints());
                var rolled = new Dictionary<int, int>();
                r.Map(rolled);
                foreach (KeyValuePair<int, int> pair in rolled)
                    e.Rolled[EffectAt(card, pair.Key)] = pair.Value;
                e.KnockedDown.UnionWith(r.Ints());
                belows[i] = r.Int();
                e.NextEffect = r.Int();
                entries.Add(e);
            }
            ChainEntry EntryAt(int index) => index == -1 ? null : entries[index];
            for (int i = 0; i < entryCount; i++)
            {
                entries[i].Below = EntryAt(belows[i]);
                foreach (KeyValuePair<int, int> pair in sharers[i])
                    entries[i].DamageSharers[pair.Key] = entries[pair.Value];
            }
            void ReadEntries(List<ChainEntry> into)
            {
                into.Clear();
                foreach (int index in r.Ints())
                    into.Add(entries[index]);
            }
            ReadEntries(_chain);
            ReadEntries(_resolving);
            ReadEntries(_chainOrder);

            // 반응 시간
            _reactionOpensAt = r.Time();
            _reactionDeadline = r.Time();
            _reactionAsked.Clear();
            int askedCount = r.Count();
            for (int i = 0; i < askedCount; i++)
            {
                int actor = r.Int();
                _reactionAsked[actor] = r.Ints();
            }
            ReadSet(_reactionSkipped);
            ReadSet(_reactionHeld);

            // 고르기
            _choiceActor = r.Int();
            _choiceEntry = EntryAt(r.Int());
            int choiceEffect = r.Int();
            _choiceEffect = _choiceEntry == null ? null : EffectAt(_choiceEntry.Card, choiceEffect);
            if (_choiceEffect != null && !(_choiceEffect is DiscardOrDamageEffect) && !(_choiceEffect is ChooseDiscardEffect))
                throw new InvalidOperationException("고르기 효과가 아닌 효과를 고르는 중으로 되살릴 수 없다");
            _choiceDeadline = r.Time();
            _choiceMore = r.Int();
            if ((_choiceActor != -1) != (_choiceEntry != null))
                throw new InvalidOperationException("고르는 사람과 고르는 카드가 맞지 않는다");

            // 덱
            ReadCards(_deck);
            ReadCards(_discardPile);
            ReadCards(_allCards);

            // 개판 · 대폭발
            _gaepanCaster = r.Int();
            _gaepanLeft = r.Int();
            _gaepanTurns = r.Int();
            _gaepanFresh = r.Bool();
            _gaepanJudgeAt = r.Time();
            _gaepanLoser = r.Int();
            _restartCount = r.Int();
            _falloutTurns = r.Int();
            _restartPending = r.Bool();
            _turnEndStage = r.Int();

            // 「~한 후」 기록 · 처리 전 손패 · 지표 · 기다려
            _afterRecords.Clear();
            int afterCount = r.Count();
            for (int i = 0; i < afterCount; i++)
                _afterRecords.Add(new AfterRecord { Kind = (AfterKind)r.Int(), Subject = r.Int(), Source = r.Int(), Amount = r.Int(), CardInstanceId = r.Int() });
            _handBefore.Clear();
            r.Map(_handBefore);
            _stats.Clear();
            int statCount = r.Count();
            for (int i = 0; i < statCount; i++)
            {
                int actor = r.Int();
                int[] stats = r.Ints();
                if (stats.Length != StatCount)
                    throw new InvalidOperationException("지표 수가 다르다");
                _stats[actor] = stats;
            }
            _waitCasters.Clear();
            _waitCasters.AddRange(r.Ints());

            // 승리
            _gameOver = r.Bool();
            _judgesElimination = r.Bool();

            if (!r.AtEnd)
                throw new InvalidOperationException("스냅샷에 남는 값이 있다");

            _events.Clear();
            _handDirty.Clear();
        }

        /// <summary>
        /// 새 방장이 Restore 뒤에 한 번 부른다 — 멈췄던 단계부터 이어 간다 (09-network.md 9.1, PM 2026-11-19).
        /// 앞 방장이 사라진 뒤 흐른 시간은 알 수 없으므로, 멈췄던 단계의 시간은 처음부터 다시 준다 (턴 · 반응 · 고르기 · 사망 대기 · 개판 판정 공개).
        /// 반응 시간의 스킵 · 고민 연장 기록은 그대로다. 덱은 다시 섞는다 — 예비 방장일 때 받은 순서를 쓰지 못하게 (화면에는 알리지 않는다).
        /// departed — 사본 뒤에 나간 사람 (앞 방장 포함). 곧 RemovePlayer 로 나감 처리하므로 그 사람 앞으로는 단계를 다시 알리지 않는다.
        /// </summary>
        public IReadOnlyList<GameEvent> ResumeAfterMigration(ICollection<int> departed = null)
        {
            departed ??= Array.Empty<int>();

            if (_gameOver)
                return TakeEvents();

            double now = _clock();
            Shuffle(_deck);

            if (_chain.Count > 0)
            {
                // 연출은 이미 지나갔다 — 반응 시간을 지금부터. 물을 사람이 없으면 바로 처리
                _reactionOpensAt = now;
                _reactionDeadline = _reactionAsked.Count > 0 ? now + ReactionTimeLimit : now;
            }

            if (IsChoosing)
            {
                _choiceDeadline = now + ChoiceSeconds;
                if (!departed.Contains(_choiceActor))
                    _events.Add(new ChoiceStarted(_choiceActor, _choiceEntry.User, _choiceEntry.Card.CardId, _players[_choiceActor].Hand.Count,
                        _choiceEffect is DiscardOrDamageEffect option ? Scaled(_choiceEntry, option.amount) : 0, now, _choiceDeadline, IsHandChoice ? ChoiceKind.HandCard : ChoiceKind.Options));
            }

            if (IsGaepanJudging)
                _gaepanJudgeAt = now + GaepanJudgeSeconds; // 팻말 연출은 이미 나갔다 — 판정만 다시 센다

            // 후처리 연출은 이미 지나갔다 — 기다리던 다음 턴 · 턴 재개는 바로
            if (_nextTurnAt >= 0)
                _nextTurnAt = now;
            _resumeAt = -1;

            _lastTickAt = now;
            if (CurrentTurnActor != -1 && !_mainActionUsed)
            {
                _turnDeadline = now + TurnTimeLimit;
                // 멈춰 있으면 풀릴 때 UpdateTurnClock 이 남은 시간(15초)으로 알린다
                _turnPaused = IsPaused(now);
                if (!_turnPaused && IsInGame(CurrentTurnActor) && !departed.Contains(CurrentTurnActor))
                    _events.Add(new TurnResumed(CurrentTurnActor, _turnDeadline));
            }

            Revision++;
            return TakeEvents();
        }

        private static int IndexOfEffect(Card card, CardEffect effect)
        {
            for (int i = 0; i < card.Effects.Count; i++)
            {
                if (ReferenceEquals(card.Effects[i], effect))
                    return i;
            }

            throw new InvalidOperationException($"{card} 에 없는 효과");
        }

        private static CardEffect EffectAt(Card card, int index)
        {
            if (index < 0 || index >= card.Effects.Count)
                throw new InvalidOperationException($"{card} 의 효과 번호가 이상하다 ({index})");
            return card.Effects[index];
        }
    }
}
