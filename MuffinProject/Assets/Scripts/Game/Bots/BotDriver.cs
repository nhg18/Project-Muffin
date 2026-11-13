using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game.Bots
{
    /// <summary>
    /// 봇의 판단 (18-bot.md 7절 — 1단계 뽑기 · 행동 카드 · 찹츄 선언, 2단계 함정 설치 · 발동 · 카운터). 방장 기기에서만 돈다 (3절 #1).
    /// 사람과 같은 요청 메서드(GameServer.Draw · PlayCard · DeclareChapchu)를 부르고, 돌아온 일어난 일을 그대로 넘긴다 — 지름길이 없다 (3절 #2).
    /// 방장이 매 프레임 Tick 을 부른다. 행동마다 이유 한 줄을 TakeLogs 로 남긴다 (7-6).
    /// </summary>
    public sealed class BotDriver
    {
        // 확정 (18-bot.md 4절).
        private const double ThinkMin = 2, ThinkMax = 4; // 내 턴 시작 뒤 생각 시간 (초)
        private const double ReactMin = 1, ReactMax = 3; // 반응 창(카운터 · 함정 · 사망 대기)이 열린 뒤 (초)
        private const int MaxRequestsPerTurn = 3;          // 거절 포함
        private const int HealBelowHp = 30;
        private const int DrawBelowHand = 7;
        private const int DrawAtHand = 9;

        private readonly GameServer _server;
        private readonly int[] _bots;
        private readonly Random _random;
        private readonly Func<double> _clock;
        private readonly Dictionary<int, TurnPlan> _plans = new Dictionary<int, TurnPlan>();
        private readonly Dictionary<int, ReactPlan> _reacts = new Dictionary<int, ReactPlan>();
        private readonly List<string> _logs = new List<string>();

        // 봇 한 명의 이번 턴 — 언제 행동할지 · 몇 번 요청했는지 · 거절돼 건너뛸 줄
        private sealed class TurnPlan
        {
            public int Serial = -1;
            public double ActAt;
            public bool Done;
            public int Requests;
            public readonly HashSet<int> SkippedLines = new HashSet<int>();
        }

        // 봇 한 명의 반응 — 반응 창 하나(Key)에 한 번만. 거절돼도 그 창에서는 더 내지 않는다 (7-4)
        private sealed class ReactPlan
        {
            public string Key;
            public double ActAt;
            public bool Done;
        }

        // 반응할 거리 하나 — 창(Key) · 열리는 시각 · 요청
        private readonly struct Reaction
        {
            public readonly string Key;
            public readonly double OpensAt;
            public readonly string Reason;
            public readonly Func<IReadOnlyList<GameEvent>> Request;

            public Reaction(string key, double opensAt, string reason, Func<IReadOnlyList<GameEvent>> request)
            {
                Key = key;
                OpensAt = opensAt;
                Reason = reason;
                Request = request;
            }
        }

        // 7-1 표의 한 줄을 고른 결과. Request 가 null 이면 메인 행동을 하지 않는다.
        private readonly struct Choice
        {
            public readonly int Line;
            public readonly string Reason;
            public readonly Func<IReadOnlyList<GameEvent>> Request;

            public Choice(int line, string reason, Func<IReadOnlyList<GameEvent>> request)
            {
                Line = line;
                Reason = reason;
                Request = request;
            }
        }

        public BotDriver(GameServer server, IEnumerable<int> botActors, Random random, Func<double> clock)
        {
            _server = server;
            _bots = botActors.ToArray();
            _random = random;
            _clock = clock;
            foreach (int bot in _bots)
            {
                _plans[bot] = new TurnPlan();
                _reacts[bot] = new ReactPlan();
            }
        }

        public bool HasBots => _bots.Length > 0;

        /// <summary>지난 Tick 뒤로 봇이 한 행동의 이유 (방장 로그용, 18-bot.md 7-6). 꺼내면 비운다.</summary>
        public IReadOnlyList<string> TakeLogs()
        {
            string[] logs = _logs.ToArray();
            _logs.Clear();
            return logs;
        }

        /// <summary>봇들이 지금 할 일을 하고, 그 요청들이 만든 일어난 일을 순서대로 돌려준다.</summary>
        public IReadOnlyList<GameEvent> Tick()
        {
            var events = new List<GameEvent>();
            if (_bots.Length == 0 || _server.IsGameOver)
                return events;

            foreach (int bot in _bots)
                DeclareChapchuIfReady(bot, events);

            double clock = _clock();
            foreach (int bot in _bots)
                ReactIfNeeded(bot, clock, events);

            int turn = _server.CurrentTurnActor;
            if (_plans.TryGetValue(turn, out TurnPlan plan) && _server.InGame(turn))
            {
                double now = _clock();
                if (plan.Serial != _server.TurnSerial)
                    StartTurn(plan, now);

                if (!plan.Done && now >= plan.ActAt && !_server.IsBusy && !_server.MainActionUsed)
                    TakeTurn(turn, plan, events);
            }

            return events;
        }

        // 7-5 — 손패가 정확히 10장이 되는 순간 즉시 (남의 턴에도). 해제되면 다시 10장이 될 때 다시.
        private void DeclareChapchuIfReady(int bot, List<GameEvent> events)
        {
            if (!_server.InGame(bot))
                return;

            PlayerState me = _server.StateOf(bot);
            if (me.Life != LifeState.Alive || me.IsChapchu || me.Hand.Count != GameServer.ChapchuHandCount)
                return;

            IReadOnlyList<GameEvent> result = _server.DeclareChapchu(bot);
            events.AddRange(result);
            Log(bot, RejectCodeOf(result, bot) == 0 ? "7-5 손패 10장 → 찹츄 선언" : $"7-5 찹츄 선언 거절 (코드 {RejectCodeOf(result, bot)})");
        }

        private void StartTurn(TurnPlan plan, double now)
        {
            plan.Serial = _server.TurnSerial;
            plan.ActAt = now + ThinkMin + _random.NextDouble() * (ThinkMax - ThinkMin);
            plan.Done = false;
            plan.Requests = 0;
            plan.SkippedLines.Clear();
        }

        // 7-1 을 위에서부터 보고 처음 맞는 행동 하나. 거절되면 그 줄을 빼고 다음 줄로 — 거절 포함 3회까지.
        // 그 전에 손패의 함정을 빈 칸 수만큼 설치한다 (7-4, 메인 행동 아님 — 요청 횟수에 넣지 않는다).
        private void TakeTurn(int bot, TurnPlan plan, List<GameEvent> events)
        {
            SetTraps(bot, events);

            while (plan.Requests < MaxRequestsPerTurn)
            {
                Choice choice = Choose(bot, plan.SkippedLines);
                if (choice.Request == null)
                {
                    Log(bot, $"7-1 #{choice.Line} {choice.Reason}");
                    plan.Done = true;
                    return;
                }

                IReadOnlyList<GameEvent> result = choice.Request();
                events.AddRange(result);
                plan.Requests++;

                int code = RejectCodeOf(result, bot);
                if (code == 0)
                {
                    Log(bot, $"7-1 #{choice.Line} {choice.Reason}");
                    plan.Done = true;
                    return;
                }

                Log(bot, $"7-1 #{choice.Line} {choice.Reason} → 거절 (코드 {code}), 다음 줄");
                plan.SkippedLines.Add(choice.Line);
            }

            Log(bot, $"요청 {MaxRequestsPerTurn}회 — 이번 턴은 더 하지 않는다 (시간 초과로 넘어감)");
            plan.Done = true;
        }

        // 7-4 — 손패 앞쪽부터 빈 함정 칸 수만큼. 손패가 10장(찹츄)이면 설치하지 않는다 — 설치하면 9장이 되어 찹츄가 깨진다
        private void SetTraps(int bot, List<GameEvent> events)
        {
            PlayerState me = _server.StateOf(bot);
            if (me.IsChapchu || me.Hand.Count == GameServer.ChapchuHandCount)
                return;

            foreach (Card card in me.Hand.Where(c => c.Type == CardType.Trap).ToArray())
            {
                if (me.Traps.Count >= GameServer.TrapSlotCount)
                    return;

                IReadOnlyList<GameEvent> result = _server.SetTrap(bot, card.InstanceId);
                events.AddRange(result);
                int code = RejectCodeOf(result, bot);
                Log(bot, code == 0 ? $"7-4 함정 설치 → 카드 {card.CardId} (#{card.InstanceId}) · 함정 {me.Traps.Count}장" : $"7-4 함정 설치 거절 (코드 {code})");
                if (code != 0)
                    return;
            }
        }

        // 7-4 — 지금 반응할 거리가 있으면 그 창이 열리고 1 ~ 3초 뒤 한 번. 같은 창에서는 다시 내지 않는다.
        private void ReactIfNeeded(int bot, double now, List<GameEvent> events)
        {
            ReactPlan plan = _reacts[bot];
            if (!_server.InGame(bot) || !TryFindReaction(bot, now, out Reaction reaction))
            {
                plan.Key = null;
                return;
            }

            if (plan.Key != reaction.Key)
            {
                plan.Key = reaction.Key;
                plan.ActAt = Math.Max(now, reaction.OpensAt) + ReactMin + _random.NextDouble() * (ReactMax - ReactMin);
                plan.Done = false;
            }

            if (plan.Done || now < plan.ActAt)
                return;

            plan.Done = true;
            IReadOnlyList<GameEvent> result = reaction.Request();
            events.AddRange(result);
            int code = RejectCodeOf(result, bot);
            Log(bot, code == 0 ? reaction.Reason : $"{reaction.Reason} → 거절 (코드 {code}), 이 창에서는 더 내지 않음");
        }

        // 반응할 거리를 우선순위로 하나 (7-4). 없으면 false.
        private bool TryFindReaction(int bot, double now, out Reaction reaction)
        {
            if (_server.IsChoosing && _server.ChoiceActor == bot)
            {
                // 119 목표 조준의 대상 — 반응처럼 1 ~ 3초 뒤 (고르는 시간 5초 안). 고르는 규칙은 효과가 (18-bot 7-3, 프로토타입 v2)
                int option = _server.BotChoiceOption(bot);
                reaction = new Reaction($"choice:{_server.ChoiceDeadline}", now, $"고르기 → {(option == 0 ? "손패 전부 버리기" : "피해 받기")}", () => _server.Choose(bot, option));
                return true;
            }

            PlayerState me = _server.StateOf(bot);
            ChainEntry top = _server.ChainTop;
            bool reactionOpen = _server.IsReactionOpen(now);
            bool inReveal = top != null && !reactionOpen;

            // ① 내가 사망 대기 — 사망 관련 함정 (설치 순서). 06 3절: 사망 대기 중엔 이것만
            if (me.Life == LifeState.DeathPending)
            {
                Card dying = me.Traps.FirstOrDefault(c => c.Trigger == TrapTrigger.SelfDeathPending);
                if (dying != null && !inReveal)
                {
                    reaction = TrapReaction(bot, dying, $"death:{_server.DeathDeadline}", now, "7-4 사망 대기 → 사망 관련 함정");
                    return true;
                }

                reaction = default;
                return false;
            }

            if (me.Life != LifeState.Alive)
            {
                reaction = default;
                return false;
            }

            // ② 나를 대상으로 한 남의 카드가 체인 맨 위 — 카운터, 없으면 「카드 반응」 함정. 남끼리의 카드에는 끼지 않는다
            if (reactionOpen && top.User != bot && top.Targets.Contains(bot))
            {
                string key = $"chain:{_server.ReactionOpensAt}";
                Card counter = me.Hand.Where(c => c.Type == CardType.Counter && _server.CanReactTo(c, bot, top)).OrderBy(c => c.CardId).FirstOrDefault();
                if (counter != null)
                {
                    int[] targets = counter.Target == TargetType.OneOther ? SortedOpponents(bot).Take(1).ToArray() : Array.Empty<int>();
                    int instanceId = counter.InstanceId;
                    reaction = new Reaction(key, _server.ReactionOpensAt, $"7-4 나를 노린 카드 {top.Card.CardId} 에 카운터 → 카드 {counter.CardId} (#{instanceId})",
                        () => _server.PlayCounter(bot, instanceId, targets));
                    return true;
                }

                Card react = me.Traps.FirstOrDefault(c => c.Trigger == TrapTrigger.ReactToCard && _server.CanReactTo(c, bot, top));
                if (react != null)
                {
                    reaction = TrapReaction(bot, react, key, _server.ReactionOpensAt, $"7-4 나를 노린 카드 {top.Card.CardId} 에 함정");
                    return true;
                }
            }

            // ③ 함정 시간 — 「~한 후」 조건(맞음 · 훔쳐짐 · 버려짐 · 파괴 · 스킵 …)이 맞는 함정, 설치 순서
            if (_server.IsTrapWindowOpenAt(now))
            {
                Card after = me.Traps.FirstOrDefault(c => _server.HasAfterTrigger(bot, c));
                if (after != null)
                {
                    reaction = TrapReaction(bot, after, $"trapwin:{_server.TrapWindowOpensAt}", _server.TrapWindowOpensAt, $"7-4 함정 시간 ({after.Trigger}) → 함정");
                    return true;
                }
            }

            // ④ 조건이 맞는 상태 함정 (HP 이하 · 찹츄 존재) — 연출 중이 아닐 때
            if (!inReveal)
            {
                Card state = me.Traps.FirstOrDefault(c =>
                    (c.Trigger == TrapTrigger.HpAtMost && me.Hp <= c.TriggerValue) ||
                    (c.Trigger == TrapTrigger.ChapchuExists && _server.ActorsInGame.Any(a => a != bot && _server.StateOf(a).IsChapchu)));
                if (state != null)
                {
                    reaction = TrapReaction(bot, state, $"state:{state.InstanceId}", now, "7-4 함정 조건 충족 → 함정");
                    return true;
                }
            }

            reaction = default;
            return false;
        }

        private Reaction TrapReaction(int bot, Card trap, string key, double opensAt, string reason)
        {
            int instanceId = trap.InstanceId;
            int[] targets = trap.Target == TargetType.OneOther ? SortedOpponents(bot).Take(1).ToArray() : Array.Empty<int>();
            return new Reaction(key, opensAt, $"{reason} → 카드 {trap.CardId} (#{instanceId})", () => _server.ActivateTrap(bot, instanceId, targets));
        }

        private Choice Choose(int bot, HashSet<int> skipped)
        {
            PlayerState me = _server.StateOf(bot);
            int[] opponents = SortedOpponents(bot);
            int chapchuTarget = opponents.FirstOrDefault(a => _server.StateOf(a).IsChapchu);
            bool anyChapchu = opponents.Any(a => _server.StateOf(a).IsChapchu);

            if (!skipped.Contains(1) && me.IsChapchu)
                return new Choice(1, "찹츄 상태 — 메인 행동 없이 기다림", null);

            Choice found;
            if (!skipped.Contains(2) && anyChapchu && TryCard(bot, me, opponents, BotRole.HandChange, chapchuTarget, 2, "찹츄 상대 손패 깨기", out found))
                return found;

            if (!skipped.Contains(3) && anyChapchu && TryCard(bot, me, opponents, BotRole.Attack, chapchuTarget, 3, "찹츄 상대 공격", out found))
                return found;

            if (!skipped.Contains(4) && me.Hp < HealBelowHp && TryCard(bot, me, opponents, BotRole.Heal, -1, 4, $"HP {me.Hp} < {HealBelowHp} 회복", out found))
                return found;

            if (!skipped.Contains(5) && me.Hand.Count == DrawAtHand)
                return DrawChoice(bot, 5, $"손패 {DrawAtHand}장 — 뽑아서 10장");

            if (!skipped.Contains(6) && me.Hand.Count < DrawBelowHand)
                return DrawChoice(bot, 6, $"손패 {me.Hand.Count}장 < {DrawBelowHand}");

            if (!skipped.Contains(10) && TryCard(bot, me, opponents, BotRole.Board, -1, 10, "판 카드 조건 맞음", out found))
                return found;

            if (!skipped.Contains(7) && TryCard(bot, me, opponents, BotRole.Attack, -1, 7, "공격", out found))
                return found;

            if (!skipped.Contains(8) && TryCard(bot, me, opponents, BotRole.HandChange, -1, 8, "손패 변동", out found))
                return found;

            if (!skipped.Contains(9))
                return DrawChoice(bot, 9, "할 것이 없어 뽑기");

            return new Choice(9, "뽑기도 거절 — 아무것도 하지 않음", null);
        }

        private Choice DrawChoice(int bot, int line, string reason)
        {
            return new Choice(line, reason + " → 뽑기", () => _server.Draw(bot));
        }

        // 그 묶음의 카드 중 지금 쓸 수 있는 손패 앞쪽 카드 (7-1 「쓸 수 있는」 · 같은 줄 여러 장이면 앞쪽).
        // preferredTarget 이 있으면 그 사람에게 (찹츄 상대), 없으면 7-2 순서 첫 사람.
        private bool TryCard(int bot, PlayerState me, int[] opponents, BotRole role, int preferredTarget, int line, string reason, out Choice choice)
        {
            if (me.ActionBlocked)
            {
                choice = default;
                return false; // 107 짖기 — 이번 턴은 행동 카드를 쓸 수 없다 (방장 검증과 같은 기준, 7-1 「쓸 수 있는」) → 뽑기 줄로
            }

            // 노릴 상대가 따로 있는 카드(105 — 쓰러뜨릴 수 있는 상대)를 먼저 본다. 나머지는 손패 순서 그대로 (18-bot.md 7-3)
            foreach (Card card in me.Hand.OrderByDescending(c => c.Effects.Any(e => e.BotPreferredTarget(_server, bot, opponents) != -1)))
            {
                if (card.Type != CardType.Action || !card.Effects.Any(e => e.BotRole == role))
                    continue;

                if (!TryTargets(bot, card, me, opponents, role, preferredTarget, out int[] targets))
                    continue;

                if (!card.Effects.All(e => e.BotWants(_server, bot, targets)))
                    continue; // 이 카드의 효과가 지금은 쓸 만하지 않다 (18-bot.md 7-3 조건 — 예: 111 은 대상 HP 가 내 HP 보다 높을 때만 · 105 는 반동으로 내가 쓰러지면 안 씀)

                int instanceId = card.InstanceId;
                string to = targets.Length > 0 ? " → " + string.Join(", ", targets.Select(t => $"P{t}")) : "";
                choice = new Choice(line, $"{reason} → 카드 {card.CardId} (#{instanceId}){to}", () => _server.PlayCard(bot, instanceId, targets));
                return true;
            }

            choice = default;
            return false;
        }

        // 카드 대상 타입에 맞는 요청 대상. 쓸 수 없으면 false (TwoOthers 는 1단계에서 쓰지 않는다, 7-2).
        // 고르는 타입은 대상 후보 조건(11-card-list 1-2)에 맞는 사람만 — 방장 검증과 같은 기준 (7-1 「쓸 수 있는」).
        private bool TryTargets(int bot, Card card, PlayerState me, int[] opponents, BotRole role, int preferredTarget, out int[] targets)
        {
            targets = Array.Empty<int>();
            switch (card.Target)
            {
                case TargetType.OneOther:
                    int[] pickable = opponents.Where(a => _server.MeetsTargetCondition(a, card.TargetCondition)).ToArray();
                    if (pickable.Length == 0)
                        return false;
                    if (role == BotRole.Heal)
                    {
                        // 상대를 거쳐 내 HP 를 올리는 카드(111 HP 맞바꾸기) — HP 가 가장 높은 상대 (18-bot.md 7-2, 제안)
                        targets = new[] { pickable.OrderByDescending(a => _server.StateOf(a).Hp).First() };
                        return true;
                    }
                    int wish = card.Effects.Select(e => e.BotPreferredTarget(_server, bot, pickable)).Where(t => t != -1).DefaultIfEmpty(-1).First();
                    targets = new[] { pickable.Contains(preferredTarget) ? preferredTarget : pickable.Contains(wish) ? wish : pickable[0] };
                    return true;

                case TargetType.OneAny:
                    // 나를 포함한 1명 — 회복(7-1 #4 는 내 HP 기준)은 나에게. 그 밖의 쓰임은 7-2 순서의 상대에게
                    if (role == BotRole.Heal)
                    {
                        if (!_server.MeetsTargetCondition(bot, card.TargetCondition))
                            return false;
                        targets = new[] { bot };
                        return true;
                    }
                    int[] others = opponents.Where(a => _server.MeetsTargetCondition(a, card.TargetCondition)).ToArray();
                    if (others.Length == 0)
                        return false;
                    targets = new[] { others.Contains(preferredTarget) ? preferredTarget : others[0] };
                    return true;

                case TargetType.AllOthers:
                    return opponents.Length > 0;

                case TargetType.Everyone:
                    // 나도 맞는다 — 공격으로는 내 HP 가 상대 전원보다 높을 때만 (7-3 ㄱ A14). 판 카드는 효과의 조건(BotWants)으로
                    return role != BotRole.Attack || opponents.All(a => _server.StateOf(a).Hp < me.Hp);

                case TargetType.Self:
                case TargetType.None:
                    return role == BotRole.Heal || role == BotRole.Board;

                case TargetType.ChapchuPlayers: // 나도 찹츄면 나도 맞는다 — 찹츄 상대가 있고 나는 아닐 때만 (7-3 ㄱ A02)
                    return !me.IsChapchu && opponents.Any(a => _server.StateOf(a).IsChapchu);

                case TargetType.ChapchuOthers:
                    return opponents.Any(a => _server.StateOf(a).IsChapchu);

                case TargetType.MostHand:
                    // 손패 최다 1명이 대상 — 상대 중 누가 나(낸 뒤 장수)보다 많을 때만. 동률이면 내가 대상이 될 수 있어 쓰지 않는다 (18-bot.md 7-3, 제안)
                    return opponents.Any(a => _server.StateOf(a).Hand.Count > me.Hand.Count - 1);

                case TargetType.MoreHandThanMe:
                    return opponents.Any(a => _server.StateOf(a).Hand.Count > me.Hand.Count - 1);

                default:
                    return false;
            }
        }

        // 7-2 — 나를 뺀 생존자를 찹츄 → 손패 많은 → HP 낮은 순으로. 모두 같으면 무작위.
        private int[] SortedOpponents(int bot)
        {
            return _server.ActorsInGame
                .Where(a => a != bot && _server.StateOf(a).Life == LifeState.Alive)
                .OrderBy(_ => _random.Next())
                .OrderByDescending(a => _server.StateOf(a).IsChapchu)
                .ThenByDescending(a => _server.StateOf(a).Hand.Count)
                .ThenBy(a => _server.StateOf(a).Hp)
                .ToArray();
        }

        private static int RejectCodeOf(IReadOnlyList<GameEvent> events, int bot)
        {
            foreach (GameEvent gameEvent in events)
            {
                if (gameEvent is RequestRejected rejected && rejected.Actor == bot)
                    return rejected.Code;
            }

            return 0;
        }

        private void Log(int bot, string message) => _logs.Add($"P{bot} {message}");
    }
}
