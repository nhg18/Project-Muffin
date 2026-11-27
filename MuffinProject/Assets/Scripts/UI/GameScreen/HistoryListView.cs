using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 왼쪽 「낸 카드 기록」 (통합 화면 UI v3 .hist) — 새 줄이 맨 위에 튀어나오고 아래로 밀린다. 보이는 줄 수는 영역 높이 ÷ 줄 간격 (3 ~ 7, 1080 높이면 4).
    /// 넘친 줄은 지운다. 줄 내용은 HistoryPresenter 가 넣는다 — 여기는 줄을 만들고 놓기만 한다.
    /// </summary>
    public class HistoryListView : MonoBehaviour
    {
        [SerializeField] private HistoryItemView itemPrefab;
        [Tooltip("줄을 담는 곳 — 위끝에서 아래로 쌓는다. 높이가 보이는 줄 수를 정한다.")]
        [SerializeField] private RectTransform content;
        [Tooltip("줄 간격 (줄 128 + 틈 20).")]
        [SerializeField] private float pitch = 148f;
        [SerializeField] private int minItems = 3;
        [SerializeField] private int maxItems = 7;

        private readonly List<HistoryItemView> _items = new List<HistoryItemView>(); // 0 = 가장 새 줄
        private readonly List<object> _keys = new List<object>();

        private void Awake()
        {
            if (itemPrefab == null || content == null)
                Debug.LogError($"[{nameof(HistoryListView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>맨 위에 새 줄을 만든다 — key 로 나중에 다시 찾는다(결과가 붙을 때). 내용은 돌려받은 줄에 넣는다.</summary>
        public HistoryItemView Push(object key)
        {
            HistoryItemView item = Instantiate(itemPrefab, content);
            item.gameObject.SetActive(true);
            _items.Insert(0, item);
            _keys.Insert(0, key);

            int capacity = Mathf.Clamp(Mathf.FloorToInt(content.rect.height / pitch), minItems, maxItems);
            while (_items.Count > capacity)
            {
                int last = _items.Count - 1;
                Destroy(_items[last].gameObject);
                _items.RemoveAt(last);
                _keys.RemoveAt(last);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                var rect = (RectTransform)_items[i].transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -i * pitch);
            }
            item.PlayPop();
            return item;
        }

        /// <summary>key 로 만든 줄 — 이미 밀려나 지워졌으면 null.</summary>
        public HistoryItemView Find(object key)
        {
            int index = _keys.IndexOf(key);
            return index >= 0 ? _items[index] : null;
        }

        /// <summary>줄을 모두 지운다.</summary>
        public void Clear()
        {
            foreach (HistoryItemView item in _items)
                Destroy(item.gameObject);
            _items.Clear();
            _keys.Clear();
        }
    }
}
