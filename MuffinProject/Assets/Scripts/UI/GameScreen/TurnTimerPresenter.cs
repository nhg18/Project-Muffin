using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 남은 초를 모든 타이머(내 캐릭터 옆 · 상대 좌석 3개의 스톱워치 + 턴 순서 이름표의 배지)에 넣는다 (통합 화면 UI v3 · 03-turn.md 9절).
    /// 남은 초는 턴 마감 시각으로부터 매 프레임 계산한다 (09-network.md 8절 — 표시 전용, 판정 없음). 턴 주인은 GameScreenPresenter 가 연출 큐에서 넘긴다.
    /// 체인 · 함정으로 멈췄던 턴이 이어지면(OnTurnResumed) 새 마감으로 다시 센다.
    /// 어느 타이머가 보이는지는 좌석 · 내 HUD · 턴 순서 뷰가 턴 표시와 같이 켜고 끈다 — 여기서는 값만 넣는다. 늦게 열렸을 때는 Start 에서 스냅샷을 한 번 읽는다 (09-network.md 6.1).
    /// 내가 아직 행동할 수 있는 내 턴이면 마지막 몇 초에 초마다 째깍 소리 (임시 효과음).
    /// </summary>
    public class TurnTimerPresenter : MonoBehaviour
    {
        [Tooltip("타이머 전부 — 스톱워치(내 것 + 상대 좌석 3개) + 턴 순서 배지. 값은 모두에게 같이 넣는다.")]
        [SerializeField] private TurnTimerView[] timers;

        [Tooltip("남은 초가 이 값 이하가 되면 초마다 째깍 (내 턴에만)")]
        [SerializeField] private int tickFromSeconds = 3;

        private double _turnDeadline = -1; // 게임 시각(GameClock, 초). 음수면 아직 턴이 없다
        private int _shownSeconds = -1;

        /// <summary>째깍 소리를 낼지 — 내 턴이고 아직 메인 행동 전 · 체인 밖일 때 (GameScreenPresenter 가 매 프레임 넣는다).</summary>
        public bool TickForMe { private get; set; }

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

            bool paused = GameClock.IsPaused; // 봇전에서 설정 판을 연 동안 멈춤 표시 (GameClock)
            // 개발자 도구 「턴 시간 무제한」 이면 마감이 아주 멀다 — 링은 가득, 글자는 제한 시간에 멈춘 채로 (GameServer.Dev)
            float remaining = Mathf.Clamp((float)(_turnDeadline - GameClock.Now), 0f, (float)GameServer.TurnTimeLimit);
            int seconds = Mathf.CeilToInt(remaining);
            float fraction = remaining / (float)GameServer.TurnTimeLimit; // 링의 전체 길이 = 방장 규칙의 제한 시간
            // 링이 매 프레임 부드럽게 줄어들도록 매 프레임 넣는다 (글자는 초가 바뀔 때만 달라진다)
            foreach (TurnTimerView timer in timers)
                timer.SetRemaining(seconds, fraction, paused);

            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            if (TickForMe && !paused && seconds >= 1 && seconds <= tickFromSeconds)
                SoundManager.Play(Sfx.TimerTick);
        }

        /// <summary>턴이 바뀌었거나 멈췄던 턴이 이어졌다 (OnTurnStarted · OnTurnResumed) — 새 마감 시각.</summary>
        public void ShowTurn(double deadline) => _turnDeadline = deadline;
    }
}
