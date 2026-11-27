using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 덱 뷰와 서버를 잇는다. 덱 탭 · 「뽑고 턴 끝」 버튼 → 드로우 요청, 덱 잔여 장수 → 「덱 n」 (10-ui.md 2-1 · 9절, 05-deck.md 9절).
    /// 두 흐름은 서로 모른다 — 요청 뒤 장수를 미리 줄이지 않고, 방장이 뽑아 준 결과를 GameScreenPresenter 가 연출 큐 순서로 넘겨 줄 때만 갱신한다.
    /// 뽑은 카드 내용은 다루지 않는다 — 손패 쪽(HandPresenter) 몫.
    /// </summary>
    [RequireComponent(typeof(DeckView))]
    public class DeckPresenter : MonoBehaviour
    {
        [SerializeField] private PunGameServer server;

        private DeckView _view;
        private bool _drawRequested; // 뽑기를 보냈다 — 턴이 바뀌거나 거절될 때까지 버튼을 다시 띄우지 않는다 (두 번 누름 방지)

        private void Awake()
        {
            _view = GetComponent<DeckView>();

            if (server == null)
                Debug.LogError($"[{nameof(DeckPresenter)}] {nameof(server)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.DrawClicked += HandleDrawClicked;
            GameEvents.OnMyRequestRejected += HandleRequestRejected;
        }

        private void OnDisable()
        {
            _view.DrawClicked -= HandleDrawClicked;
            GameEvents.OnMyRequestRejected -= HandleRequestRejected;
        }

        // 씬이 늦게 열려 첫 분배 이벤트를 놓쳤을 때를 위해 지금 값 스냅샷에서 한 번 읽는다 (09-network.md 6.1).
        private void Start()
        {
            if (!PhotonNetwork.InRoom) return;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.DeckCount, out object value) && value is int deckCount)
                _view.SetDeckCount(deckCount);
        }

        /// <summary>덱 잔여 장수 — 누가 뽑든 · 다시 채워지든 전원에게 온다.</summary>
        public void ShowDeckCount(int deckCount) => _view.SetDeckCount(deckCount);

        /// <summary>「뽑고 턴 끝」 버튼을 보일지 — 표시만, 판정은 방장이 한다 (안 되면 거절이 온다).</summary>
        public void ShowCanDraw(bool canDraw) => _view.SetCanDraw(canDraw && !_drawRequested);

        /// <summary>턴이 바뀌었다 — 다음 내 턴에 다시 뽑을 수 있다.</summary>
        public void ShowTurnChanged() => _drawRequested = false;

        private void HandleDrawClicked()
        {
            _drawRequested = true;
            _view.SetCanDraw(false);
            server?.RequestDraw();
        }

        private void HandleRequestRejected(int code) => _drawRequested = false;
    }
}
