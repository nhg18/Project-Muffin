using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 버림 더미 — 뒤 카드 2장 + 맨 위 카드 앞면 (10-ui.md 2-1). 맨 위 카드 그림만 바꾼다.
    /// 카드 ID → 그림 변환은 프레젠터가 한다 (덱 에셋 조회).
    /// </summary>
    public class DiscardPileView : MonoBehaviour
    {
        [SerializeField] private GameObject topCard;
        [SerializeField] private Image topCardImage;

        private void Awake()
        {
            if (topCard == null || topCardImage == null)
                Debug.LogError($"[{nameof(DiscardPileView)}] {nameof(topCard)} 또는 {nameof(topCardImage)} 참조가 비어 있습니다.", this);
        }

        /// <summary>맨 위 카드를 보인다. sprite 가 null 이면 빈 흰 카드.</summary>
        public void SetTopCard(Sprite sprite)
        {
            topCardImage.sprite = sprite;
            topCard.SetActive(true);
        }

        /// <summary>버림 더미가 비었다 (게임 시작 · 덱 다시 채움 뒤).</summary>
        public void SetEmpty() => topCard.SetActive(false);
    }
}
