using Chapchu.Core;

namespace Chapchu.Game
{
    // GameServer.ChapChu.cs — 찹츄 선언 (07-win-condition.md 4절). 승리 판정(4.3)은 기능 8 의 3단계에서 붙인다.
    // 선언은 한 번 되면 유지된다 — 손패가 10장이 아니게 돼도 해제하지 않는다 (2026-10-11 사용자 결정. 07-win-condition.md 4.2 "즉시 해제" 는 문서 수정 필요).
    public partial class GameServer
    {
        // 확정(07-win-condition.md 4.1). 선언 · 유지 모두 "정확히 10장".
        private const int ChapChuHandCount = 10;

        /// <summary>
        /// 찹츄 선언 요청. 자신의 턴이고 손패가 정확히 10장이면 찹츄 상태로 바꾸고 전원에게 공개한다. 자동 선언은 없다 — 버튼을 눌러야만 온다.
        /// 조건은 요청 시점만 본다 — 같은 턴 안에서 장수가 바뀌었더라도 지금 10장이면 된다.
        /// </summary>
        public void DeclareChapChu(int requester)
        {
            // 자신의 턴에만 (07-win-condition.md 4.1 — 2026-10-11 "자신의 턴" 으로 확정, 문서 반영 필요)
            if (requester != CurrentTurnActor)
            {
                _outbox.Reject(requester, RejectCode.NotYourTurn);
                return;
            }

            PlayerState player = _players[requester];

            if (player.Hand.Count != ChapChuHandCount)
            {
                _outbox.Reject(requester, RejectCode.HandNotTen);
                return;
            }

            if (player.IsChapChu)
            {
                _outbox.Reject(requester, RejectCode.AlreadyChapChu);
                return;
            }

            SetChapChu(requester, true);
        }

        // 찹츄 상태는 여기서만 바꾼다 — 원본 변경과 공개가 항상 같이 나간다.
        private void SetChapChu(int actor, bool isChapChu)
        {
            _players[actor].IsChapChu = isChapChu;
            _outbox.SetPlayerState(actor, PlayerProps.ChapChu, isChapChu);
        }
    }
}
