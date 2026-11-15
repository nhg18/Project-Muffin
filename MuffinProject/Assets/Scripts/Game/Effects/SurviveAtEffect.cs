using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>HP hp 로 생존 (SurviveAt) — now 면 지금 (T17: 사망 대기를 끝내고, discardHandAndTraps 면 손패 · 다른 함정을 버림), 아니면 반응한 카드의 피해로 0 이 될 때 hp 로 남는다 (C11).</summary>
    [Serializable]
    public sealed class SurviveAtEffect : CardEffect
    {
        public int hp = 1;
        // 켜면 지금 바로 (T17), 끄면 반응한 카드의 피해에서 (C11)
        public bool now;
        // now 일 때 — 손패와 다른 설치 함정을 모두 버린다 (T17)
        public bool discardHandAndTraps;

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            if (now)
                server.SurviveNow(entry.User, hp, discardHandAndTraps);
            else
                server.MarkBelow(entry, below => below.SurviveAt[entry.User] = hp);
        }
    }
}
