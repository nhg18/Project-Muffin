namespace Chapchu.Practice
{
    /// <summary>
    /// UI → 마스터(GameServer) 요청. 의도만 보내고 판정하지 않는다. 기능이 늘면 여기에 메서드를 추가한다.
    /// 결과는 <see cref="GameEvents"/> 로만 돌아온다.
    /// 요청자는 구현체가 식별하므로 인자로 받지 않는다.
    /// </summary>
    public interface IGameRequests
    {
        void RequestEndTurn();
    }
}
