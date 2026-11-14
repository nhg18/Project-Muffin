using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 덱 더미 — 테이블 면에 누운 뒷면 카드 + 그 밑에 깔린 받침 카드(겹친 두께) + 남은 장수. 탭을 이벤트로만 알린다 (10-ui.md 2-1 「덱」 · 9절 "덱 탭 → 드로우 요청").
    /// 탭은 카메라의 PhysicsRaycaster 가 눕힌 더미(자식)의 BoxCollider 로 맞추고, 클릭 이벤트가 부모인 이 컴포넌트로 올라온다. 뽑을 수 있는지 스스로 판정하지 않는다 — 안 되면 서버가 거절로 알려준다.
    /// </summary>
    public class DeckView : MonoBehaviour, IPointerClickHandler
    {
        private const string CountFormat = "덱 {0}";

        [SerializeField] private TMP_Text countText;
        [Tooltip("맨 위 카드(흰 받침 + 뒷면 그림). 0장이면 숨긴다 — 탭 영역은 남는다 (빈 덱에서 뽑기 = 빈 덱 피해, 05-deck.md 5절).")]
        [SerializeField] private GameObject topCard;
        [Tooltip("맨 위 카드 밑에 깔린 받침 카드, 위부터. 덱 장수 − 1 만큼만 켠다 (2장이면 1겹 · 1장 이하면 없음).")]
        [SerializeField] private GameObject[] underCards;

        public event Action DrawClicked;

        private void Awake()
        {
            if (countText == null || topCard == null || underCards == null)
                Debug.LogError($"[{nameof(DeckView)}] {nameof(countText)} · {nameof(topCard)} · {nameof(underCards)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetDeckCount(int count)
        {
            countText.text = string.Format(CountFormat, count);
            topCard.SetActive(count > 0);

            int under = Mathf.Max(0, count - 1);
            for (int i = 0; i < underCards.Length; i++)
                underCards[i].SetActive(i < under);
        }

        public void OnPointerClick(PointerEventData eventData) => DrawClicked?.Invoke();
    }
}
