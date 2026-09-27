namespace Chapchu.Game
{
    /// <summary>
    /// <see cref="GameServer"/> 가 결과를 내보내는 출구. GameServer 가 Photon 을 모르게 하려고 둔다 (구현: PunGameServer).
    /// 새 종류의 결과(예: 뽑은 카드 내용 — 주인에게만)가 생기면 메서드를 하나 추가한다.
    /// </summary>
    public interface IServerOutbox
    {
        /// <summary>공개 상태 (방). 키는 <c>RoomProps</c>. 늦게 켜진 화면도 읽을 수 있다.</summary>
        void SetRoomState(string key, object value);

        /// <summary>공개 상태 (플레이어). 키는 <c>PlayerProps</c>.</summary>
        void SetPlayerState(int actorNumber, string key, object value);

        /// <summary>요청 거절. 요청자에게만 보낸다.</summary>
        void Reject(int actorNumber, string reason);
    }
}
