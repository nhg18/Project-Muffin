using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 새 카드 보여 주기의 HUD — 크게 띄운 카드 아래의 남은 시간 막대와 안내 글 (10-ui.md 2-1 「새 카드 보여 주기」, 프로토타입 v2 .rvbar · .rvhint).
    /// 카드는 3D 층(HandView)이 놓고, 여기는 받은 자리 · 시간대로 표시만 한다. 판정과 상관없는 연출.
    /// </summary>
    public class HandRevealView : MonoBehaviour
    {
        [Tooltip("막대 · 안내 글을 묶은 것 — 캔버스 가운데 기준으로 세로 자리만 옮긴다.")]
        [SerializeField] private RectTransform content;
        [Tooltip("남은 시간만큼 줄어드는 막대 — 왼쪽 끝이 기준점.")]
        [SerializeField] private RectTransform fill;
        [Tooltip("카드 묶음 아래 끝에서 막대 위 끝까지 (캔버스 px) — 시안 12 × 2.")]
        [SerializeField] private float gap = 24f;

        private float _startsAt;
        private float _endsAt;

        private void Awake()
        {
            if (content == null || fill == null)
                Debug.LogError($"[{nameof(HandRevealView)}] {nameof(content)} · {nameof(fill)} 참조 중 비어 있는 것이 있습니다.", this);

            Hide();
        }

        private void Update()
        {
            float remaining = Mathf.InverseLerp(_endsAt, _startsAt, Time.time); // 시작 전 1 → 끝 0
            fill.localScale = new Vector3(remaining, 1f, 1f);
        }

        /// <summary>카드 묶음의 아래 끝(캔버스 y, 화면 가운데가 0)에 맞춰 켠다. delay 초 뒤부터 hold 초 동안 막대가 줄어든다.</summary>
        public void Show(float cardsBottomY, float delay, float hold)
        {
            content.anchoredPosition = new Vector2(0f, cardsBottomY - gap);
            _startsAt = Time.time + delay;
            _endsAt = _startsAt + hold;
            fill.localScale = Vector3.one;
            content.gameObject.SetActive(true);
            enabled = true;
        }

        public void Hide()
        {
            content.gameObject.SetActive(false);
            enabled = false;
        }
    }
}
