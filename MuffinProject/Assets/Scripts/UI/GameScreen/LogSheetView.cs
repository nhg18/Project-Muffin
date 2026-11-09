using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 전체 기록 서랍 (10-ui.md 2-1 HUD v2 「기록」) — 오른쪽 위 설정 버튼 아래에 열리는 판. 턴 머리글(점선 위 회색)과 항목(「이름 · 무엇을」 + 빨간 수치), 최신이 위.
    /// 줄은 템플릿을 복제한다. 기본은 비활성, 닫기 버튼으로 닫는다.
    /// </summary>
    public class LogSheetView : MonoBehaviour
    {
        private const string EntryFormat = "{0} · {1}";

        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform content;
        [Tooltip("턴 머리글 줄 — 비활성으로 둔다. 구조: 루트 → Line · Text.")]
        [SerializeField] private RectTransform headerTemplate;
        [Tooltip("항목 줄 — 비활성으로 둔다. 구조: 루트 → Text · Value.")]
        [SerializeField] private RectTransform entryTemplate;
        [SerializeField] private ScrollRect scroll;

        private readonly List<GameObject> _rows = new List<GameObject>();

        /// <summary>닫기를 눌렀다.</summary>
        public event Action CloseClicked;

        private void Awake()
        {
            if (closeButton == null || content == null || headerTemplate == null || entryTemplate == null || scroll == null)
                Debug.LogError($"[{nameof(LogSheetView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        /// <summary>
        /// 기록 전체를 다시 그린다. entries 는 일어난 순서(오래된 것부터). 턴 머리글 단위로 묶어 최신 턴부터, 턴 안에서도 최신 항목부터 보여 준다 (시안 logHTML).
        /// nameOf · inkOf 로 actorNumber 를 표시 이름 · 글자색으로 바꾼다.
        /// </summary>
        public void Show(IReadOnlyList<HistoryEntry> entries, Func<int, string> nameOf, Func<int, Color> inkOf)
        {
            foreach (GameObject row in _rows)
                Destroy(row);
            _rows.Clear();

            // 묶기: 머리글 뒤에 오는 항목들이 한 묶음. 머리글 없이 시작한 항목은 머리글 없는 첫 묶음
            var groups = new List<(string header, List<HistoryEntry> items)>();
            foreach (HistoryEntry entry in entries)
            {
                if (entry.IsHeader)
                    groups.Add((entry.Header, new List<HistoryEntry>()));
                else
                {
                    if (groups.Count == 0)
                        groups.Add((null, new List<HistoryEntry>()));
                    groups[groups.Count - 1].items.Add(entry);
                }
            }

            for (int g = groups.Count - 1; g >= 0; g--)
            {
                (string header, List<HistoryEntry> items) = groups[g];
                if (header != null)
                    AddHeader(header, g == groups.Count - 1);
                for (int i = items.Count - 1; i >= 0; i--)
                    AddEntry(items[i], nameOf, inkOf);
            }

            gameObject.SetActive(true);
            scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide() => gameObject.SetActive(false);

        // 첫 머리글은 점선 없이 (시안 .log .d:first-child)
        private void AddHeader(string header, bool first)
        {
            RectTransform row = Instantiate(headerTemplate, content);
            row.gameObject.SetActive(true);
            row.Find("Line").gameObject.SetActive(!first);
            row.Find("Text").GetComponent<TMP_Text>().text = header;
            _rows.Add(row.gameObject);
        }

        private void AddEntry(HistoryEntry entry, Func<int, string> nameOf, Func<int, Color> inkOf)
        {
            RectTransform row = Instantiate(entryTemplate, content);
            row.gameObject.SetActive(true);

            string ink = ColorUtility.ToHtmlStringRGB(inkOf(entry.Actor));
            row.Find("Text").GetComponent<TMP_Text>().text = string.Format(EntryFormat, $"<color=#{ink}>{nameOf(entry.Actor)}</color>", entry.Text);

            TMP_Text value = row.Find("Value").GetComponent<TMP_Text>();
            value.text = entry.Value;
            value.gameObject.SetActive(!string.IsNullOrEmpty(entry.Value));
            _rows.Add(row.gameObject);
        }

        private void HandleCloseClicked() => CloseClicked?.Invoke();
    }
}
