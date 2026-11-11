using System.Collections.Generic;
using Chapchu.UI.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 방 로그 — 접힌 상태는 최근 한 줄 + 「n / 30」, 누르면 전부 펼친다 (최신이 아래, 세로 스크롤). 머리 띠 · 판 밖을 누르면 접힌다.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-9 · 3절 #9
    /// </summary>
    public class RoomLogView : MonoBehaviour
    {
        // 확정 (08-room.md 5절 #4)
        private const int MaxLines = 30;
        private const string CountFormat = "{0} / {1}";

        [Header("접힌 한 줄")]
        [SerializeField] private Button collapsedButton;

        [Tooltip("최근 한 줄 — 씬에는 기록이 없을 때의 글 · 색으로 저장한다")]
        [SerializeField] private TMP_Text lastLineText;
        [SerializeField] private TMP_Text collapsedCountText;

        [Header("펼친 판")]
        [SerializeField] private RectTransform expandedPanel;
        [SerializeField] private Button headerButton;
        [SerializeField] private TMP_Text expandedCountText;
        [SerializeField] private ScrollRect scroll;

        [Tooltip("줄 하나 — 프리팹 RoomLogLine")]
        [SerializeField] private TMP_Text linePrefab;

        [Tooltip("펼친 판 밖을 덮는 투명 버튼 — 누르면 접힌다")]
        [SerializeField] private Button outsideButton;

        [Tooltip("펼친 판에서 줄 목록 말고 차지하는 높이 — 머리 띠 + 위아래 여백 (캔버스 px)")]
        [SerializeField] private float chromeHeight = 92f;

        [Tooltip("줄 목록 높이의 최소 · 최대 (캔버스 px) — 넘으면 스크롤")]
        [SerializeField] private float minListHeight = 124f;
        [SerializeField] private float maxListHeight = 572f;

        private readonly List<TMP_Text> _lines = new List<TMP_Text>();

        public bool IsExpanded => expandedPanel.gameObject.activeSelf;

        private void Awake()
        {
            if (collapsedButton == null || lastLineText == null || collapsedCountText == null || expandedPanel == null || headerButton == null
                || expandedCountText == null || scroll == null || linePrefab == null || outsideButton == null)
                Debug.LogError($"[{nameof(RoomLogView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            collapsedButton.onClick.AddListener(HandleCollapsedClicked);
            headerButton.onClick.AddListener(HandleCollapseClicked);
            outsideButton.onClick.AddListener(HandleCollapseClicked);
        }

        private void OnDisable()
        {
            collapsedButton.onClick.RemoveListener(HandleCollapsedClicked);
            headerButton.onClick.RemoveListener(HandleCollapseClicked);
            outsideButton.onClick.RemoveListener(HandleCollapseClicked);
        }

        /// <summary>맨 아래에 한 줄 더한다. 30줄을 넘으면 가장 오래된 줄을 지운다.</summary>
        public void Add(string text)
        {
            TMP_Text line;
            if (_lines.Count < MaxLines)
            {
                line = Instantiate(linePrefab, scroll.content);
            }
            else
            {
                // 가장 오래된 줄을 맨 아래로 돌려 쓴다
                line = _lines[0];
                _lines.RemoveAt(0);
                line.transform.SetAsLastSibling();
            }

            line.text = text;
            _lines.Add(line);

            lastLineText.text = text;
            lastLineText.color = LobbyStyle.Ink;

            string count = string.Format(CountFormat, _lines.Count, MaxLines);
            collapsedCountText.text = count;
            expandedCountText.text = count;

            if (IsExpanded)
                FitExpandedPanel();
        }

        public void Collapse()
        {
            expandedPanel.gameObject.SetActive(false);
            outsideButton.gameObject.SetActive(false);
            collapsedButton.gameObject.SetActive(true);
        }

        private void Expand()
        {
            collapsedButton.gameObject.SetActive(false);
            outsideButton.gameObject.SetActive(true);
            expandedPanel.gameObject.SetActive(true);
            FitExpandedPanel();
        }

        // 판 높이를 줄 수에 맞추고 (최소 ~ 최대) 최신 줄이 보이게 맨 아래로 내린다
        private void FitExpandedPanel()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            float listHeight = Mathf.Clamp(scroll.content.rect.height, minListHeight, maxListHeight);
            expandedPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, chromeHeight + listHeight);
            scroll.verticalNormalizedPosition = 0f;
        }

        private void HandleCollapsedClicked() => Expand();

        private void HandleCollapseClicked() => Collapse();
    }
}
