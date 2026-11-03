using System.Collections;
using Chapchu.Cards;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 뷰에 카드를 넣고 뺀다 (10-ui.md 4절). 손패 원본은 방장(GameServer)에 있고 여기는 화면 표시용 사본이다.
    /// 무엇을 언제 넣고 뺄지는 GameScreenPresenter 가 연출 큐 순서로 부른다 — 내가 뽑은 카드(첫 5장 포함)를 덱 자리에서 꺼내 붙이고, 방장이 승인한 내 카드 사용만 뺀다.
    /// 카드 종류 ID → CardData 는 덱 에셋으로 찾는다 — 게임에 나오는 카드는 전부 덱에서 나오기 때문.
    /// </summary>
    [RequireComponent(typeof(HandView))]
    public class HandPresenter : MonoBehaviour
    {
        [Tooltip("PunGameServer 와 같은 덱 에셋 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;
        [Tooltip("뽑은 카드가 날아오기 시작하는 자리 — 덱 더미.")]
        [SerializeField] private RectTransform drawOrigin;
        [Tooltip("카드를 꺼낸 뒤 다음 일로 넘어가기까지 초. 날아오는 연출(HandView)이 끝나기를 다 기다리면 첫 5장에 5초가 걸려, 겹쳐 날아오게 둔다.")]
        [SerializeField] private float dealInterval = 0.15f;

        private HandView _view;

        private void Awake()
        {
            _view = GetComponent<HandView>();

            if (deck == null || drawOrigin == null)
                Debug.LogError($"[{nameof(HandPresenter)}] {nameof(deck)} 또는 {nameof(drawOrigin)} 참조가 비어 있습니다.", this);
        }

        /// <summary>내가 뽑은 카드를 덱 자리에서 꺼내 손패 끝에 붙인다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator DrawRoutine(int cardInstanceId, int cardId)
        {
            CardData data = deck.GetCard(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(HandPresenter)}] 덱에 없는 카드 종류 cardId={cardId}", this);
                yield break;
            }

            _view.AddCard(cardInstanceId, data, drawOrigin.position);
            yield return new WaitForSeconds(dealInterval);
        }

        /// <summary>방장이 승인한 내 카드 사용 — 손패에서 뺀다.</summary>
        public void ShowCardPlayed(int cardInstanceId)
        {
            if (!_view.RemoveCard(cardInstanceId))
                Debug.LogWarning($"[{nameof(HandPresenter)}] 승인된 카드가 손패에 없다. instanceId={cardInstanceId}", this);
        }
    }
}
