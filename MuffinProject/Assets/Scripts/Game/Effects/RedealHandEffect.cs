using System;
using System.Linq;

namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 받는 사람마다 손패를 전부 버리고 시작 손패 장수만큼 새로 뽑는다 (RedealHand — 113 기억 삭제). HP · 설치 함정은 그대로.
    /// 교환 · 재분배라 훔침 · 버림에 반응하는 카드의 대상이 아니다 (11-card-list 1-2) — 반응 조건 일(Tag)이 없다.
    /// 봇은 상대 손패를 바꾸는 카드로 본다 — 찹츄 상대가 있거나 받는 사람의 평균 손패가 시작 장수보다 많을 때만 (18-bot.md 7-3).
    /// </summary>
    [Serializable]
    public sealed class RedealHandEffect : CardEffect
    {
        public EffectSubject subject;

        internal override Bots.BotRole BotRole => subject == EffectSubject.Target ? Bots.BotRole.HandChange : Bots.BotRole.None;

        internal override bool BotWants(GameServer server, int bot, int[] targets)
        {
            // 113 은 자동 대상(AllOthers)이라 봇 단계의 targets 는 비어 있다 — 받는 사람(게임에 남은 상대)을 서버 상태에서 구한다.
            int[] receivers = server.ActorsInGame.Where(actor => actor != bot).ToArray();
            if (receivers.Length == 0)
                return false;
            return receivers.Any(actor => server.StateOf(actor).IsChapchu)
                || receivers.Average(actor => (double)server.HandCountOf(actor)) > GameServer.InitialHandCount;
        }

        internal override void Apply(GameServer server, ChainEntry entry)
        {
            foreach (int actor in server.ReceiversOf(subject, entry))
                server.RedealHand(actor);
        }
    }
}
