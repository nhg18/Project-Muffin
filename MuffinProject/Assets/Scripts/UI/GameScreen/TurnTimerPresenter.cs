using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 타이머 값을 모든 스톱워치(내 캐릭터 옆 · 상대 좌석 3개)에 넣는다 (10-ui.md 2-1 HUD v2 「턴 타이머」 · 03-turn.md 9절).
    /// 남은 초는 턴 마감 시각으로부터 매 프레임 계산한다 (09-network.md 8절 — 표시 전용, 판정 없음). 턴 주인은 GameScreenPresenter 가 연출 큐에서 넘긴다.
    /// 어느 스톱워치가 보이는지는 좌석 · 내 패널 뷰가 턴 표시와 같이 켜고 끈다 — 여기서는 값만 넣는다. 늦게 열렸을 때는 Start 에서 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// </summary>
    public class TurnTimerPresenter : MonoBehaviour
    {
        [Tooltip("스톱워치 전부 — 내 것 + 상대 좌석 3개. 값은 모두에게 같이 넣는다.")]
        [SerializeField] private TurnTimerView[] timers;
        [Tooltip("턴 제한 시간(초) — 링의 전체 길이. 03-turn.md 15초, GameServer 와 같게.")]
        [SerializeField] private float turnSeconds = 15f;

        private double _turnDeadline = -1; // 서버 시각(PhotonNetwork.Time, 초). 음수면 아직 턴이 없다

        private void Awake()
        {
            if (timers == null || timers.Length == 0 || System.Array.Exists(timers, t => t == null))
                Debug.LogError($"[{nameof(TurnTimerPresenter)}] {nameof(timers)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void Start()
        {
            if (!PhotonNetwork.InRoom) return; // 에디터에서 씬을 직접 열었을 때

            Hashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            if (room.TryGetValue(RoomProps.TurnDeadline, out object deadline) && deadline is double deadlineValue)
                ShowTurn(deadlineValue);
        }

        private void Update()
        {
            if (_turnDeadline < 0) return;

            float remaining = Mathf.Max(0f, (float)(_turnDeadline - PhotonNetwork.Time));
            int seconds = Mathf.CeilToInt(remaining);
            float fraction = turnSeconds > 0f ? remaining / turnSeconds : 0f;
            // 링이 매 프레임 부드럽게 줄어들도록 매 프레임 넣는다 (글자는 초가 바뀔 때만 달라진다)
            foreach (TurnTimerView timer in timers)
                timer.SetRemaining(seconds, fraction, false);
        }

        /// <summary>턴이 바뀌었다 (OnTurnStarted) — 새 마감 시각.</summary>
        public void ShowTurn(double deadline) => _turnDeadline = deadline;
    }
}
