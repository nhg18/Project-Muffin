using Photon.Pun;
using UnityEngine;

namespace Chapchu.Network
{
    /// <summary>
    /// 게임 시각(초) — 방장 규칙(GameServer · BotDriver)과 화면의 남은 초가 함께 읽는다. 평소에는 PhotonNetwork.Time 그대로다.
    /// 봇전(방 안의 사람이 나 하나)에서만 멈출 수 있다 (10-ui.md 9절 「설정 버튼」 — 베타 한정, PM 2026-11-28).
    /// 멈춘 동안은 시각이 서고, 다시 흐르면 멈춘 시간만큼 뺀 값을 돌려준다 — 턴 · 반응 · 고르기 마감과 봇의 생각 시간이 모두 남은 시간부터 이어진다.
    /// 연출도 같이 서도록 멈춘 동안 Time.timeScale = 0. 그동안 PUN 은 FixedUpdate 가 돌지 않아 받은 메시지를 처리하지 않으므로, LateUpdate 에서 처리하게 바꿔 둔다 (접속 유지).
    /// 사람이 둘 이상이면 멈추지 않는다 — 각 기기의 시각이 어긋난다.
    /// 상태가 static 인 이유는 CODE_CONVENTION 4절 예외.
    /// </summary>
    public static class GameClock
    {
        private static double _pausedAt = -1; // 멈춘 순간의 PhotonNetwork.Time. 음수면 흐르는 중
        private static double _pausedTotal;   // 이번 게임에서 멈춰 있던 시간의 합
        private static float _dispatchScale;  // 멈추기 전의 PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate

        public static bool IsPaused => _pausedAt >= 0;

        public static double Now => (IsPaused ? _pausedAt : PhotonNetwork.Time) - _pausedTotal;

        /// <summary>멈출 수 있다 — 방 안의 사람이 나 하나(나머지는 봇)이고 내가 방장. 게임 중에는 방이 닫혀 있어 새로 들어오는 사람이 없다.</summary>
        public static bool CanPause => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount == 1 && NetworkManager.IsTrustedMaster;

        public static void Pause()
        {
            if (IsPaused || !CanPause)
                return;

            _pausedAt = PhotonNetwork.Time;
            _dispatchScale = PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate;
            PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = 0f; // timeScale 0 이면 LateUpdate 에서 받은 메시지를 처리한다
            Time.timeScale = 0f;
            Debug.Log($"[{nameof(GameClock)}] 게임 멈춤");
        }

        public static void Resume()
        {
            if (!IsPaused)
                return;

            _pausedTotal += PhotonNetwork.Time - _pausedAt;
            _pausedAt = -1;
            Time.timeScale = 1f;
            PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = _dispatchScale;
            Debug.Log($"[{nameof(GameClock)}] 게임 이어짐 — 이번 게임에서 멈춘 시간 {_pausedTotal:0.0}초");
        }

        /// <summary>새 게임 씬 — 지난 판에 멈춘 시간을 비운다. 아직 아무도 게임 시각을 읽기 전에 (PunGameServer.Awake).</summary>
        public static void ResetForNewGame()
        {
            Resume();
            _pausedTotal = 0;
        }
    }
}
