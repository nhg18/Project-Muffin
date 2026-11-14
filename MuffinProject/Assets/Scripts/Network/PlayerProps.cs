namespace Chapchu.Network
{
    /// <summary>
    /// Player CustomProperties 키 (09-network.md 6절). 모두가 봐도 되는 작은 값의 "지금 값" 스냅샷만 둔다.
    /// </summary>
    public static class PlayerProps
    {
        public const string Hp = "hp";
        public const string HandCount = "handCount";
        public const string TrapCount = "trapCount";
        public const string LifeState = "lifeState";
        public const string ChapChu = "chapChu";
        public const string ActionBlock = "actionBlock"; // 행동 카드 금지 (byte = Game.ActionBlock — 107 짖기). 없으면 키를 지운다
    }
}
