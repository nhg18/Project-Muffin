using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 남은 초 — 두 모양이 같은 값을 그린다 (통합 화면 UI v3).
    /// 스톱워치(.swatch, 지금 턴인 캐릭터 머리 옆): 남은 비율만큼 차는 링(위에서 시계 방향) + 초. 5초 이하면 링 · 글자가 빨강.
    /// 배지(.tsec, 턴 순서의 지금 턴 이름표): 링 없이 동그라미 + 초. 5초 이하면 바탕이 빨강 · 글자 흰색 · 1초마다 커졌다 돌아온다.
    /// 멈춤(사망 대기)이면 멈춘 초를 회색으로. 값은 TurnTimerPresenter 가 매 프레임 넘긴다. 켜고 끄는 것은 좌석 · 내 HUD · 턴 순서가 턴 표시와 함께 한다.
    /// </summary>
    public class TurnTimerView : MonoBehaviour
    {
        [Tooltip("남은 비율만큼 차는 링 — Filled · Radial 360 · 위에서 시계 방향. 링 없는 배지는 비운다.")]
        [SerializeField] private Image ring;
        [SerializeField] private TMP_Text secondsText;
        [Tooltip("배지 바탕 — 남은 초가 적으면 빨강으로 칠한다 (시안 .tsec.hot). 스톱워치는 비운다.")]
        [SerializeField] private Graphic face;
        [Tooltip("남은 초가 이 값 이하면 빨강 (시안: 5).")]
        [SerializeField] private int urgentSeconds = 5;
        [Tooltip("남은 초가 적을 때 1초마다 이 배율까지 커졌다 돌아온다 (시안 tsecTick 1.15). 1 이면 그대로.")]
        [SerializeField] private float urgentPulse = 1f;
        [Tooltip("평소 링 색.")]
        [SerializeField] private Color ringColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [Tooltip("평소 남은 초 글자색.")]
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [Tooltip("배지의 평소 바탕색.")]
        [SerializeField] private Color faceColor = Color.white;
        [Tooltip("남은 초가 적을 때의 빨강 — 스톱워치는 링 · 글자, 배지는 바탕.")]
        [SerializeField] private Color urgentColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [Tooltip("남은 초가 적을 때 빨간 배지 위 글자색.")]
        [SerializeField] private Color urgentInkOnFace = Color.white;
        [Tooltip("멈춤일 때 링 색.")]
        [SerializeField] private Color pausedRingColor = new Color32(0xb8, 0xae, 0xa4, 0xFF);
        [Tooltip("멈춤일 때 글자색.")]
        [SerializeField] private Color pausedInk = new Color32(0x8a, 0x81, 0x7a, 0xFF);

        private int _shownSeconds = -1;

        private void Awake()
        {
            if (secondsText == null)
                Debug.LogError($"[{nameof(TurnTimerView)}] {nameof(secondsText)} 참조가 비어 있습니다.", this);
        }

        /// <summary>남은 초와 남은 비율(0~1). 멈춤이면 멈춘 초를 회색으로.</summary>
        public void SetRemaining(int seconds, float fraction, bool paused)
        {
            bool urgent = !paused && seconds <= urgentSeconds;
            if (ring != null)
            {
                ring.fillAmount = Mathf.Clamp01(fraction);
                ring.color = paused ? pausedRingColor : urgent ? urgentColor : ringColor;
            }

            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                secondsText.SetText("{0}", seconds);
            }

            if (face != null)
            {
                face.color = urgent ? urgentColor : faceColor;
                secondsText.color = paused ? pausedInk : urgent ? urgentInkOnFace : ink;
            }
            else
            {
                secondsText.color = paused ? pausedInk : urgent ? urgentColor : ink;
            }

            // 시안 tsecTick — 1초 주기 ease-in-out 으로 가운데(0.5초)에 가장 크다
            float pulse = urgent && urgentPulse > 1f ? (1f - Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f)) / 2f : 0f;
            transform.localScale = Vector3.one * (1f + (urgentPulse - 1f) * pulse);
        }
    }
}
