using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game.Bots
{
    /// <summary>
    /// 봇의 판단 (18-bot.md 7절, 1단계 — 뽑기 · 행동 카드 · 찹츄 선언). 방장 기기에서만 돈다 (3절 #1).
    /// 사람과 같은 요청 메서드(GameServer.Draw · PlayCard · DeclareChapchu)를 부르고, 돌아온 일어난 일을 그대로 넘긴다 — 지름길이 없다 (3절 #2).
    /// 방장이 매 프레임 Tick 을 부른다. 행동마다 이유 한 줄을 TakeLogs 로 남긴다 (7-6).
    /// </summary>
    public sealed class BotDriver
    {
        // 확정 (18-bot.md 4절).
        private const double ThinkMin = 2, ThinkMax = 4; // 내 턴 시작 뒤 생각 시간 (초)
        private const int MaxRequestsPerTurn = 3;          // 거절 포함
        private const int HealBelowHp = 30;
        private const int DrawBelowHand = 7;
        private const int DrawAtHand = 9;

        private readonly GameServer _server;
        private readonly int[] _bots;
        private readonly Random _random;
        private readonly Func<double> _clock;
        private readonly Dictionary<int, TurnPlan> _plans = new Dictionary<int, TurnPlan>();
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
                _plans[bot] = new TurnPlan();
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
        private void TakeTurn(int bot, TurnPlan plan, List<GameEvent> events)
        {
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
            foreach (Card card in me.Hand)
            {
                if (card.Type != CardType.Action || !card.Effects.Any(e => e.BotRole == role))
                    continue;

                if (!TryTargets(card, me, opponents, role, preferredTarget, out int[] targets))
                    continue;

                int instanceId = card.InstanceId;
                string to = targets.Length > 0 ? " → " + string.Join(", ", targets.Select(t => $"P{t}")) : "";
                choice = new Choice(line, $"{reason} → 카드 {card.CardId} (#{instanceId}){to}", () => _server.PlayCard(bot, instanceId, targets));
                return true;
            }

            choice = default;
            return false;
        }

        // 카드 대상 타입에 맞는 요청 대상. 쓸 수 없으면 false (TwoOthers 는 1단계에서 쓰지 않는다, 7-2).
        private bool TryTargets(Card card, PlayerState me, int[] opponents, BotRole role, int preferredTarget, out int[] targets)
        {
            targets = Array.Empty<int>();
            switch (card.Target)
            {
                case TargetType.OneOther:
                    if (opponents.Length == 0)
                        return false;
                    targets = new[] { preferredTarget > 0 ? preferredTarget : opponents[0] };
                    return true;

                case TargetType.AllOthers:
                    return opponents.Length > 0;

                case TargetType.Everyone:
                    // 나도 맞는다 — 공격으로는 내 HP 가 상대 전원보다 높을 때만 (7-3 ㄱ A14)
                    return role != BotRole.Attack || opponents.All(a => _server.StateOf(a).Hp < me.Hp);

                case TargetType.Self:
                case TargetType.None:
                    return role == BotRole.Heal;

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
