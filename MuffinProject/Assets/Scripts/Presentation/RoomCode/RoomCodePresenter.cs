using Photon.Pun;
using UnityEngine;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 방 코드를 Photon 에서 읽어 뷰에 넣는다. Presentation/RoomCode 에서 네트워크를 아는 유일한 파일이다.
    /// 방 코드는 방 이름 그대로이고 마스터 판정 대상이 아니라 IGameState 를 거치지 않는다 (10-ui.md 2절).
    /// 게임 씬은 방 안에서 LoadLevel 로만 열리므로 Start 시점에 이미 InRoom 이다 — RoomPresenter 와 달리 OnJoinedRoom 을 기다리지 않는다.
    /// </summary>
    [RequireComponent(typeof(RoomCodeView))]
    public class RoomCodePresenter : MonoBehaviour
    {
        // 에디터에서 씬을 직접 열었거나 FakeGameServer 로 돌릴 때 (방 없음)
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
            // 멀티 테스트 확인용 (CLAUDE.md 12절: 확인 로그는 Start 에서)
            Debug.Log($"[{nameof(RoomCodePresenter)}] 방 코드 {code}", this);
        }
    }
}
