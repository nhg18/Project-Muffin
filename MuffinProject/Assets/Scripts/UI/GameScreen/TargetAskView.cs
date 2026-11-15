using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 대상 선택 안내 — 화면을 어둡게 하고 「카드 · 누구에게 쓸까요?」 + 「취소」 (10-ui.md 6절 HUD v2 — 제한 시간 없음). 취소 버튼이 모바일 취소 입력이다.
    /// 값을 받아 표시하고 탭을 이벤트로만 알린다. 기본은 비활성.
    /// </summary>
    public class TargetAskView : MonoBehaviour
    {
        private const string QuestionFormat = "<color=#{0}>{1}</color> · 누구에게 쓸까요?"; // {0} = 카드 이름 색(16진수), {1} = 카드 이름

        [Tooltip("배경을 어둡게 — 좌석보다 뒤에 둔다 (좌석은 어두워지지 않는다).")]
        [SerializeField] private GameObject dimmer;
        [SerializeField] private GameObject bar;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button cancelButton;
        [Tooltip("안내 글 속 카드 이름 색 — 행동 카드 색 (시안 #009dff, FeedView · CardOwnerTagView 의 actionColor 와 같게).")]
        [SerializeField] private Color cardNameColor = new Color32(0x00, 0x9d, 0xff, 0xFF);

        /// <summary>「취소」를 눌렀다.</summary>
        public event Action CancelClicked;

        private void Awake()
        {
            if (dimmer == null || bar == null || questionText == null || cancelButton == null)
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
            questionText.text = string.Format(QuestionFormat, ColorUtility.ToHtmlStringRGB(cardNameColor), cardName);
            dimmer.SetActive(true);
            bar.SetActive(true);
        }

        public void Hide()
        {
            dimmer.SetActive(false);
            bar.SetActive(false);
        }

        private void HandleCancelClicked() => CancelClicked?.Invoke();
    }
}
