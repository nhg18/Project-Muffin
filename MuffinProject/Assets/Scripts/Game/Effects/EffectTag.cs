namespace Chapchu.Game.Effects
{
    /// <summary>
    /// 효과가 하는 일 — 카운터 · 함정의 반응 조건(04-card.md 7절)이 맨 위 카드를 볼 때 쓴다 (예: 「나에게 피해」 = Damage 를 받는 사람에 내가 있다).
    /// </summary>
    public enum EffectTag
    {
        None,
        Damage,
        Heal,
        TakeHand,    // 손패를 훔치거나 버리게 한다
        DestroyTrap,
        SkipTurn,
        GiveChapchu
    }
}
