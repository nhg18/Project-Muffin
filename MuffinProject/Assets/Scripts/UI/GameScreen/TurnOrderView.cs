using System.Collections.Generic;
using Chapchu.UI.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 오른쪽 세로 턴 순서 (통합 화면 UI v3 .tord) — 위에 「턴 방향」 알약, 그 아래 둥근 기둥 안에 사람마다 한 줄(TurnOrderRowView 프리팹 복제).
    /// 지금 턴인 줄은 얼굴 원이 커지고, 왼쪽에 꼬리표(이름 + 형광펜 · 남은 초 배지)가 붙는다. 꼬리표는 하나를 지금 줄 높이로 옮겨 쓴다 — 남은 초 배지는 TurnTimerPresenter 가 채운다.
    /// 역방향이면 알약이 주황 · 화살표가 위로, 꺾쇠도 위를 가리킨다. 값을 받아 그리기만 한다 — 누가 몇 번째인지는 TurnOrderPresenter 가 정한다.
    /// </summary>
    public class TurnOrderView : MonoBehaviour
    {
        [Header("「턴 방향」 알약")]
        [SerializeField] private UIShape headerFill;
        [Tooltip("진행 방향 화살표 — 정방향은 아래, 역방향이면 위.")]
        [SerializeField] private RectTransform directionArrow;
        [SerializeField] private Color headerColor = Color.white;
        [Tooltip("역방향일 때 알약 색 (시안 .tord .hd.rev).")]
        [SerializeField] private Color headerReversedColor = new Color32(0xff, 0xd9, 0xa8, 0xFF);

        [Header("줄")]
        [Tooltip("둥근 기둥 — 줄 수에 맞춰 높이를 바꾼다.")]
        [SerializeField] private RectTransform track;
        [Tooltip("줄을 담는 곳 — 첫 줄 위끝이 0.")]
        [SerializeField] private RectTransform rows;
        [SerializeField] private TurnOrderRowView rowPrefab;
        [Tooltip("줄 간격 (줄 124 + 꺾쇠 28).")]
        [SerializeField] private float rowPitch = 152f;
        [Tooltip("기둥 높이 = 줄 간격 × 줄 수 + 이 값 (위 여백 20 + 아래 여백 16 − 마지막 꺾쇠 28).")]
        [SerializeField] private float trackExtra = 8f;

        [Header("지금 턴 꼬리표")]
        [SerializeField] private RectTransform currentTab;
        [SerializeField] private TMP_Text currentNameText;
        [SerializeField] private LayoutElement currentNameLayout;
        [Tooltip("이름 아래 형광펜 띠.")]
        [SerializeField] private Graphic currentBand;
        [SerializeField] private GameObject currentCrown;
        [Tooltip("꼬리표 가운데 높이 — 줄 위끝에서 아래로 (지금 턴 얼굴 원의 가운데).")]
        [SerializeField] private float currentTabCenter = 62f;
        [Tooltip("꼬리표 이름 최대 폭 — 넘치면 … .")]
        [SerializeField] private float maxCurrentNameWidth = 220f;
        [SerializeField] private Color turnBandColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [Tooltip("찹츄면 형광펜이 분홍 + 왕관 (시안 .tord .tab.chu).")]
        [SerializeField] private Color chapchuBandColor = new Color32(0xff, 0xb3, 0xcf, 0xFF);

        private readonly List<TurnOrderRowView> _rows = new List<TurnOrderRowView>();
        private readonly List<string> _names = new List<string>();
        private readonly List<bool> _chapchu = new List<bool>();
        private int _current = -1;
        private bool _reversed;

        private void Awake()
        {
            if (headerFill == null || directionArrow == null || track == null || rows == null || rowPrefab == null
                || currentTab == null || currentNameText == null || currentNameLayout == null || currentBand == null || currentCrown == null)
                Debug.LogError($"[{nameof(TurnOrderView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>줄을 순서대로 다시 만든다 — 이름 · 얼굴.</summary>
        public void SetPlayers(IReadOnlyList<(string name, Sprite face)> players)
        {
            foreach (TurnOrderRowView row in _rows)
                Destroy(row.gameObject);
            _rows.Clear();
            _names.Clear();
            _chapchu.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                TurnOrderRowView row = Instantiate(rowPrefab, rows);
                row.gameObject.SetActive(true);
                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -i * rowPitch);
                row.SetPlayer(players[i].name, players[i].face);
                row.SetCurrent(false);
                row.SetChevron(i < players.Count - 1, _reversed);
                _rows.Add(row);
                _names.Add(players[i].name);
                _chapchu.Add(false);
            }

            track.sizeDelta = new Vector2(track.sizeDelta.x, players.Count > 0 ? rowPitch * players.Count + trackExtra : 0f);
            track.gameObject.SetActive(players.Count > 0);
            SetCurrent(_current < players.Count ? _current : -1);
        }

        /// <summary>지금 턴인 줄 — 얼굴 원을 키우고 꼬리표를 그 높이에. 음수면 꼬리표를 숨긴다.</summary>
        public void SetCurrent(int index)
        {
            _current = index >= 0 && index < _rows.Count ? index : -1;
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetCurrent(i == _current);

            currentTab.gameObject.SetActive(_current >= 0);
            if (_current < 0) return;

            currentTab.anchoredPosition = new Vector2(currentTab.anchoredPosition.x, -(_current * rowPitch + currentTabCenter));
            string playerName = _names[_current];
            currentNameText.text = playerName;
            currentNameLayout.preferredWidth = Mathf.Min(currentNameText.GetPreferredValues(playerName).x, maxCurrentNameWidth);
            RefreshTab();
        }

        public void SetHp(int index, float fraction)
        {
            if (index >= 0 && index < _rows.Count)
                _rows[index].SetHp(fraction);
        }

        public void SetDead(int index, bool dead)
        {
            if (index >= 0 && index < _rows.Count)
                _rows[index].SetDead(dead);
        }

        /// <summary>찹츄 상태 — 그 사람이 지금 턴이면 꼬리표 형광펜이 분홍 + 왕관.</summary>
        public void SetChapchu(int index, bool declared)
        {
            if (index < 0 || index >= _chapchu.Count) return;

            _chapchu[index] = declared;
            if (index == _current)
                RefreshTab();
        }

        /// <summary>진행 방향 — 역방향이면 알약이 주황 · 화살표 · 꺾쇠가 위로.</summary>
        public void SetReversed(bool reversed)
        {
            _reversed = reversed;
            headerFill.color = reversed ? headerReversedColor : headerColor;
            directionArrow.localRotation = Quaternion.Euler(0f, 0f, reversed ? 180f : 0f);
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetChevron(i < _rows.Count - 1, reversed);
        }

        private void RefreshTab()
        {
            bool chapchu = _chapchu[_current];
            currentBand.color = chapchu ? chapchuBandColor : turnBandColor;
            currentCrown.SetActive(chapchu);
        }
    }
}
