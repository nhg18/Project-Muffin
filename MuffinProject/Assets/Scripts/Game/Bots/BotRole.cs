namespace Chapchu.Game.Bots
{
    /// <summary>
    /// 봇이 카드를 고를 때 쓰는 묶음 (18-bot.md 7-3). 카드마다가 아니라 효과 종류마다 정한다 — 효과가 자기 쓰임을 안다 (CardEffect.BotRole).
    /// 한 카드에 효과가 여럿이면 묶음도 여럿이다 (A19 = 공격 + 회복).
    /// </summary>
    public enum BotRole
    {
        None,       // 봇이 고르지 않는다 (ㅁ)
        Attack,     // ㄱ 상대 HP 를 줄인다
        Heal,       // ㄴ 내 HP 를 올린다
        HandChange, // ㄷ 상대 손패 장수를 바꾼다 (찹츄 해제용)
        Board       // ㅂ 판 전체를 바꾼다 — 효과마다 쓰는 조건이 있다 (112 대소동 · 121 개판 오분전)
    }
}
