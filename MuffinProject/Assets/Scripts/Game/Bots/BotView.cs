using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Bots
{
    /// <summary>
    /// 봇 한 명이 지금 판을 보는 눈 (18-bot.md 7-0 · 7-1). 카드 효과(CardEffect.BotScore)가 자기 점수를 낼 때 이 값들을 쓴다.
    /// 공개 정보만 본다 — 모두의 HP · 손패 장수 · 찹츄 상태 · 버림 더미 맨 위 · 턴 순서, 그리고 자기 손패. 남의 손패 내용 · 덱 순서는 읽지 않는다.
    /// 점수 1 = 상대 HP 1. 한 번의 판단(턴)마다 새로 만든다.
    /// </summary>
    internal sealed class BotView
    {
        // 18-bot.md 4절 「점수 값」
        public const double CardValue = 8;        // 카드 1장
        public const double KillBonus = 60;       // 상대를 쓰러뜨림
        public const double BreakChapchu = 300;   // 찹츄 상태인 상대의 10장을 깸 (또는 그 상대를 쓰러뜨림)
        public const double ChapchuReady = 100;   // 내 손패가 정확히 10장 — 선언할 수 있다
        public const double StyleBonus = 1.25;    // 성향이 좋아하는 일의 배수
        private const double LostCardValue = 4;   // 상대가 카드 1장을 잃음
        private const double WeakFocus = 0.1;     // 잃은 HP 1 마다 — 약한 상대를 먼저 노리는 정도

        // 18-bot.md 7-1 「내가 받는 피해」 · 「회복」, 7-3 「행동 카드 금지」 — HP 기준
        private const int LowHp = 30;             // 이 미만이면 위험 — 자해 −20 · 회복 × 1.5
        private const int MidHp = 50, HighHp = 70; // 회복 × 1.0 · × 0.6 의 경계
        public const int GuardHp = 40;            // 107 짖기 — 내 HP 가 이 이하면 몸을 지키는 값을 더한다

        public readonly GameServer Server;
        public readonly int Bot;
        public readonly BotLevelSpec Level;
        public readonly BotStyle Style;

        /// <summary>이 수의 점수에 크게 든 이유 — 로그용 (7-6). 수 하나를 채점하기 전에 비운다.</summary>
        public readonly List<string> Notes = new List<string>();

        /// <summary>그 카드가 지금 내 손패에 있다면 낼 수 있는 가장 높은 효과 점수 (109 가 버림 더미 맨 위를 볼 때). 봇 판단기가 넣는다.</summary>
        public Func<Card, double> WorthOf = _ => 0;

        private readonly HashSet<int> _noticed; // 이번 턴에 찹츄를 알아챈 상대 (7-7 「찹츄 알아챔」)

        public BotView(GameServer server, int bot, BotLevelSpec level, BotStyle style, HashSet<int> noticed)
        {
            Server = server;
            Bot = bot;
            Level = level;
            Style = style;
            _noticed = noticed;
        }

        public PlayerState Me => Server.StateOf(Bot);
        public int MyHp => Me.Hp;
        public int MyHand => Me.Hand.Count;

        /// <summary>나를 뺀 생존자 (턴 순서대로).</summary>
        public int[] Opponents => Server.ActorsInGame.Where(a => a != Bot && Server.StateOf(a).Life == LifeState.Alive).ToArray();

        /// <summary>그 상대가 찹츄 상태이고, 내가 이번 턴에 그것을 알아챘다.</summary>
        public bool SeesChapchu(int actor) => actor != Bot && Server.StateOf(actor).IsChapchu && _noticed.Contains(actor);

        public bool SeesAnyChapchu => Opponents.Any(SeesChapchu);

        /// <summary>
        /// 찹츄 상태가 아닌 상대의 손패가 hand 장일 때 그 상대가 찹츄에 얼마나 가까운가 (7-1 「상대 손패」).
        /// 10장 = 자기 턴에 선언 · 8 ~ 9장 = 한 번 뽑으면 10장 · 11장 = 한 장 내면 10장 · 7장 = 두 번 뽑으면.
        /// </summary>
        public double NearChapchu(int hand)
        {
            const int ten = GameServer.ChapchuHandCount;
            double near = hand == ten ? 50 : hand == ten - 1 || hand == ten - 2 ? 35 : hand == ten + 1 ? 20 : hand == ten - 3 ? 12 : 0;
            return near * Level.Foresight;
        }

        /// <summary>상대 actor 의 손패가 delta 장 바뀔 때의 점수 — 잃으면 +, 찹츄에서 멀어지면 +, 찹츄(또는 10장)에 닿게 해 주면 −.</summary>
        public double HandScore(int actor, int delta)
        {
            if (actor == Bot)
                return 0; // 내 손패는 OwnHandScore 로

            int hand = Server.HandCountOf(actor);
            int after = Math.Max(0, hand + delta);
            if (after == hand)
                return 0;

            bool chapchu = SeesChapchu(actor);
            if (chapchu)
                Notes.Add("찹츄 깨기");

            double before = chapchu ? BreakChapchu : NearChapchu(hand);
            double score = before - NearChapchu(after) + (hand - after) * LostCardValue;
            return score * (Style == BotStyle.Disruptor ? StyleBonus : 1);
        }

        /// <summary>actor 가 amount 피해를 받을 때의 점수. 쓰러뜨리면 KillBonus + 그 상대의 찹츄 위협이 사라진 만큼. 내가 받으면 SelfDamageScore.</summary>
        public double DamageScore(int actor, int amount)
        {
            if (actor == Bot)
                return SelfDamageScore(amount);

            int hp = Server.PlayerHp(actor);
            int dealt = Math.Min(amount, hp);
            if (dealt <= 0)
                return 0;

            double score = dealt;
            if (Level.SeesKill)
            {
                // 보통은 약한 상대부터, 어려움은 HP 가 가장 높은 1등부터 (7-7 「1등 견제」)
                score += Level.LeaderFocus > 0 ? hp * Level.LeaderFocus : (GameServer.MaxHp - hp) * WeakFocus;
                if (hp <= amount)
                {
                    Notes.Add("쓰러뜨림");
                    score += KillBonus + (SeesChapchu(actor) ? BreakChapchu : NearChapchu(Server.HandCountOf(actor)));
                }
            }

            return score * (Style == BotStyle.Fighter ? StyleBonus : 1);
        }

        /// <summary>내가 amount 피해를 받을 때의 점수 (−). 그 피해로 쓰러지면 쓰지 않을 만큼 낮다.</summary>
        public double SelfDamageScore(int amount)
        {
            if (amount <= 0)
                return 0;
            if (MyHp <= amount)
                return -1000;

            return -(amount * 1.2 + (MyHp - amount < LowHp ? 20 : 0));
        }

        /// <summary>actor 가 amount 회복할 때의 점수. 내 회복은 HP 가 낮을수록 값지고, 상대의 회복은 손해다.</summary>
        public double HealScore(int actor, int amount)
        {
            int hp = Server.PlayerHp(actor);
            int healed = Math.Min(amount, GameServer.MaxHp - hp);
            if (healed <= 0)
                return 0;
            if (actor != Bot)
                return -healed;

            double weight = hp < LowHp ? 1.5 : hp < MidHp ? 1.0 : hp < HighHp ? 0.6 : 0.3;
            return healed * weight;
        }

        /// <summary>
        /// 내 손패 hand 장의 값어치 (7-1 「내 손패」). 장당 CardValue + 정확히 10장이면 ChapchuReady, 8 ~ 9장은 다음 턴에 10장이 되므로 조금 더,
        /// 10장을 넘으면 넘은 만큼 깎는다 (한 장씩 내서 10장으로 돌아와야 한다).
        /// </summary>
        public double HandPotential(int hand)
        {
            double bonus = hand == GameServer.ChapchuHandCount ? ChapchuReady
                : hand == GameServer.ChapchuHandCount - 1 ? 15 * Level.Foresight
                : hand == GameServer.ChapchuHandCount - 2 ? 10 * Level.Foresight
                : hand > GameServer.ChapchuHandCount ? -18 * (hand - GameServer.ChapchuHandCount)
                : 0;
            return hand * CardValue + bonus;
        }

        /// <summary>이 수를 둔 뒤 내 손패가 after 장이 될 때의 점수. 찹츄 상태를 스스로 깨면 더 깎는다.</summary>
        public double OwnHandScore(int after)
        {
            int before = MyHand;
            double score = HandPotential(after) - HandPotential(before);
            if (after == GameServer.ChapchuHandCount && before != after)
                Notes.Add("찹츄 준비");
            if (Me.IsChapchu && after != GameServer.ChapchuHandCount)
                score -= 50;

            return score > 0 && Style == BotStyle.Collector ? score * StyleBonus : score;
        }
    }
}
