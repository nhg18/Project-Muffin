using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 반응 타이머 — 크게 둔 처리 카드의 아래 가장자리에 걸치는 알약 (10-ui.md 5절 「반응 타이머」 · 2-1 「카드 연출 자리」, 시안 .rt2).
    /// 열리기 전: 회색 「반응 · 곧」. 진행 중: 노란 막대가 줄고 남은 초, 얼마 안 남으면 빨강. 마감 뒤: 「‖ 처리하는 중」.
    /// 값은 ReactionTimerPresenter 가 매 프레임 넘긴다. 받은 값을 표시만 한다.
    /// </summary>
    public class ReactionTimerView : MonoBehaviour
    {
        private const string ReactionLabel = "반응";
        private const string ResolvingLabel = "처리하는 중";
        private const string SoonText = "곧";

        [Tooltip("켜고 끄는 몸통 — 루트는 켜 둔다.")]
        [SerializeField] private GameObject body;
        [SerializeField] private Image border;
        [SerializeField] private GameObject clockIcon;
        [SerializeField] private GameObject pauseIcon;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private GameObject bar;
        [Tooltip("막대의 채움 — 오른쪽 앵커(0~1)로 남은 비율을 그린다.")]
        [SerializeField] private Image barFill;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private GameObject unitText;

        [Header("색 (시안 .rt2 · .low · .soon)")]
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color fillColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color lowFillColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color lowInk = new Color32(0xc8, 0x1e, 0x2b, 0xFF);
        [SerializeField] private Color soonBorder = new Color32(0xb8, 0xae, 0xa4, 0xFF);
        [SerializeField] private Color soonInk = new Color32(0x8a, 0x81, 0x7a, 0xFF);

        private void Awake()
        {
            if (body == null || border == null || clockIcon == null || pauseIcon == null || labelText == null || bar == null || barFill == null || secondsText == null || unitText == null)
            {
                Debug.LogError($"[{nameof(ReactionTimerView)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            Hide();
        }

        /// <summary>반응 시간이 아직 열리지 않았다 (카드 연출 중) — 회색 「반응 · 곧」.</summary>
        public void ShowSoon()
        {
            ShowParts(true);
            border.color = soonBorder;
            labelText.text = ReactionLabel;
            labelText.color = soonInk;
            SetFill(0f, fillColor);
            secondsText.text = SoonText;
            secondsText.color = soonInk;
            unitText.SetActive(false);
        }

        /// <summary>반응 시간 진행 중 — 남은 초 · 남은 비율(0~1). low 면 빨강.</summary>
        public void ShowRemaining(int seconds, float fraction, bool low)
        {
            ShowParts(true);
            border.color = ink;
            labelText.text = ReactionLabel;
            labelText.color = ink;
            SetFill(fraction, low ? lowFillColor : fillColor);
            secondsText.text = seconds.ToString();
            secondsText.color = low ? lowInk : ink;
            unitText.SetActive(true);
        }

        /// <summary>마감이 지났고 결과를 기다린다 — 「‖ 처리하는 중」.</summary>
        public void ShowResolving()
        {
            ShowParts(false);
            border.color = ink;
            labelText.text = ResolvingLabel;
            labelText.color = ink;
        }

        public void Hide() => body.SetActive(false);

        // counting = 시계 · 막대 · 초, 아니면 멈춤 아이콘 + 글만
        private void ShowParts(bool counting)
        {
            body.SetActive(true);
            clockIcon.SetActive(counting);
            pauseIcon.SetActive(!counting);
            bar.SetActive(counting);
            secondsText.gameObject.SetActive(counting);
            if (!counting)
                unitText.SetActive(false);
        }

        private void SetFill(float fraction, Color color)
        {
            RectTransform fill = barFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), fill.anchorMax.y);
            barFill.color = color;
        }
    }
}
