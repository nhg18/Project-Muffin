using Chapchu.Cards;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 뷰와 서버 결과를 잇는다 (10-ui.md 4절). 손패 원본은 방장(GameServer)에 있고 여기는 화면 표시용 사본이다.
    /// 내가 뽑은 카드(OnMyCardDrawn, 첫 5장 포함)를 덱 자리에서 꺼내 붙이고, 방장이 승인한 내 카드 사용(OnCardPlayed)만 뺀다.
    /// 카드 종류 ID → CardData 는 덱 에셋으로 찾는다 — 게임에 나오는 카드는 전부 덱에서 나오기 때문.
    /// </summary>
    [RequireComponent(typeof(HandView))]
    public class HandPresenter : MonoBehaviour
    {
        [Tooltip("PunGameServer 와 같은 덱 에셋 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;
        [Tooltip("뽑은 카드가 날아오기 시작하는 자리 — 덱 더미.")]
        [SerializeField] private RectTransform drawOrigin;

        private HandView _view;

        private void Awake()
        {
            _view = GetComponent<HandView>();

            if (deck == null || drawOrigin == null)
                Debug.LogError($"[{nameof(HandPresenter)}] {nameof(deck)} 또는 {nameof(drawOrigin)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            GameEvents.OnMyCardDrawn += HandleMyCardDrawn;
            GameEvents.OnCardPlayed += HandleCardPlayed;
        }

        private void OnDisable()
        {
            GameEvents.OnMyCardDrawn -= HandleMyCardDrawn;
            GameEvents.OnCardPlayed -= HandleCardPlayed;
        }

        private void HandleMyCardDrawn(int cardInstanceId, int cardId)
        {
            CardData data = deck.GetCard(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardId}", this);
                return;
            }

            _view.AddCard(cardInstanceId, data, drawOrigin.position);
        }

        // 전원에게 오지만 내 손패에서 뺄 것은 내 카드뿐이다.
        private void HandleCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets, int handCount, int discardCount)
        {
            if (actorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

            if (!_view.RemoveCard(cardInstanceId))
                Debug.LogWarning($"[{nameof(HandPresenter)}] 승인된 카드가 손패에 없다. instanceId={cardInstanceId}", this);
        }
    }
}
