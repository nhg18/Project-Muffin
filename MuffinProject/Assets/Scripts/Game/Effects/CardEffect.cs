using System;
using System.Collections.Generic;

namespace Chapchu.Game.Effects
{
    /// <summary>효과를 받는 사람. 한 카드 안에서도 효과마다 다르다 (예: A19 대상에게 피해 · 본인 회복, 11-card-list.md 5절).</summary>
    public enum EffectSubject
    {
        Target,     // 카드의 대상 (TargetType 으로 정해진 사람들)
        Self        // 카드를 쓴 사람
    }

    /// <summary>
    /// 카드 효과 하나 (11-card-list.md 5절 · CODE_CONVENTION 9절). 효과 종류마다 이 클래스를 상속하고,
    /// 카드 에셋(CardData)이 [SerializeReference] 목록으로 조립한다. 카드마다 클래스를 만들지 않는다.
    /// 에셋에는 클래스 이름 · 네임스페이스로 저장된다 — 바꾸면 그 효과가 에셋에서 사라진다.
    /// 한 인스턴스를 같은 종류의 카드 여러 장이 함께 쓴다. Apply 에서 필드를 바꾸지 않는다.
    /// </summary>
    [Serializable]
    public abstract class CardEffect
    {
        /// <summary>방장의 규칙이 체인을 처리할 때 부른다. entry = 체인에 오른 이 카드 (쓴 사람 · 확정된 대상).</summary>
        internal abstract void Apply(GameServer server, ChainEntry entry);

        /// <summary>카드가 체인에 오를 때(낼 때) 부른다 — 낼 때 정하는 값을 entry.Rolled 에 남긴다 (예: 116 뽑을 장수). 기본은 없음.</summary>
        internal virtual void Prepare(GameServer server, ChainEntry entry) { }

        /// <summary>봇이 이 카드를 이 대상에게 지금 쓸 만한가 (18-bot.md 7-3 조건). 기본은 언제나.</summary>
        internal virtual bool BotWants(GameServer server, int bot, int[] targets) => true;

        /// <summary>봇이 이 효과를 어떤 묶음으로 보는가 (18-bot.md 7-3). 새 효과는 자기 쓰임을 여기서 정한다.</summary>
        internal virtual Bots.BotRole BotRole => Bots.BotRole.None;

        /// <summary>이 효과가 하는 일 (반응 조건 판정용, 04-card.md 7절).</summary>
        internal virtual EffectTag Tag => EffectTag.None;

        /// <summary>이 효과가 Tag 의 일을 하는 사람 — 처리 전에 미리 본다 (예: 피해를 받을 사람).</summary>
        internal virtual IEnumerable<int> Affected(GameServer server, ChainEntry entry) => Array.Empty<int>();

        /// <summary>actor 가 이 효과로 받을 피해 (감소 · 전환 전) — 「그 피해로 사망」(C11) 판정용.</summary>
        internal virtual int PredictDamage(GameServer server, ChainEntry entry, int actor) => 0;
    }
}
