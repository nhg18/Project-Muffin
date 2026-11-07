using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 덱 더미 — 뒷면 Quad 를 겹친 두께 + 남은 장수. 탭을 이벤트로만 알린다 (10-ui.md 2-1 · 9절 "덱 탭 → 드로우 요청").
    /// 탭은 카메라의 PhysicsRaycaster 가 BoxCollider 로 맞춰 준다. 뽑을 수 있는지 스스로 판정하지 않는다 — 안 되면 서버가 거절로 알려준다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DeckView : MonoBehaviour, IPointerClickHandler
    {
        private const string CountFormat = "덱 {0}";

        [SerializeField] private TMP_Text countText;

        public event Action DrawClicked;

        private void Awake()
        {
            if (countText == null)
                Debug.LogError($"[{nameof(DeckView)}] {nameof(countText)} 참조가 비어 있습니다.", this);
        }

        public void SetDeckCount(int count) => countText.text = string.Format(CountFormat, count);

        public void OnPointerClick(PointerEventData eventData) => DrawClicked?.Invoke();
    }
}
