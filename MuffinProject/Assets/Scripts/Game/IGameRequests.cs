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

        /// <summary>카드 사용 · 버림으로 손패에서 카드 1장이 빠졌음을 알린다 (장수 갱신용). 카드 사용은 RequestPlayCard.</summary>
        void RequestDiscard(int cardId);
        void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers);
        void RequestSetTrap(int cardInstanceId, int slotIndex);
        void RequestDeclareChapChu();
    }
}
