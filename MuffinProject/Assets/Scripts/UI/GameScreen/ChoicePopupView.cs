using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 효과 고르기 판 — 선택지 버튼 2개 (프로토타입 v2 `.pop2.pick` · G.choose opts, 11-card-list 119).
    /// 머리 띠: 발 아이콘 + 제목 + 남은 초(오른쪽). 몸통: 설명 + 버튼 줄(첫 버튼이 주 버튼). 받은 값을 표시만 한다.
    /// </summary>
    public class ChoicePopupView : MonoBehaviour
    {
        private const string SecondsFormat = "{0}초";

        [Tooltip("켜고 끄는 판 (바탕 가림 포함) — 이 컴포넌트의 오브젝트는 켜 둔다.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private Button firstButton;
        [SerializeField] private TMP_Text firstLabel;
        [SerializeField] private Button secondButton;
        [SerializeField] private TMP_Text secondLabel;

        public event Action<int> Picked; // 0 첫 버튼 · 1 둘째 버튼

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel == null || titleText == null || secondsText == null || descText == null || firstButton == null || firstLabel == null || secondButton == null || secondLabel == null)
            {
                Debug.LogError($"[{nameof(ChoicePopupView)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            firstButton.onClick.AddListener(() => Picked?.Invoke(0));
            secondButton.onClick.AddListener(() => Picked?.Invoke(1));
            panel.SetActive(false);
        }

        public void Show(string title, string desc, string first, string second)
        {
            titleText.text = title;
            descText.text = desc;
            firstLabel.text = first;
            secondLabel.text = second;
            panel.SetActive(true);
        }

        public void SetSeconds(int seconds) => secondsText.text = string.Format(SecondsFormat, seconds);

        public void Hide() => panel.SetActive(false);
    }
}
