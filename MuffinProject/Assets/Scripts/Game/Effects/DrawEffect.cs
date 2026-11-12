using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>받는 사람마다 덱에서 count 장 (Draw — A06 · A12 · A13 · C08 · T09). 메인 행동이 아니다. useTriggerAmount 면 함정의 사건 값(T09 버려진 장수)만큼.</summary>
    [Serializable]
    public sealed class DrawEffect : CardEffect
    {
        public EffectSubject subject;
        public int count;
        // 0 보다 크면 count ~ randomMax 사이 무작위 장수 — 카드를 낼 때 방장이 정한다 (116)
        public int randomMax;
        // 함정 전용 — 사건 값만큼 뽑는다 (T09 「버려진 장수만큼」). 켜면 count 는 쓰지 않는다.
        public bool useTriggerAmount;

        internal override void Prepare(GameServer server, ChainEntry entry)
        {
            if (randomMax > count)
                entry.Rolled[this] = server.RollRange(count, randomMax);
        }

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            int n = useTriggerAmount ? entry.TriggerAmount : entry.Rolled.TryGetValue(this, out int rolled) ? rolled : count;
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.DrawCards(actor, n, entry);
        }
    }
}
