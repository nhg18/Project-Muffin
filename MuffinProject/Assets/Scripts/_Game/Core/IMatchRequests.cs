namespace Chapchu.Game.Core
{
    /// <summary>
    /// 이 기기의 플레이어가 보내는 요청(의도). 화면은 이것만 부르고, 멀티인지 로컬인지 모른다.
    /// 보낸 사람은 넣지 않는다 — 클라가 남인 척할 수 없게, 전달 계층이 실제 보낸 사람을 붙여 MatchHost 에 넘긴다.
    /// </summary>
    public interface IMatchRequests
    {
        /// <summary>메인 행동: 드로우 1장.</summary>
        void Draw();
    }
}
