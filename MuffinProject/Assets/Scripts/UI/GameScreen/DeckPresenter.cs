using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 덱 뷰와 서버를 잇는다. 탭 → 드로우 요청, 덱 잔여 장수 → <c>DRAW n</c> (10-ui.md 2-1 · 9절, 05-deck.md 9절).
    /// 두 흐름은 서로 모른다 — 요청 뒤 장수를 미리 줄이지 않고, 방장이 뽑아 준 결과(GameEvents)로만 갱신한다.
    /// 뽑을 수 있는지(내 턴 · 메인 행동 미사용)는 방장이 판정한다. 거절 문구는 <see cref="RejectToastPresenter"/> 가 띄운다.
    /// 뽑은 카드 내용은 다루지 않는다 — 손패 쪽이 GameEvents.OnMyCardDrawn 을 직접 구독한다.
    /// </summary>
    [RequireComponent(typeof(DeckView))]
    public class DeckPresenter : MonoBehaviour
    {
        [SerializeField] private PunGameServer server;

        private DeckView _view;

        private void Awake()
        {
            _view = GetComponent<DeckView>();

            if (server == null)
                Debug.LogError($"[{nameof(DeckPresenter)}] {nameof(server)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.DrawClicked += HandleDrawClicked;
            GameEvents.OnCardDrawn += HandleCardDrawn;
            GameEvents.OnDeckRefilled += HandleDeckRefilled;
        }

        private void OnDisable()
        {
            _view.DrawClicked -= HandleDrawClicked;
            GameEvents.OnCardDrawn -= HandleCardDrawn;
            GameEvents.OnDeckRefilled -= HandleDeckRefilled;
        }

        // 씬이 늦게 열려 첫 분배 이벤트를 놓쳤을 때를 위해 지금 값 스냅샷에서 한 번 읽는다 (09-network.md 6.1).
        private void Start()
        {
            if (!PhotonNetwork.InRoom) return;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.DeckCount, out object value) && value is int deckCount)
                _view.SetDeckCount(deckCount);
        }

        private void HandleDrawClicked() => server?.RequestDraw();

        // 누가 뽑든 전원에게 온다.
        private void HandleCardDrawn(int actorNumber, int handCount, int deckCount) => _view.SetDeckCount(deckCount);

        private void HandleDeckRefilled(int deckCount) => _view.SetDeckCount(deckCount);
    }
}
