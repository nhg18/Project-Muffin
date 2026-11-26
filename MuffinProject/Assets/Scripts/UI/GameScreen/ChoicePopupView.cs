using System;
using System.Collections.Generic;
using Chapchu.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 효과 고르기 판 — 선택지 버튼 2개 (프로토타입 v2 `.pop2.pick` · G.choose opts, 11-card-list 1019).
    /// 머리 띠: 발 아이콘 + 제목 + 남은 초(오른쪽). 몸통: 설명 + 버튼 줄(첫 버튼이 주 버튼). 받은 값을 표시만 한다.
    /// 「손패에서 1장」 고르기 (2013 · 2021 의 대가, 10-ui.md 5절)는 같은 판에 버튼 줄 대신 카드 칸을 늘어놓는다 — 한 줄 5장, 넘치면 다음 줄, 10장을 넘으면 칸을 줄여 한 줄 7장.
    /// </summary>
    public class ChoicePopupView : MonoBehaviour
    {
        private const string SecondsFormat = "{0}초";
        private const int WideColumns = 5;       // 10장까지 — 한 줄 5칸
        private const int NarrowColumns = 7;     // 그보다 많으면 칸을 줄여 한 줄 7칸
        private const int WideLimit = 10;
        private static readonly Vector2 WideCell = new Vector2(150f, 210f);
        private static readonly Vector2 NarrowCell = new Vector2(106f, 150f);
        private const float WideSpacing = 16f, NarrowSpacing = 12f;

        [Tooltip("켜고 끄는 판 (바탕 가림 포함) — 이 컴포넌트의 오브젝트는 켜 둔다.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private Button firstButton;
        [SerializeField] private TMP_Text firstLabel;
        [SerializeField] private Button secondButton;
        [SerializeField] private TMP_Text secondLabel;
        [Tooltip("버튼 줄 — 카드 고르기일 때 끈다.")]
        [SerializeField] private GameObject buttonsRow;
        [Tooltip("카드 칸을 늘어놓는 자리 (GridLayoutGroup) — 선택지 고르기일 때 끈다.")]
        [SerializeField] private GridLayoutGroup cardsGrid;
        [Tooltip("카드 한 칸의 프리팹 (Prefab/UI/GameScreen/ChoiceCardTile) — 손패 장수만큼 만들어 쓴다.")]
        [SerializeField] private ChoiceCardTile tilePrefab;

        private readonly List<ChoiceCardTile> _tiles = new List<ChoiceCardTile>();

        public event Action<int> Picked;     // 0 첫 버튼 · 1 둘째 버튼
        public event Action<int> CardPicked; // 고른 카드의 장 번호

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel == null || titleText == null || secondsText == null || descText == null || firstButton == null || firstLabel == null || secondButton == null || secondLabel == null
                || buttonsRow == null || cardsGrid == null || tilePrefab == null)
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
            buttonsRow.SetActive(true);
            cardsGrid.gameObject.SetActive(false);
            panel.SetActive(true);
        }

        /// <summary>손패에서 1장 고르기 — 카드마다 한 칸. instanceIds 와 cards 는 같은 순서.</summary>
        public void ShowCards(string title, string desc, IReadOnlyList<int> instanceIds, IReadOnlyList<CardData> cards)
        {
            titleText.text = title;
            descText.text = desc;
            buttonsRow.SetActive(false);

            bool wide = cards.Count <= WideLimit;
            cardsGrid.cellSize = wide ? WideCell : NarrowCell;
            cardsGrid.spacing = Vector2.one * (wide ? WideSpacing : NarrowSpacing);
            cardsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cardsGrid.constraintCount = wide ? WideColumns : NarrowColumns;

            while (_tiles.Count < cards.Count)
            {
                ChoiceCardTile tile = Instantiate(tilePrefab, cardsGrid.transform);
                tile.Clicked += id => CardPicked?.Invoke(id);
                _tiles.Add(tile);
            }
            for (int i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].gameObject.SetActive(i < cards.Count);
                if (i < cards.Count)
                    _tiles[i].Set(instanceIds[i], cards[i]);
            }

            cardsGrid.gameObject.SetActive(true);
            panel.SetActive(true);
        }

        public void SetSeconds(int seconds) => secondsText.text = string.Format(SecondsFormat, seconds);

        public void Hide() => panel.SetActive(false);
    }
}
