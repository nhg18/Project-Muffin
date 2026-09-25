namespace Chapchu.Practice
{
    /// <summary>
    /// 게임 상태 조회(읽기 전용). UI가 처음 켜질 때 이벤트를 놓쳐도 현재 상태를 그릴 수 있게 한다.
    /// </summary>
    public interface IGameState
    {
        /// <summary>현재 턴 주인의 actorNumber. 아직 시작 전이면 -1.</summary>
        int CurrentTurnActor { get; }
    }
}
