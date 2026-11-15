using System;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 턴 진행 방향을 반대로 (ReverseTurnOrder — 145 뒤로 돌아, 03-turn.md 6절 #4 · 7절). 다시 쓰면 원래 방향. 쌓인 턴 스킵은 그대로.
    /// 생존자가 2명이면 다음 사람을 건너뛰어 쓴 사람의 턴이 다시 온다. 봇은 둘만 남았을 때(한 턴 더) · 다음 차례가 찹츄 상대일 때(그 턴을 뒤로 미룬다) 쓴다 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class ReverseTurnOrderEffect : CardEffect
    {
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            int[] opponents = view.Opponents;
            if (opponents.Length == 1)
                return 28;

            return opponents.Length > 1 && view.SeesChapchu(view.Server.NextActorAfter(view.Bot)) ? 100 : 2;
        }

        internal override void Apply(GameServer server, ChainEntry entry) => server.ReverseTurnOrder(entry.User);
    }
}
