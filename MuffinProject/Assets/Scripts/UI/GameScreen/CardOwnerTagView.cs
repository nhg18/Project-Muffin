using Chapchu.Game;
using Chapchu.UI.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3 단계(확대) 동안 크게 보인 카드 아래에 뜨는 주인 태그 — 종류 색 작은 카드 + 「낸 사람 → 대상」 (10-ui.md 2-1 「카드 연출 자리」, 시안 .owner).
    /// 글은 ChainCardsPresenter 가 만들어 넘긴다 (이름은 플레이어 색 리치 텍스트). 값을 받아 표시만 한다.
    /// </summary>
    public class CardOwnerTagView : MonoBehaviour
    {
        [Tooltip("켜고 끄는 몸통 — 루트는 켜 둔다.")]
        [SerializeField] private GameObject body;
        [SerializeField] private TMP_Text ownerText;
        [Tooltip("작은 카드의 테 · 면 (UIShape 한 장) — 테를 종류 색으로 칠한다.")]
        [SerializeField] private UIShape kindBox;
        [Tooltip("작은 카드에서 종류 색으로 칠하는 이미지 (띠).")]
        [SerializeField] private Image[] kindImages;
        [SerializeField] private Color actionColor = new Color32(0x00, 0x9d, 0xff, 0xFF);
        [SerializeField] private Color counterColor = new Color32(0x6f, 0xa4, 0x34, 0xFF);
        [SerializeField] private Color trapColor = new Color32(0xf6, 0x46, 0x12, 0xFF);

        private void Awake()
        {
            if (body == null || ownerText == null || kindBox == null || kindImages == null || kindImages.Length == 0)
            {
                Debug.LogError($"[{nameof(CardOwnerTagView)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            Hide();
        }

        public void Show(CardType type, string text)
        {
            Color kind = type == CardType.Counter ? counterColor : type == CardType.Trap ? trapColor : actionColor;
            kindBox.BorderColor = kind;
            foreach (Image image in kindImages)
                image.color = kind;

            ownerText.text = text;
            body.SetActive(true);
        }

        public void Hide() => body.SetActive(false);
    }
}
