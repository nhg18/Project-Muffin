using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Game.Effects;

namespace Chapchu.Game.Bots
{
    /// <summary>
    /// 봇의 판단 (18-bot.md 7절). 방장 기기에서만 돈다 (3절 #1).
    /// 내 턴에는 할 수 있는 수(뽑기 · 카드 × 대상 · 기다림)마다 점수를 매겨 가장 높은 수를 둔다 (7-1). 반응(카운터 · 함정 · 고르기)은 7-4 의 순서대로.
    /// 사람과 같은 요청 메서드(GameServer.Draw · PlayCard · DeclareChapchu …)를 부르고, 돌아온 일어난 일을 그대로 넘긴다 — 지름길이 없다 (3절 #2).
    /// 방장이 매 프레임 Tick 을 부르고, 방에서 일어난 일을 Observe 로 알려 준다. 행동마다 이유 한 줄을 TakeLogs 로 남긴다 (7-6).
    /// </summary>
    public sealed class BotDriver
    {
        // 18-bot.md 4절 「생각 시간」 — 수가 뻔하면 짧게, 비슷한 수가 있으면 길게
        private const double QuickThinkMin = 1.5, QuickThinkMax = 2.5;
        private const double ThinkMin = 2, ThinkMax = 4;
        private const double LongThinkMin = 3, LongThinkMax = 5.5;
        private const double ClearGap = 40;  // 1등과 2등의 점수 차가 이 이상이면 뻔한 수
        private const double CloseGap = 10;  // 이 미만이면 고민되는 수
        private const double ReactMin = 1, ReactMax = 3; // 반응 창(카운터 · 함정 · 사망 대기 · 고르기) · 선언 대기가 열린 뒤 (초)
        private const int MaxRequestsPerTurn = 3;          // 거절 포함
        private const int MaxDeclaresPerTurn = 2;          // 거절돼도 매 프레임 다시 내지 않게

        // 18-bot.md 4절 「점수 값」 — 나머지는 BotView · 각 효과의 BotScore
        private const double IdleScore = -5;        // 아무것도 하지 않음 (손패가 10장이 아닐 때)
        private const double EmptyDrawPenalty = 15; // 덱 · 버림 더미가 비어 못 뽑는 1장마다 (빈 덱 피해)
        private const int GrudgePerHit = 2, GrudgeCap = 6, GrudgeCounted = 4; // 나를 노린 카드 1장마다 +2 · 내 턴마다 −1
        private const double GrudgeScore = 4;       // 원한 1 마다 그 상대를 노리는 수에 더하는 점수

        private readonly GameServer _server;
        private readonly int[] _bots;
        private readonly Random _random;
        private readonly Func<double> _clock;
        private readonly Dictionary<int, BotMind> _minds = new Dictionary<int, BotMind>();
        private readonly List<string> _logs = new List<string>();

        // 봇 한 명 — 난이도 · 성향 · 원한과, 이번 턴 · 이번 반응 창의 진행
        private sealed class BotMind
        {
            public BotLevelSpec Spec;
            public BotStyle Style;
            public readonly Dictionary<int, int> Grudge = new Dictionary<int, int>(); // 나를 노린 사람 → 원한
            public readonly HashSet<int> Noticed = new HashSet<int>();                // 이번 턴에 찹츄를 알아챈 상대

            // 이번 턴
            public int Serial = -1;
            public double ActAt;
            public bool Done;
            public int Requests;
            public double DeclareAt = -1; // 선언 대기에서 선언할 시각. 아직 안 정했으면 음수
            public int Declares;

            // 이번 반응 창 — 창 하나(Key)에 한 번만. 거절돼도 그 창에서는 더 내지 않는다 (7-4)
            public string ReactKey;
            public double ReactAt;
            public bool Reacted;
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

        // 내 턴에 둘 수 있는 수 하나와 그 점수 (7-1). Request 가 null 이면 메인 행동 없이 기다린다.
        private sealed class Move
        {
            public string Text;
            public double Score;
            public Func<IReadOnlyList<GameEvent>> Request;
        }

        /// <param name="levelOf">봇 식별자 → 난이도. 없으면 모두 보통 (18-bot.md 7-7).</param>
        public BotDriver(GameServer server, IEnumerable<int> botActors, Random random, Func<double> clock, Func<int, BotLevel> levelOf = null)
        {
            _server = server;
            _bots = botActors.ToArray();
            _random = random;
            _clock = clock;

            BotStyle[] styles = (BotStyle[])Enum.GetValues(typeof(BotStyle));
            foreach (int bot in _bots)
            {
                BotLevel level = levelOf != null ? levelOf(bot) : BotLevel.Normal;
                var mind = new BotMind { Spec = BotLevelSpec.Of(level), Style = styles[_random.Next(styles.Length)] };
                _minds[bot] = mind;
                Log(bot, $"난이도 {level} · 성향 {mind.Style}");
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

        /// <summary>
        /// 방에서 일어난 일을 봇에게 알린다 — 누가 나를 노렸는지 기억해 두었다가 되갚는다 (18-bot.md 7-8 「원한」).
        /// 사람의 요청 · 봇의 요청 · 시간 진행이 만든 일을 모두 넘긴다. 공개된 일(카드를 냈다 · 대상)만 읽는다.
        /// </summary>
        public void Observe(IReadOnlyList<GameEvent> events)
        {
            foreach (GameEvent gameEvent in events)
            {
                if (!(gameEvent is CardPlayed played) || played.Targets == null)
                    continue;

                foreach (int bot in _bots)
                {
                    if (bot == played.Actor || !played.Targets.Contains(bot))
                        continue;

                    Dictionary<int, int> grudge = _minds[bot].Grudge;
                    grudge.TryGetValue(played.Actor, out int value);
                    grudge[played.Actor] = Math.Min(GrudgeCap, value + GrudgePerHit);
                }
            }
        }

        /// <summary>봇들이 지금 할 일을 하고, 그 요청들이 만든 일어난 일을 순서대로 돌려준다.</summary>
        public IReadOnlyList<GameEvent> Tick()
        {
            var events = new List<GameEvent>();
            if (_bots.Length == 0 || _server.IsGameOver)
                return events;

            double now = _clock();
            foreach (int bot in _bots)
                ReactIfNeeded(bot, now, events);

            int turn = _server.CurrentTurnActor;
            if (!_minds.TryGetValue(turn, out BotMind mind) || !_server.InGame(turn))
                return events;

            if (mind.Serial != _server.TurnSerial)
                StartTurn(turn, mind, now);

            DeclareIfReady(turn, mind, now, events);
            if (!mind.Done && now >= mind.ActAt && !_server.IsBusy && !_server.MainActionUsed)
                TakeTurn(turn, mind, events);

            return events;
        }

        #region 내 턴 (7-1)

        private void StartTurn(int bot, BotMind mind, double now)
        {
            mind.Serial = _server.TurnSerial;
            mind.Done = false;
            mind.Requests = 0;
            mind.DeclareAt = -1;
            mind.Declares = 0;

            // 원한은 내 턴마다 1 씩 식는다 (7-8)
            foreach (int actor in mind.Grudge.Keys.ToArray())
                mind.Grudge[actor] = Math.Max(0, mind.Grudge[actor] - 1);

            // 찹츄 상태인 상대를 이번 턴에 알아챘는가 (7-7) — 턴마다 한 번 정한다
            mind.Noticed.Clear();
            foreach (int actor in _server.ActorsInGame)
            {
                if (actor != bot && _server.StateOf(actor).IsChapchu && _random.NextDouble() < mind.Spec.NoticeRate)
                    mind.Noticed.Add(actor);
            }

            // 생각 시간 — 지금 보이는 수들의 점수 차로 (4절). 둘 때는 다시 계산한다
            double[] scores = Moves(bot, mind).Select(move => move.Score).OrderByDescending(score => score).ToArray();
            double gap = scores.Length > 1 ? scores[0] - scores[1] : ClearGap;
            double min = gap >= ClearGap ? QuickThinkMin : gap < CloseGap ? LongThinkMin : ThinkMin;
            double max = gap >= ClearGap ? QuickThinkMax : gap < CloseGap ? LongThinkMax : ThinkMax;
            mind.ActAt = now + min + _random.NextDouble() * (max - min);
        }

        // 7-5 — 내 턴에 손패가 정확히 10장이면 선언한다. 뽑아서 10장(선언 대기)이면 1 ~ 3초 뒤, 카드 효과로 10장이면 턴이 넘어가기 전에 바로.
        // 메인 행동 전에 이미 10장이면 7-1 이 「선언하고 기다림」 을 고를 때 한다 (TakeTurn).
        private void DeclareIfReady(int bot, BotMind mind, double now, List<GameEvent> events)
        {
            PlayerState me = _server.StateOf(bot);
            if (me.Life != LifeState.Alive || me.IsChapchu || me.Hand.Count != GameServer.ChapchuHandCount || _server.IsResolving)
                return;

            if (_server.IsDeclareWait)
            {
                if (mind.DeclareAt < 0)
                    mind.DeclareAt = now + ReactMin + _random.NextDouble() * (ReactMax - ReactMin);
                if (now < mind.DeclareAt)
                    return;
            }
            else if (!_server.MainActionUsed)
            {
                return;
            }

            Declare(bot, mind, events, _server.IsDeclareWait ? "뽑아서 손패 10장 (선언 대기)" : "카드를 쓰고 손패 10장");
        }

        private void Declare(int bot, BotMind mind, List<GameEvent> events, string why)
        {
            if (mind.Declares >= MaxDeclaresPerTurn)
                return;

            mind.Declares++;
            IReadOnlyList<GameEvent> result = _server.DeclareChapchu(bot);
            events.AddRange(result);
            int code = RejectCodeOf(result, bot);
            Log(bot, code == 0 ? $"7-5 {why} → 찹츄 선언" : $"7-5 찹츄 선언 거절 (코드 {code})");
        }

        // 점수가 가장 높은 수 하나. 거절되면 그 수를 빼고 다음 수로 — 거절 포함 3회까지.
        // 그 전에 손패의 함정을 빈 칸 수만큼 설치한다 (7-4, 메인 행동 아님 — 요청 횟수에 넣지 않는다).
        private void TakeTurn(int bot, BotMind mind, List<GameEvent> events)
        {
            SetTraps(bot, events);

            List<Move> moves = Moves(bot, mind);
            while (mind.Requests < MaxRequestsPerTurn && moves.Count > 0)
            {
                Move move = Pick(moves, mind.Spec, out string how);
                Move next = moves.Where(other => other != move).OrderByDescending(other => other.Score).FirstOrDefault();
                string line = $"7-1 {move.Text} · {move.Score:0}점{how}" + (next != null ? $" (다음 수: {next.Text} {next.Score:0}점)" : "");

                if (move.Request == null)
                {
                    Log(bot, line);
                    PlayerState me = _server.StateOf(bot);
                    if (!me.IsChapchu && me.Hand.Count == GameServer.ChapchuHandCount)
                        Declare(bot, mind, events, "내 턴 · 손패 10장");
                    mind.Done = true;
                    return;
                }

                IReadOnlyList<GameEvent> result = move.Request();
                events.AddRange(result);
                mind.Requests++;

                int code = RejectCodeOf(result, bot);
                if (code == 0)
                {
                    Log(bot, line);
                    mind.Done = true;
                    return;
                }

                Log(bot, $"{line} → 거절 (코드 {code}), 다음 수로");
                moves.Remove(move);
            }

            Log(bot, $"요청 {MaxRequestsPerTurn}회 — 이번 턴은 더 하지 않는다 (시간 초과로 넘어감)");
            mind.Done = true;
        }

        // 난이도만큼 흐리게 고른다 (7-7) — 점수마다 ± Noise 를 섞고, MistakeRate 확률로는 손해가 아닌 아무 수나.
        // 기다림은 점수 그대로 1등일 때만 고른다 — 흐려져서 턴을 그냥 흘리는 일이 없게.
        private Move Pick(List<Move> moves, BotLevelSpec spec, out string how)
        {
            how = "";
            Move best = moves.OrderByDescending(move => move.Score).First();
            if (best.Request == null)
                return best;

            Move[] acts = moves.Where(move => move.Request != null).ToArray();
            if (_random.NextDouble() < spec.MistakeRate)
            {
                Move[] harmless = acts.Where(move => move.Score >= 0).ToArray();
                if (harmless.Length > 1)
                {
                    how = " · 실수";
                    return harmless[_random.Next(harmless.Length)];
                }
            }

            return acts.OrderByDescending(move => move.Score + (_random.NextDouble() * 2 - 1) * spec.Noise).First();
        }

        // 지금 둘 수 있는 수 전부와 점수 (7-1) — 기다림 · 뽑기 · 낼 수 있는 행동 카드 × 고를 수 있는 대상.
        private List<Move> Moves(int bot, BotMind mind)
        {
            var view = new BotView(_server, bot, mind.Spec, mind.Style, mind.Noticed);
            view.WorthOf = card => WorthOf(view, card);

            PlayerState me = view.Me;
            int hand = me.Hand.Count;
            var moves = new List<Move>();

            // 기다림 — 손패가 10장이면 선언하고(또는 찹츄를 지키며) 턴을 흘린다. 다른 수는 10장을 깨는 만큼 감점된다
            bool ten = hand == GameServer.ChapchuHandCount;
            moves.Add(new Move
            {
                Text = me.IsChapchu ? "찹츄 유지 — 기다림" : ten ? "손패 10장 — 선언하고 기다림" : "아무것도 하지 않음",
                Score = ten ? 0 : IdleScore
            });

            // 뽑기 — 2장, 손패가 9장이면 1장 (05-deck 4절). 못 뽑는 장은 빈 덱 피해
            view.Notes.Clear();
            int want = hand == GameServer.ChapchuHandCount - 1 ? 1 : GameServer.TurnDrawCount;
            int drawn = Math.Min(want, _server.DrawableCount);
            double drawScore = view.OwnHandScore(hand + drawn) - (want - drawn) * EmptyDrawPenalty;
            moves.Add(new Move { Text = $"뽑기 {want}장{NotesOf(view)}", Score = drawScore, Request = () => _server.Draw(bot) });

            if (me.ActionBlocked)
                return moves; // 107 짖기 — 이번 턴은 행동 카드를 쓸 수 없다 (방장 검증과 같은 기준)

            // 카드 — 같은 종류가 여러 장이면 손패 앞쪽 한 장만 본다
            var seen = new HashSet<int>();
            foreach (Card card in me.Hand)
            {
                if (card.Type != CardType.Action || !seen.Add(card.CardId))
                    continue;

                int[][] options = ReceiverOptions(view, card).ToArray();
                if (card.Target == TargetType.MostHand && options.Length > 1)
                {
                    // 118 무야호 — 동률이면 방장이 무작위로 고른다 (11-card-list 118). 동률인 사람마다 점수를 내 평균한 수 하나 (18-bot.md 7-2)
                    var notes = new List<string>();
                    double sum = 0;
                    bool usable = true;
                    foreach (int[] receivers in options)
                    {
                        view.Notes.Clear();
                        if (!TryScore(view, card, receivers, out double each, out int eachGain))
                        {
                            usable = false;
                            break;
                        }

                        sum += each + view.OwnHandScore(hand - 1 + eachGain);
                        notes.AddRange(view.Notes);
                    }

                    if (usable)
                    {
                        view.Notes.Clear();
                        view.Notes.AddRange(notes);
                        int tiedInstanceId = card.InstanceId;
                        string tied = string.Join(" · ", options.Select(t => $"P{t[0]}"));
                        moves.Add(new Move { Text = $"카드 {card.CardId} (#{tiedInstanceId}) → {tied} 중 무작위{NotesOf(view)}", Score = sum / options.Length, Request = () => _server.PlayCard(bot, tiedInstanceId, Array.Empty<int>()) });
                    }
                    continue;
                }

                foreach (int[] receivers in options)
                {
                    view.Notes.Clear();
                    if (!TryScore(view, card, receivers, out double score, out int gain))
                        continue;

                    // 내가 고른 상대가 나를 노렸던 사람이면 되갚는다 (7-8)
                    bool picked = card.Target == TargetType.OneOther || card.Target == TargetType.OneAny;
                    if (picked && score > 0 && receivers[0] != bot && mind.Grudge.TryGetValue(receivers[0], out int grudge) && grudge > 0)
                    {
                        score += Math.Min(grudge, GrudgeCounted) * GrudgeScore;
                        view.Notes.Add("보복");
                    }

                    score += view.OwnHandScore(hand - 1 + gain);

                    int instanceId = card.InstanceId;
                    int[] request = picked ? receivers : Array.Empty<int>(); // 자동 대상은 방장이 정한다
                    string to = picked ? " → " + string.Join(", ", receivers.Select(t => $"P{t}")) : "";
                    moves.Add(new Move { Text = $"카드 {card.CardId} (#{instanceId}){to}{NotesOf(view)}", Score = score, Request = () => _server.PlayCard(bot, instanceId, request) });
                }
            }

            return moves;
        }

        private static string NotesOf(BotView view) => view.Notes.Count > 0 ? " [" + string.Join(" · ", view.Notes.Distinct()) + "]" : "";

        // 이 카드를 receivers 에게 쓸 때의 효과 점수 합과 내 손패가 느는 장수. 봇이 쓰지 않는 효과가 있거나 카드별 조건(7-3)에 안 맞으면 false.
        private bool TryScore(BotView view, Card card, int[] receivers, out double score, out int gain)
        {
            score = 0;
            gain = 0;
            if (card.Effects.Count == 0)
                return false;

            foreach (CardEffect effect in card.Effects)
            {
                double value = effect.BotScore(view, receivers);
                if (double.IsNaN(value) || !effect.BotWants(_server, view.Bot, receivers))
                    return false;

                score += value;
                gain += effect.BotHandGain(view, receivers);
            }

            return true;
        }

        // 그 카드가 지금 내 손패에 있다면 낼 수 있는 가장 높은 효과 점수 — 버림 더미 맨 위를 가져올 값어치 (109)
        private double WorthOf(BotView view, Card card)
        {
            double best = 0;
            foreach (int[] receivers in ReceiverOptions(view, card))
            {
                if (TryScore(view, card, receivers, out double score, out int gain))
                    best = Math.Max(best, score + gain * BotView.CardValue);
            }

            view.Notes.Clear();
            return best;
        }

        // 카드의 대상이 될 사람들 — 고르는 카드는 후보마다 하나씩, 자동 대상은 방장이 정할 그대로 하나(118 동률이면 동률인 사람마다). 낼 수 없으면 비어 있다.
        // 대상 후보 조건(11-card-list 1-2)은 방장 검증과 같은 기준으로 먼저 거른다. TwoOthers 는 쓰지 않는다 (7-3).
        private IEnumerable<int[]> ReceiverOptions(BotView view, Card card)
        {
            int bot = view.Bot;
            int[] inGame = _server.ActorsInGame.ToArray();
            int[] others = inGame.Where(a => a != bot).ToArray();
            int myHandAfter = view.MyHand - 1;

            switch (card.Target)
            {
                case TargetType.OneOther:
                    foreach (int actor in view.Opponents.Where(a => _server.MeetsTargetCondition(a, card.TargetCondition)))
                        yield return new[] { actor };
                    break;

                case TargetType.OneAny:
                    foreach (int actor in view.Opponents.Prepend(bot).Where(a => _server.MeetsTargetCondition(a, card.TargetCondition)))
                        yield return new[] { actor };
                    break;

                case TargetType.AllOthers:
                    if (others.Length > 0)
                        yield return others;
                    break;

                case TargetType.Everyone:
                    yield return inGame;
                    break;

                case TargetType.Self:
                    yield return new[] { bot };
                    break;

                case TargetType.None:
                    yield return Array.Empty<int>();
                    break;

                case TargetType.ChapchuPlayers: // 나도 찹츄면 나도 맞는다 — 찹츄 상대가 있고 나는 아닐 때만
                case TargetType.ChapchuOthers:
                    int[] chapchu = others.Where(view.SeesChapchu).ToArray(); // 알아챈 찹츄 상대만 (7-7)
                    if (chapchu.Length > 0 && !view.Me.IsChapchu)
                        yield return chapchu;
                    break;

                case TargetType.MostHand:
                    // 손패 최다 1명 (낸 뒤의 장수). 내가 최다이거나 동률이면 내가 대상이 될 수 있어 쓰지 않는다.
                    // 상대끼리 동률이면 방장이 무작위로 고르므로 동률인 사람마다 하나씩 — Moves 가 평균을 낸다
                    int[] alive = view.Opponents;
                    if (alive.Length > 0)
                    {
                        int most = alive.Max(_server.HandCountOf);
                        if (most > myHandAfter)
                        {
                            foreach (int actor in alive.Where(a => _server.HandCountOf(a) == most))
                                yield return new[] { actor };
                        }
                    }
                    break;

                case TargetType.NextPlayer:
                    int next = _server.NextActorAfter(bot);
                    if (next != bot)
                        yield return new[] { next };
                    break;

                case TargetType.MoreHandThanMe:
                    int[] more = others.Where(a => _server.HandCountOf(a) > myHandAfter).ToArray();
                    if (more.Length > 0)
                        yield return more;
                    break;
            }
        }

        #endregion

        #region 함정 · 반응 (7-4)

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
            BotMind mind = _minds[bot];
            if (!_server.InGame(bot) || !TryFindReaction(bot, now, out Reaction reaction))
            {
                mind.ReactKey = null;
                return;
            }

            if (mind.ReactKey != reaction.Key)
            {
                mind.ReactKey = reaction.Key;
                mind.ReactAt = Math.Max(now, reaction.OpensAt) + ReactMin + _random.NextDouble() * (ReactMax - ReactMin);
                mind.Reacted = false;
            }

            if (mind.Reacted || now < mind.ReactAt)
                return;

            mind.Reacted = true;
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
                reaction = new Reaction($"choice:{_server.ChoiceDeadline}", now, $"7-4 고르기 → {(option == 0 ? "손패 전부 버리기" : "피해 받기")}", () => _server.Choose(bot, option));
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

        // 7-4 — 대상을 고르는 카운터 · 함정의 대상: 나를 뺀 생존자를 찹츄 → 손패 많은 → HP 낮은 순으로. 모두 같으면 무작위.
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

        #endregion

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
