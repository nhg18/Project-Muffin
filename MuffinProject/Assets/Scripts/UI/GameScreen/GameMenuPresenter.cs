using System.Collections;
using Chapchu.Core;
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
    /// 끊김(튕김) 뒤 재접속은 이후 작업 — 그때 이 흐름(스스로 나가기)과 끊김을 구분한다 (02-player.md 6절 미정).
    /// </summary>
    [RequireComponent(typeof(GameMenuView))]
    public class GameMenuPresenter : MonoBehaviour
    {
        // 22-popup-ui.md 3절 「3턴 연속 시간 초과로 강제 퇴장된 뒤 로비 도착」
        private const string ForcedOutTitle = "방에서 나왔어요";
        private const string HaltedTitle = "방으로 돌아왔어요";
        private const string HaltedText = "게임을 이어갈 수 없어 방으로 돌아왔어요.";
        private const string ForcedOutText = "3턴 동안 행동하지 않아 게임에서 나왔어요.";

        private GameMenuView _view;

        private void Awake()
        {
            _view = GetComponent<GameMenuView>();
        }

        private void OnEnable()
        {
            _view.ExitConfirmed += HandleExitConfirmed;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
            GameEvents.OnPlayerLeftGame += HandlePlayerLeftGame;
            GameEvents.OnGameHalted += HandleGameHalted;
        }

        private void OnDisable()
        {
            _view.ExitConfirmed -= HandleExitConfirmed;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
            GameEvents.OnPlayerLeftGame -= HandlePlayerLeftGame;
            GameEvents.OnGameHalted -= HandleGameHalted;
        }

        // 방장이 사라졌는데 판을 이어받지 못했다 — 새 방장이 곧 전원을 방으로 옮긴다. 도착해서 사유를 알린다 (09-network.md 9.1 · 22-popup-ui.md)
        private void HandleGameHalted()
        {
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowWarningOnNextScene(HaltedTitle, HaltedText);
        }

        // 방장이 나를 강제 퇴장시켰다 — 규칙에서는 이미 빠졌다. 방을 나가 로비로 가고, 도착해서 사유를 알린다.
        private void HandlePlayerLeftGame(int actorNumber, int[] cardIds, int discardCount, bool forced)
        {
            if (!forced || NetworkManager.Instance == null || PhotonNetwork.LocalPlayer == null || actorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            Debug.Log($"[{nameof(GameMenuPresenter)}] 3턴 연속 시간 초과로 강제 퇴장 — 방을 나간다", this);
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowWarningOnNextScene(ForcedOutTitle, ForcedOutText);
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
            if (NetworkManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(GameMenuPresenter)}] {nameof(NetworkManager)} 가 없다 — 방 밖에서 열린 씬. 바로 로비로.", this);
                SceneManager.LoadScene(ScenePaths.Lobby);
                return;
            }

            Debug.Log($"[{nameof(GameMenuPresenter)}] 게임 나가기 요청 — 방을 나간다", this);
            NetworkManager.Instance.LeaveRoom();
        }

        private void HandleLeftRoom() => SceneManager.LoadScene(ScenePaths.Lobby);
    }
}
