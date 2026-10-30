namespace Chapchu.Game.Core
{
    /// <summary>
    /// 전달 계층 (멀티: PhotonMatchTransport, 로컬: 나중에 LocalMatchTransport).
    /// 화면은 이것만 알고 요청 · 결과 구독을 한다 — 멀티인지 로컬인지 모른다.
    /// </summary>
    public interface IMatchTransport : IMatchRequests
    {
        /// <summary>이 기기 플레이어의 ID. 결과가 "내 것"인지 구분할 때 쓴다.</summary>
        int LocalPlayerId { get; }

        /// <summary>이 기기에서 결과를 받을 쪽을 등록한다. Start 에서 부른다.</summary>
        void AddListener(IMatchEvents listener);
    }
}
