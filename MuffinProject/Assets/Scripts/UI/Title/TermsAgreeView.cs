using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 약관 동의 — 「모두 동의하기」 · 두 약관 칸(본문 스크롤 + 체크) · 「동의하고 계속」. 둘 다 체크해야 OnAgreed.
    /// 하나라도 빠진 채 누르면 빠진 칸만 빨간 테두리 + 흔들림 + 안내 한 줄. 기준 문서: docs/systems/20-title-ui-full.md 7-11
    /// </summary>
    public class TermsAgreeView : MonoBehaviour
    {
        private const int TermCount = 2;
        private static readonly Color CheckedColor = new Color32(0x3C, 0xBF, 0x72, 0xFF);
        private static readonly Color ColumnColor = new Color32(0xF6, 0xF0, 0xE7, 0xFF);
        private static readonly Color NeedColor = new Color32(0xFF, 0xF1, 0xEF, 0xFF);
        private static readonly Color NeedEdgeColor = new Color32(0xEC, 0x3B, 0x46, 0xFF);

        [SerializeField] private Button allButton;
        [SerializeField] private Image allBox;
        [SerializeField] private GameObject allCheck;

        [Tooltip("이용약관 · 개인정보 처리방침 순서")]
        [SerializeField] private Button[] termButtons = new Button[TermCount];
        [SerializeField] private Image[] termBoxes = new Image[TermCount];
        [SerializeField] private GameObject[] termChecks = new GameObject[TermCount];
        [Tooltip("약관 칸 — 빠지면 이것을 흔든다")]
        [SerializeField] private RectTransform[] columns = new RectTransform[TermCount];
        [Tooltip("칸 테두리 — 평소 칸 색, 빠지면 빨강 (안쪽 테 5)")]
        [SerializeField] private Image[] columnEdges = new Image[TermCount];
        [SerializeField] private Image[] columnFills = new Image[TermCount];
        [SerializeField] private ScrollRect[] scrolls = new ScrollRect[TermCount];
        [SerializeField] private TMP_Text[] termTexts = new TMP_Text[TermCount];

        [Tooltip("약관 본문 — 앱에 넣는다 (23-login 7-5). 이용약관 · 개인정보 처리방침 순서")]
        [SerializeField] private TextAsset[] termDocuments = new TextAsset[TermCount];

        [SerializeField] private GameObject needMessage;
        [SerializeField] private Button confirmButton;
        [SerializeField] private PressableButton confirmLook;

        private readonly bool[] _checked = new bool[TermCount];
        private bool _showNeed;

        /// <summary>두 약관 모두 체크하고 「동의하고 계속」</summary>
        public event Action OnAgreed;

        private bool AllChecked => _checked[0] && _checked[1];

        private void Awake()
        {
            if (allButton == null || allBox == null || allCheck == null)
                Debug.LogError($"[{nameof(TermsAgreeView)}] 「모두 동의하기」 참조가 비어 있습니다.", this);

            if (needMessage == null || confirmButton == null || confirmLook == null)
                Debug.LogError($"[{nameof(TermsAgreeView)}] 「동의하고 계속」 참조가 비어 있습니다.", this);

            for (int i = 0; i < TermCount; i++)
            {
                if (termButtons[i] == null || termBoxes[i] == null || termChecks[i] == null || columns[i] == null || columnEdges[i] == null || columnFills[i] == null || scrolls[i] == null || termTexts[i] == null)
                    Debug.LogError($"[{nameof(TermsAgreeView)}] 약관 칸 {i} 참조가 비어 있습니다.", this);

                if (termDocuments[i] == null)
                    Debug.LogError($"[{nameof(TermsAgreeView)}] 약관 본문 {i} 이 비어 있습니다.", this);
                else
                    termTexts[i].text = termDocuments[i].text;
            }
        }

        private void OnEnable()
        {
            allButton.onClick.AddListener(HandleAllClicked);
            termButtons[0].onClick.AddListener(HandleFirstTermClicked);
            termButtons[1].onClick.AddListener(HandleSecondTermClicked);
            confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        private void OnDisable()
        {
            allButton.onClick.RemoveListener(HandleAllClicked);
            termButtons[0].onClick.RemoveListener(HandleFirstTermClicked);
            termButtons[1].onClick.RemoveListener(HandleSecondTermClicked);
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        private void Start()
        {
            for (int i = 0; i < TermCount; i++)
                scrolls[i].verticalNormalizedPosition = 1f;

            Apply();
        }

        /// <summary>빠진 칸 표시를 지운다 — 팝업을 다시 열 때. 체크는 타이틀에 있는 동안 남는다 (시안 openSheet)</summary>
        public void HideNeed()
        {
            _showNeed = false;
            Apply();
        }

        // 「모두 동의하기」 — 하나라도 꺼져 있으면 둘 다 켬, 둘 다 켜져 있으면 둘 다 끔 (20 5절)
        private void HandleAllClicked()
        {
            bool on = !AllChecked;
            _checked[0] = on;
            _checked[1] = on;
            Apply();
        }

        private void HandleFirstTermClicked() => Toggle(0);

        private void HandleSecondTermClicked() => Toggle(1);

        private void Toggle(int index)
        {
            _checked[index] = !_checked[index];
            Apply();
        }

        private void HandleConfirmClicked()
        {
            if (AllChecked)
            {
                OnAgreed?.Invoke();
                return;
            }

            _showNeed = true;
            Apply();
            for (int i = 0; i < TermCount; i++)
            {
                if (!_checked[i])
                    UIJuice.Shake(columns[i], 12f, 0.32f);
            }
        }

        private void Apply()
        {
            if (AllChecked)
                _showNeed = false;

            SetBox(allBox, allCheck, AllChecked);
            for (int i = 0; i < TermCount; i++)
            {
                SetBox(termBoxes[i], termChecks[i], _checked[i]);
                bool need = _showNeed && !_checked[i];
                columnEdges[i].color = need ? NeedEdgeColor : ColumnColor;
                columnFills[i].color = need ? NeedColor : ColumnColor;
            }

            needMessage.SetActive(_showNeed);
            confirmLook.SetDimmed(!AllChecked);
        }

        private static void SetBox(Image box, GameObject check, bool on)
        {
            box.color = on ? CheckedColor : Color.white;
            check.SetActive(on);
        }
    }
}
