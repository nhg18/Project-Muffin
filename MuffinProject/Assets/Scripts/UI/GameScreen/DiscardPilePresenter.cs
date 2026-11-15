using Chapchu.Cards;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 버림 더미 뷰에 맨 위 카드와 장수를 넣는다 (05-deck.md 7 · 9절, 10-ui.md 2-1 「버림 더미」). 원본은 방장(GameServer)에 있고 여기는 화면 표시용이다.
    /// 값은 GameScreenPresenter 가 연출 큐 순서로 넘겨 줄 때만 바뀐다 — 버림이 생기는 일(효과 적용 · 무효화 · 강제 버림 · 함정 파괴 · 사망 · 나감)과 덱 다시 채우기.
    /// 카드 종류 ID → CardData 는 덱 에셋으로 찾는다 — 게임에 나오는 카드는 전부 덱에서 나오기 때문.
    /// </summary>
    [RequireComponent(typeof(DiscardPileView))]
    public class DiscardPilePresenter : MonoBehaviour
    {
        [Tooltip("PunGameServer 와 같은 덱 에셋 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;

        private DiscardPileView _view;

        private void Awake()
        {
            _view = GetComponent<DiscardPileView>();

            if (deck == null)
                Debug.LogError($"[{nameof(DiscardPilePresenter)}] {nameof(deck)} 참조가 비어 있습니다.", this);
        }

        // 씬이 늦게 열려 버림 이벤트를 놓쳤을 때를 위해 지금 값 스냅샷에서 한 번 읽는다 (09-network.md 6.1). 없으면 뷰가 빈 더미 그대로다.
        private void Start()
        {
            if (!PhotonNetwork.InRoom) return;

            ExitGames.Client.Photon.Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (!props.TryGetValue(RoomProps.DiscardCount, out object countValue) || !(countValue is int count) || count <= 0)
                return;

            if (props.TryGetValue(RoomProps.DiscardTop, out object topValue) && topValue is int topCardId)
                Show(topCardId, count);
            else
                _view.Set(null, count);
        }

        /// <summary>버림 더미 맨 위 카드(마지막에 버린 카드)와 장수 — 누가 버리든 전원에게 온다.</summary>
        public void Show(int topCardId, int count)
        {
            CardData data = deck.GetCard(topCardId);
            if (data == null)
                Debug.LogWarning($"[{nameof(DiscardPilePresenter)}] 덱에 없는 카드 종류 cardId={topCardId}", this);

            _view.Set(data, count);
        }

        /// <summary>여러 장이 한 번에 버려졌다 (강제 버림 · 사망 · 나감) — 마지막 장이 맨 위. 버려진 카드가 없으면 그대로 둔다.</summary>
        public void Show(int[] discardedCardIds, int count)
        {
            if (discardedCardIds.Length > 0)
                Show(discardedCardIds[discardedCardIds.Length - 1], count);
        }

        /// <summary>맨 위 카드 자리 (월드 좌표) — 더미에서 손패로 가져오는 카드가 여기서 나온다 (109).</summary>
        public Vector3 TopPosition => _view.TopAnchor.position;

        /// <summary>더미를 비운다 — 덱을 다시 채웠다 (05-deck.md 5절).</summary>
        public void Clear() => _view.Set(null, 0);
    }
}
