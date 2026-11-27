using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 덱 더미 — 테이블 면에 누운 뒷면 카드 + 그 밑에 깔린 받침 카드(겹친 두께) + 남은 장수. 탭을 이벤트로만 알린다 (10-ui.md 2-1 「덱」 · 9절 "덱 탭 → 드로우 요청").
    /// 탭은 카메라의 PhysicsRaycaster 가 눕힌 더미(자식)의 BoxCollider 로 맞추고, 클릭 이벤트가 부모인 이 컴포넌트로 올라온다. 뽑을 수 있는지 스스로 판정하지 않는다 — 안 되면 서버가 거절로 알려준다.
    /// 덱 위 노란 「뽑고 턴 끝」 버튼(통합 화면 UI v3 .drawbtn)도 같은 요청을 알린다 — 언제 보일지는 프레젠터가 정한다. 남은 장수는 덱 아래 「덱 n」 알약(HUD).
    /// </summary>
    public class DeckView : MonoBehaviour, IPointerClickHandler
    {
        private const string CountFormat = "덱 {0}";

        [SerializeField] private TMP_Text countText;
        [Tooltip("맨 위 카드(흰 받침 + 뒷면 그림). 0장이면 숨긴다 — 탭 영역은 남는다 (빈 덱에서 뽑기 = 빈 덱 피해, 05-deck.md 5절).")]
        [SerializeField] private GameObject topCard;
        [Tooltip("맨 위 카드 밑에 깔린 받침 카드, 위부터. 덱 장수 − 1 만큼만 켠다 (2장이면 1겹 · 1장 이하면 없음).")]
        [SerializeField] private GameObject[] underCards;
        [Tooltip("덱 위 「뽑고 턴 끝」 버튼 (HUD) — 내 턴에 뽑을 수 있을 때만 보인다.")]
        [SerializeField] private Button drawButton;

        public event Action DrawClicked;

        private void Awake()
        {
            if (countText == null || topCard == null || underCards == null || drawButton == null)
                Debug.LogError($"[{nameof(DeckView)}] {nameof(countText)} · {nameof(topCard)} · {nameof(underCards)} · {nameof(drawButton)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable() => drawButton.onClick.AddListener(HandleDrawButton);

        private void OnDisable() => drawButton.onClick.RemoveListener(HandleDrawButton);

        public void SetDeckCount(int count)
        {
            countText.text = string.Format(CountFormat, count);
            topCard.SetActive(count > 0);

            int under = Mathf.Max(0, count - 1);
            for (int i = 0; i < underCards.Length; i++)
                underCards[i].SetActive(i < under);
        }

        /// <summary>「뽑고 턴 끝」 버튼을 보일지 — 내 턴 · 체인 없음 · 대상 선택 아님 · 멈춤 아님 (프레젠터가 정한다).</summary>
        public void SetCanDraw(bool canDraw)
        {
            if (drawButton.gameObject.activeSelf != canDraw)
                drawButton.gameObject.SetActive(canDraw);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 우클릭은 취소 입력 (10-ui.md 9절) — 왼쪽(터치)만 드로우.
            if (eventData.button != PointerEventData.InputButton.Left) return;

            DrawClicked?.Invoke();
        }

        private void HandleDrawButton() => DrawClicked?.Invoke();
    }
}
