using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>대상 1 의 찹츄를 지우고 대상 2 에게 부여 (MoveChapChu — A20, 고른 순서). 대상 1 이 찹츄가 아니면 아무 일도 없다. 대상 2 가 본인 무효(C15)면 부여만 빠진다.</summary>
    [Serializable]
    public sealed class MoveChapChuEffect : CardEffect
    {
        internal override EffectTag Tag => EffectTag.GiveChapchu;

        // 부여를 받는 사람 = 대상 2 (대상 1 이 지금 찹츄일 때만)
        internal override IEnumerable<int> Affected(GameServer server, ChainEntry entry)
        {
            if (entry.Targets.Length < 2 || !server.StateOf(entry.Targets[0]).IsChapchu || entry.Excluded.Contains(entry.Targets[1]))
                return Array.Empty<int>();
            return new[] { entry.Targets[1] };
        }

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (entry.Targets.Length < 2 || entry.Excluded.Contains(entry.Targets[0]))
                return;

            int from = entry.Targets[0], to = entry.Targets[1];
            if (entry.Excluded.Contains(to) || !server.InGame(to))
                server.ClearChapchuIfMoved(from);
            else
                server.MoveChapchu(from, to, entry.User);
        }
    }
}
