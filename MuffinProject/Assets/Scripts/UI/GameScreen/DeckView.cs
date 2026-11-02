using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 덱 뒷면 + 남은 장수. 탭을 이벤트로만 알린다 (10-ui.md 2-1 · 9절 "덱 탭 → 드로우 요청").
    /// 뽑을 수 있는지 스스로 판정하지 않는다 — 안 되면 서버가 거절로 알려준다.
    /// </summary>
    public class DeckView : MonoBehaviour
    {
        private const string CountFormat = "DRAW {0}";

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text countText;

        /// <summary>덱 더미를 누름 — 드로우 요청 (10-ui.md 9절)</summary>
        public event Action OnDrawRequested;

        private void Awake()
        {
            if (button == null || countText == null)
                Debug.LogError($"[{nameof(DeckView)}] {nameof(button)} 또는 {nameof(countText)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            button.onClick.AddListener(HandleClicked);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(HandleClicked);
        }

        /// <summary>덱 남은 장수 표시 ("DRAW n")</summary>
        public void SetDeckCount(int count) => countText.text = string.Format(CountFormat, count);

        private void HandleClicked() => OnDrawRequested?.Invoke();
    }
}
