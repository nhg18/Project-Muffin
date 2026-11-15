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

        // 18-bot 7-1 — 내가 뽑는 정해진 장수는 BotHandGain 으로. 무작위 장수(116)는 나올 수 있는 장수마다의 내 손패 값을 평균한다. 상대가 뽑으면 그 상대 손패 점수
        internal override double BotScore(Bots.BotView view, int[] receivers)
        {
            if (useTriggerAmount)
                return double.NaN; // 함정 전용

            if (subject == EffectSubject.Target)
                return receivers.Sum(actor => view.HandScore(actor, count));

            if (randomMax <= count)
                return 0;

            int afterPlay = view.MyHand - 1;
            double sum = 0;
            for (int n = count; n <= randomMax; n++)
                sum += view.HandPotential(afterPlay + n);
            return sum / (randomMax - count + 1) - view.HandPotential(afterPlay);
        }

        internal override int BotHandGain(Bots.BotView view, int[] receivers) =>
            subject == EffectSubject.Self && !useTriggerAmount && randomMax <= count ? count : 0;

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
