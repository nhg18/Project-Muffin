namespace Chapchu.Game
{

    /// <summary>
    /// UI → 마스터 단방향 요청. 판정은 하지 않고 의도만 전달한다.
    /// 결과는 <see cref="GameEvents"/> 로만 돌아온다.
    /// 요청자는 구현체가 PhotonMessageInfo.Sender 로 식별하므로 인자로 받지 않는다.
    /// </summary>
    public interface IGameRequests
    {
        void RequestDraw();
        void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers);

        /// <summary>
        /// 카운터 카드 사용 (04-card.md 7절). 내 턴이 아니어도 반응 시간(5초) 안이면 낼 수 있다.
        /// targetCardInstanceId = 반응할 체인 카드 (지금 체인 맨 위). 그새 다른 카운터가 먼저 올라가 맨 위가 바뀌었으면 거절된다.
        /// </summary>
        void RequestCounter(int cardInstanceId, int targetCardInstanceId);
        void RequestSetTrap(int cardInstanceId, int slotIndex);
        void RequestDeclareChapChu();
    }
}
