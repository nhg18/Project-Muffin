using TMPro;
using UnityEngine;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 전원 준비 뒤의 덮개 (시안 「결과화면 UI v3」 .overlay) — 「모두 준비 완료!」 3 · 2 · 1 → 「다음 판 시작!」.
    /// 남은 초를 받아 그리기만 한다. 덮개가 화면을 덮는 동안 아래 버튼은 눌리지 않는다.
    /// </summary>
    public class ResultCountdownView : MonoBehaviour
    {
        private const string StartLabel = "다음 판 시작!";

        [Tooltip("덮개 — 카운트다운이 없으면 꺼져 있다.")]
        [SerializeField] private GameObject panel;
        [Tooltip("「모두 준비 완료!」 — 숫자를 세는 동안만.")]
        [SerializeField] private GameObject readyLine;
        [SerializeField] private TMP_Text bigText;

        [Tooltip("숫자 하나를 보이는 시간 (초).")]
        [SerializeField] private float stepSeconds = 0.8f;
        [Tooltip("「다음 판 시작!」 을 보이는 시간 (초).")]
        [SerializeField] private float startSeconds = 1.1f;
        [SerializeField] private float numberFontSize = 220f;
        [SerializeField] private float startFontSize = 150f;

        private int _shown = -1; // 지금 그려진 것 — 숫자, 0 이면 「다음 판 시작!」, -1 이면 꺼짐

        private void Awake()
        {
            if (panel == null || readyLine == null || bigText == null)
                Debug.LogError($"[{nameof(ResultCountdownView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>다음 판 시작까지 남은 초. 카운트다운이 없으면 hasCountdown = false.</summary>
        public void Show(bool hasCountdown, double secondsLeft)
        {
            int step = !hasCountdown ? -1 : secondsLeft <= startSeconds ? 0 : Mathf.CeilToInt((float)(secondsLeft - startSeconds) / stepSeconds);
            if (step == _shown)
                return;

            _shown = step;
            panel.SetActive(step >= 0);
            if (step < 0)
                return;

            readyLine.SetActive(step > 0);
            bigText.fontSize = step > 0 ? numberFontSize : startFontSize;
            bigText.text = step > 0 ? step.ToString() : StartLabel;
        }
    }
}
