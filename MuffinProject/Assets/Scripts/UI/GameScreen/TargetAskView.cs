using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 대상 선택 안내 — 화면을 어둡게 하고 「카드 · 누구에게 쓸까요?」 + 남은 초 + 「취소」 (10-ui.md 6절 HUD v2). 취소 버튼이 모바일 취소 입력이다.
    /// 값을 받아 표시하고 탭을 이벤트로만 알린다. 기본은 비활성.
    /// </summary>
    public class TargetAskView : MonoBehaviour
    {
        private const string QuestionFormat = "<color=#009dff>{0}</color> · 누구에게 쓸까요?";
        private const string SecondsFormat = "{0}초";

        [Tooltip("배경을 어둡게 — 좌석보다 뒤에 둔다 (좌석은 어두워지지 않는다).")]
        [SerializeField] private GameObject dimmer;
        [SerializeField] private GameObject bar;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private Button cancelButton;

        /// <summary>「취소」를 눌렀다.</summary>
        public event Action CancelClicked;

        private void Awake()
        {
            if (dimmer == null || bar == null || questionText == null || secondsText == null || cancelButton == null)
                Debug.LogError($"[{nameof(TargetAskView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            cancelButton.onClick.AddListener(HandleCancelClicked);
        }

        private void OnDisable()
        {
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        public void Show(string cardName)
        {
            questionText.text = string.Format(QuestionFormat, cardName);
            dimmer.SetActive(true);
            bar.SetActive(true);
        }

        public void SetRemainingSeconds(int seconds) => secondsText.text = string.Format(SecondsFormat, seconds);

        public void Hide()
        {
            dimmer.SetActive(false);
            bar.SetActive(false);
        }

        private void HandleCancelClicked() => CancelClicked?.Invoke();
    }
}
