using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 「판 기록」 판 (07-win-condition.md 11-2, 시안 .board) — 지표 8줄 × 사람(순위 순) + 지표마다의 업적. 「판 기록」 버튼으로 연다.
    /// 닫는 길은 ✕ 버튼 · Esc(Android 뒤로가기)뿐 — 바탕을 눌러도 닫히지 않는다 (22-popup-ui.md 1절 #4).
    /// 씬에는 닫힌 채(꺼진 채)로 저장한다.
    /// </summary>
    public class ResultBoardView : MonoBehaviour
    {
        /// <summary>머리 줄의 한 사람 — 색 점 · 이름 · 「나」.</summary>
        [Serializable]
        public class Header
        {
            public GameObject root;
            public Graphic dot;
            public TMP_Text nameText;
            public GameObject meChip;
        }

        [Tooltip("바탕 + 판 — 닫혀 있으면 꺼져 있다.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [Tooltip("머리 줄의 사람 칸 — 왼쪽부터 순위 순. 사람이 적으면 남는 칸은 끈다.")]
        [SerializeField] private Header[] headers;
        [Tooltip("지표 줄이 쌓이는 자리.")]
        [SerializeField] private RectTransform rowArea;
        [SerializeField] private ResultBoardRowView rowPrefab;

        private readonly List<ResultBoardRowView> _rows = new List<ResultBoardRowView>();

        public bool IsOpen => panel.activeSelf;

        /// <summary>✕ 를 눌렀다.</summary>
        public event Action CloseClicked;

        private void Awake()
        {
            if (panel == null || closeButton == null || headers == null || rowArea == null || rowPrefab == null)
                Debug.LogError($"[{nameof(ResultBoardView)}] 참조 중 비어 있는 것이 있습니다.", this);

            closeButton.onClick.AddListener(() => CloseClicked?.Invoke());
        }

        /// <summary>머리 줄 — 사람은 순위 순.</summary>
        public void ShowPlayers(IReadOnlyList<string> names, IReadOnlyList<Color> colors, int meIndex)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                bool used = i < names.Count;
                headers[i].root.SetActive(used);
                if (!used)
                    continue;

                headers[i].dot.color = colors[i];
                headers[i].nameText.text = names[i];
                headers[i].meChip.SetActive(i == meIndex);
            }
        }

        /// <summary>지표 한 줄을 아래에 더한다.</summary>
        public void AddRow(string metric, IReadOnlyList<int> values, IReadOnlyList<bool> tops, Sprite icon, string award)
        {
            ResultBoardRowView row = Instantiate(rowPrefab, rowArea);
            row.Show(metric, values, tops, icon, award);
            _rows.Add(row);
        }

        public void SetOpen(bool isOpen) => panel.SetActive(isOpen);
    }
}
