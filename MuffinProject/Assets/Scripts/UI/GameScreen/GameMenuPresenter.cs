using Chapchu.Core;
using Chapchu.Network;
using Chapchu.Network.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 「게임 나가기」 — 방을 나가고(NetworkManager) 나간 것이 확정되면(RoomEvents.OnLeftRoom) 로비로 (08-room.md 6절, 2026-11-07 사용자 결정: 나가기는 항상 로비).
    /// 끊김(튕김) 뒤 재접속은 이후 작업 — 그때 이 흐름(스스로 나가기)과 끊김을 구분한다 (02-player.md 6절 미정).
    /// </summary>
    [RequireComponent(typeof(GameMenuView))]
    public class GameMenuPresenter : MonoBehaviour
    {
        private GameMenuView _view;

        private void Awake()
        {
            _view = GetComponent<GameMenuView>();
        }

        private void OnEnable()
        {
            _view.ExitConfirmed += HandleExitConfirmed;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
        }

        private void OnDisable()
        {
            _view.ExitConfirmed -= HandleExitConfirmed;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
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
