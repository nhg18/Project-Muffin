using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 방 코드를 Photon 에서 읽어 <see cref="RoomCodeView"/> 에 넣는다.
    /// 방 코드는 방 이름 그대로이고 방장 판정 대상이 아니라 게임 상태를 거치지 않는다 (10-ui.md 2절).
    /// 게임 씬은 방 안에서 LoadLevel 로만 열리므로 Start 시점에 이미 InRoom 이다 — RoomPresenter 와 달리 OnJoinedRoom 을 기다리지 않는다.
    /// </summary>
    [RequireComponent(typeof(RoomCodeView))]
    public class RoomCodePresenter : MonoBehaviour
    {
        // 에디터에서 씬을 직접 열었을 때 (방 없음)
        private const string NotInRoomText = "----";

        private RoomCodeView _view;

        private void Awake()
        {
            _view = GetComponent<RoomCodeView>();
        }

        private void Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                _view.SetRoomCode(NotInRoomText);
                return;
            }

            string code = PhotonNetwork.CurrentRoom.Name;
            _view.SetRoomCode(code);
            Debug.Log($"[{nameof(RoomCodePresenter)}] 방 코드 {code}", this);
        }
    }
}
