using System;
using System.Collections.Generic;
using Chapchu.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 오른쪽 위 「방금 일어난 일」 칩 (10-ui.md 2-1 HUD v2 「기록」). 최신 2개만 — 둘째는 조금 흐리고, 4초 지나면 더 흐려진다.
    /// 전체를 누르면 기록 서랍이 열린다 (Clicked). 칩은 템플릿을 복제한다 — 구조: 루트 → Icon · MiniCard(→ Band) · Crown · Name · Text · Value.
    /// </summary>
    public class FeedView : MonoBehaviour, IPointerClickHandler
    {
        private const int MaxChips = 2;

        [SerializeField] private RectTransform chipTemplate;
        [Tooltip("둘째 칩의 투명도 (시안 .old 0.8).")]
        [SerializeField] private float oldAlpha = 0.8f;
        [Tooltip("이 초가 지난 칩의 투명도 (시안 .stale 0.55).")]
        [SerializeField] private float staleAlpha = 0.55f;
        [SerializeField] private float staleAfter = 4f;
        [Tooltip("카드 종류 색 — 행동 · 카운터 · 함정 (시안 KC).")]
        [SerializeField] private Color actionColor = new Color32(0x00, 0x9d, 0xff, 0xFF);
        [SerializeField] private Color counterColor = new Color32(0x6f, 0xa4, 0x34, 0xFF);
        [SerializeField] private Color trapColor = new Color32(0xf6, 0x46, 0x12, 0xFF);

        private readonly List<Chip> _chips = new List<Chip>();

        /// <summary>칩 영역을 눌렀다 — 전체 기록 열기.</summary>
        public event Action Clicked;

        private void Awake()
        {
            if (chipTemplate == null)
                Debug.LogError($"[{nameof(FeedView)}] {nameof(chipTemplate)} 참조가 비어 있습니다.", this);
        }

        private void Update()
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                Chip chip = _chips[i];
                bool stale = Time.unscaledTime - chip.ShownAt >= staleAfter;
                chip.Group.alpha = stale ? staleAlpha : i == 0 ? 1f : oldAlpha;
            }
        }

        /// <summary>새 항목을 맨 위에 넣는다. 2개를 넘으면 가장 오래된 것을 지운다.</summary>
        public void Push(HistoryEntry entry, string actorName, Color actorInk)
        {
            var chip = new Chip(Instantiate(chipTemplate, chipTemplate.parent), Time.unscaledTime);
            chip.Root.gameObject.SetActive(true);
            chip.Root.SetAsFirstSibling();
            chip.Fill(entry, actorName, actorInk, CardColor(entry.CardType));
            _chips.Insert(0, chip);

            while (_chips.Count > MaxChips)
            {
                Destroy(_chips[_chips.Count - 1].Root.gameObject);
                _chips.RemoveAt(_chips.Count - 1);
            }
        }

        /// <summary>기록 서랍이 열려 있는 동안 숨긴다.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke();
        }

        private Color CardColor(CardType type) => type == CardType.Counter ? counterColor : type == CardType.Trap ? trapColor : actionColor;

        private class Chip
        {
            public readonly RectTransform Root;
            public readonly CanvasGroup Group;
            public readonly float ShownAt;
            private readonly GameObject _icon;
            private readonly GameObject _miniCard;
            private readonly Image _miniBand;
            private readonly GameObject _crown;
            private readonly TMP_Text _name;
            private readonly LayoutElement _nameLayout;
            private readonly TMP_Text _text;
            private readonly TMP_Text _value;

            public Chip(RectTransform root, float shownAt)
            {
                Root = root;
                ShownAt = shownAt;
                Group = root.GetComponent<CanvasGroup>();
                _icon = root.Find("Icon").gameObject;
                _miniCard = root.Find("MiniCard").gameObject;
                _miniBand = root.Find("MiniCard/Band").GetComponent<Image>();
                _crown = root.Find("Crown").gameObject;
                _name = root.Find("Name").GetComponent<TMP_Text>();
                _nameLayout = _name.GetComponent<LayoutElement>();
                _text = root.Find("Text").GetComponent<TMP_Text>();
                _value = root.Find("Value").GetComponent<TMP_Text>();
            }

            public void Fill(HistoryEntry entry, string actorName, Color actorInk, Color cardColor)
            {
                _icon.SetActive(entry.Icon == HistoryIcon.Card);
                _miniCard.SetActive(entry.Icon == HistoryIcon.MiniCard);
                _crown.SetActive(entry.Icon == HistoryIcon.Crown);
                _miniBand.color = cardColor;
                _name.text = actorName;
                _nameLayout.minWidth = _name.GetPreferredValues(actorName).x; // 칩이 넘치면 이름은 그대로, 내용(Text)만 … 로 줄인다
                _name.color = actorInk;
                _text.text = entry.Text;
                _value.text = entry.Value;
                _value.gameObject.SetActive(!string.IsNullOrEmpty(entry.Value));
            }
        }
    }
}
