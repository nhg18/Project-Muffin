using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 「판 기록」 판의 한 줄 (시안 .board tr) — 지표 이름 · 사람마다 값(1등 칸은 노란 알약) · 그 지표의 업적.
    /// </summary>
    public class ResultBoardRowView : MonoBehaviour
    {
        /// <summary>한 사람의 값 칸.</summary>
        [Serializable]
        public class Cell
        {
            public GameObject root;
            [Tooltip("1등일 때 칸에 까는 옅은 노랑.")]
            public Graphic highlight;
            [Tooltip("1등일 때 값을 감싸는 노란 알약.")]
            public Graphic pill;
            public TMP_Text text;
        }

        private const string NoAward = "받은 사람 없음 (모두 0)";

        [SerializeField] private TMP_Text metricText;
        [Tooltip("사람 칸 — 왼쪽부터 순위 순. 사람이 적으면 남는 칸은 끈다.")]
        [SerializeField] private Cell[] cells;
        [Tooltip("업적 — 아이콘 원 + 이름. 받은 사람이 없으면 끈다.")]
        [SerializeField] private GameObject award;
        [SerializeField] private Image awardIcon;
        [SerializeField] private TMP_Text awardText;
        [SerializeField] private TMP_Text noneText;

        private void Awake()
        {
            if (metricText == null || cells == null || award == null || awardIcon == null || awardText == null || noneText == null)
                Debug.LogError($"[{nameof(ResultBoardRowView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>한 지표를 그린다. values · tops 는 순위 순 (사람 칸과 같은 자리).</summary>
        public void Show(string metric, IReadOnlyList<int> values, IReadOnlyList<bool> tops, Sprite icon, string awardName)
        {
            metricText.text = metric;
            for (int i = 0; i < cells.Length; i++)
            {
                bool used = i < values.Count;
                cells[i].root.SetActive(used);
                if (!used)
                    continue;

                cells[i].text.text = values[i].ToString();
                cells[i].highlight.enabled = tops[i];
                cells[i].pill.enabled = tops[i];
            }

            bool hasWinner = tops.Any(top => top);
            award.SetActive(hasWinner);
            noneText.gameObject.SetActive(!hasWinner);
            noneText.text = NoAward;
            awardIcon.sprite = icon;
            awardText.text = awardName;
        }
    }
}
