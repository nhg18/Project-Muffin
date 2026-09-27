namespace Chapchu.Game
{
    /// <summary>
    /// 게임 상태 조회(읽기 전용). UI 가 늦게 켜져 이벤트를 놓쳐도 현재 상태를 그릴 수 있게 한다.
    /// 값의 원본은 마스터(GameServer)이고, 여기서 읽는 것은 마스터가 전파한 사본이다. 판정에 쓰지 않는다.
    /// 계약 3종(PlayerProps/RoomProps · GameEvents · IGameRequests)과 같이 양쪽 리뷰 대상이다.
    /// </summary>
    public interface IGameState
    {
        /// <summary>현재 턴 주인의 actorNumber. 아직 시작 전이면 -1.</summary>
        int CurrentTurnActor { get; }
    }
}
