using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 왼쪽 히스토리 패널. 세로 목록(최신이 위)에 항목을 받아 쌓기만 한다 (10-ui.md 2-1).
    /// 항목(카드 썸네일 + 캡션)의 모양은 아직 없다 — 이후 작업이 항목 프리팹을 만들어 <see cref="AddEntry"/> 로 넣는다.
    /// </summary>
    public class HistoryView : MonoBehaviour
    {
        [Tooltip("목록에 남겨 두는 최대 항목 수. 넘치면 가장 오래된(아래) 항목을 지운다. 시안은 8.")]
        [SerializeField] private int maxEntries = 8;
        [SerializeField] private RectTransform content;

        /// <summary>항목이 들어가는 세로 목록 컨테이너 (VerticalLayoutGroup).</summary>
        public RectTransform Content => content;

        private void Awake()
        {
            if (content == null)
                Debug.LogError($"[{nameof(HistoryView)}] {nameof(content)} 참조가 비어 있습니다.", this);
        }

        /// <summary>항목을 맨 위에 넣는다. 최대 수를 넘기면 맨 아래부터 지운다.</summary>
        public void AddEntry(RectTransform entry)
        {
            entry.SetParent(content, false);
            entry.SetAsFirstSibling();

            // Destroy 는 프레임 끝에 실행되므로 먼저 부모에서 떼어 childCount 를 줄인다.
            while (content.childCount > maxEntries)
            {
                Transform oldest = content.GetChild(content.childCount - 1);
                oldest.SetParent(null, false);
                Destroy(oldest.gameObject);
            }
        }

        public void Clear()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }
    }
}
