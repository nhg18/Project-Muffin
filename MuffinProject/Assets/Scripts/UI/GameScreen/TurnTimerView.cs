using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 타이머 스톱워치 — 지금 턴인 캐릭터 머리 옆의 작은 시계 (10-ui.md 2-1 HUD v2 「턴 타이머」, 시안 .swatch).
    /// 남은 초 글자와 남은 비율만큼 차 있는 링(위에서 시계 방향)을 그린다. 5초 이하면 빨강, 멈춤이면 회색 + ‖ (멈춤은 기능 5 · 7 에서).
    /// 값은 TurnTimerPresenter 가 매 프레임 넘긴다. 켜고 끄는 것은 좌석 · 내 패널 뷰가 턴 표시와 함께 한다 — 누구 턴인지 여기서 판정하지 않는다.
    /// </summary>
    public class TurnTimerView : MonoBehaviour
    {
        private const string PausedText = "‖";

        [Tooltip("남은 비율만큼 차는 링 — Filled · Radial 360 · 위에서 시계 방향.")]
        [SerializeField] private Image ring;
        [SerializeField] private TMP_Text secondsText;
        [Tooltip("남은 초가 이 값 이하면 빨강 (시안: 5).")]
        [SerializeField] private int urgentSeconds = 5;
        [SerializeField] private float normalFontSize = 30f;
        [SerializeField] private float pausedFontSize = 24f;
        [SerializeField] private Color ringColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color urgentColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color pausedRingColor = new Color32(0xb8, 0xae, 0xa4, 0xFF);
        [SerializeField] private Color pausedInk = new Color32(0x8a, 0x81, 0x7a, 0xFF);

        private void Awake()
        {
            if (ring == null || secondsText == null)
                Debug.LogError($"[{nameof(TurnTimerView)}] {nameof(ring)} · {nameof(secondsText)} 참조가 비어 있습니다.", this);
        }

        /// <summary>남은 초와 남은 비율(0~1). 멈춤이면 초 대신 ‖.</summary>
        public void SetRemaining(int seconds, float fraction, bool paused)
        {
            bool urgent = !paused && seconds <= urgentSeconds;
            ring.fillAmount = Mathf.Clamp01(fraction);
            ring.color = paused ? pausedRingColor : urgent ? urgentColor : ringColor;
            secondsText.text = paused ? PausedText : seconds.ToString();
            secondsText.fontSize = paused ? pausedFontSize : normalFontSize;
            secondsText.color = paused ? pausedInk : urgent ? urgentColor : ink;
        }
    }
}
