using System.Collections;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.Popups;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 「게임 나가기」 — 방을 나가고(NetworkManager) 나간 것이 확정되면(RoomEvents.OnLeftRoom) 로비로 (08-room.md 6절, 2026-11-07 사용자 결정: 나가기는 항상 로비).
    /// 3턴 연속 시간 초과로 방장이 나를 내보내면 같은 길로 나가고, 로비에 도착해 경고 팝업을 띄운다 (03-turn.md 10절 · 22-popup-ui.md 3절).
    /// 끊김(튕김) 뒤 재접속은 이후 작업 — 그때 이 흐름(스스로 나가기)과 끊김을 구분한다 (02-player.md 6절 확정 · 09-network 재접속 절, 구현 전).
    /// </summary>
    [RequireComponent(typeof(GameMenuView))]
    public class GameMenuPresenter : MonoBehaviour
    {
        // 22-popup-ui.md 3절 「3턴 연속 시간 초과로 강제 퇴장된 뒤 로비 도착」
        private const string ForcedOutTitle = "방에서 나왔어요";
        private const string HaltedTitle = "방으로 돌아왔어요";
        private const string HaltedText = "게임을 이어갈 수 없어 방으로 돌아왔어요.";
        private const string ForcedOutTextFormat = "{0}턴 동안 행동하지 않아 게임에서 나왔어요."; // {0} = GameServer.TimeoutLimit (25 7-4 #3)

        private GameMenuView _view;

        private void Awake()
        {
            _view = GetComponent<GameMenuView>();
        }

        // 게임 화면에 들어감 (27 5-5) — 화면마다 한 컴포넌트가 맡는데, 게임 화면은 메뉴가 늘 있다
        private void Start() => Chapchu.Account.AnalyticsRecorder.RecordScreenViewed(Chapchu.Account.AnalyticsRecorder.ScreenName.Game);

        private void OnEnable()
        {
            _view.ExitConfirmed += HandleExitConfirmed;
            _view.OpenChanged += HandleMenuOpenChanged;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
        }

        private void OnDisable()
        {
            _view.ExitConfirmed -= HandleExitConfirmed;
            _view.OpenChanged -= HandleMenuOpenChanged;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
            GameClock.Resume(); // 씬을 떠난다 — 멈춘 채(timeScale 0) 다음 씬으로 가지 않게
        }

        // 봇전이면 설정 판 · 나가기 확인 팝업이 떠 있는 동안 게임을 멈춘다 — 사람이 둘 이상이면 GameClock 이 멈추지 않는다 (10-ui.md 9절, 베타 한정)
        private void HandleMenuOpenChanged(bool open)
        {
            if (open)
                GameClock.Pause();
            else
                GameClock.Resume();
        }

        // 아래 둘은 GameScreenPresenter(큐)가 알림을 받은 자리에서 바로 부른다 — 곧 씬이 바뀌므로 연출 큐를 기다리지 않는다 (CODE_CONVENTION 9절 「큐를 타지 않는 것」)

        // 방장이 사라졌는데 판을 이어받지 못했다 — 새 방장이 곧 전원을 방으로 옮긴다. 도착해서 사유를 알린다 (09-network.md 9.1 · 22-popup-ui.md)
        public void ShowGameHalted()
        {
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowWarningOnNextScene(HaltedTitle, HaltedText);
        }

        // 방장이 나를 강제 퇴장시켰다 — 규칙에서는 이미 빠졌다. 방을 나가 로비로 가고, 도착해서 사유를 알린다.
        public void ShowPlayerLeftGame(int actorNumber, bool forced)
        {
            if (!forced || NetworkManager.Instance == null || PhotonNetwork.LocalPlayer == null || actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            Debug.Log($"[{nameof(GameMenuPresenter)}] {GameServer.TimeoutLimit}턴 연속 시간 초과로 강제 퇴장 — 방을 나간다", this);
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowWarningOnNextScene(ForcedOutTitle, string.Format(ForcedOutTextFormat, GameServer.TimeoutLimit));
            StartCoroutine(LeaveRoomNextFrame());
        }

        // 이 알림은 방장의 Send 묶음 도중에 온다 (방장 자신이 대상이면 그 자리에서 실행된다).
        // 바로 나가면 같은 묶음의 뒤 알림(턴 시작 · 게임 종료)이 남은 사람에게 가지 않으므로 묶음이 끝난 다음 프레임에 나간다.
        private IEnumerator LeaveRoomNextFrame()
        {
            yield return null;
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LeaveRoom();
        }

        private void HandleExitConfirmed()
        {
            GameClock.Resume(); // 나간다 — 화면 전환 연출이 멈춘 시간(timeScale 0)에 서지 않게
            if (NetworkManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(GameMenuPresenter)}] {nameof(NetworkManager)} 가 없다 — 방 밖에서 열린 씬. 바로 로비로.", this);
                SceneManager.LoadScene(ScenePaths.Lobby);
                return;
            }

            Debug.Log($"[{nameof(GameMenuPresenter)}] 게임 나가기 요청 — 방을 나간다", this);
            NetworkManager.Instance.LeaveRoom();
        }

        private void HandleLeftRoom() => ScreenTransition.Play(ScenePaths.Lobby, () => SceneManager.LoadScene(ScenePaths.Lobby));
    }
}
