using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 판 전체에 걸린 효과 줄 — 턴 순서 띠 아래 가운데, 모두에게 같은 내용 (프로토타입 v2 `.gfx` · `.gf`, 게시본 1794551255-93d8).
    /// 알약 하나 = 주황 이름표(예: 「피폭」) + 설명. 알약은 템플릿을 복제해 만든다 (구조: 루트 → Label/Text · Text). 값을 받아 그리기만 한다.
    /// </summary>
    public class BoardEffectsView : MonoBehaviour
    {
        [Tooltip("복제할 알약 — 비활성으로 둔다. 구조: 루트 → Label(주황 이름표) → Text · Text(설명).")]
        [SerializeField] private RectTransform pillTemplate;

        private readonly List<GameObject> _pills = new List<GameObject>();

        private void Awake()
        {
            if (pillTemplate == null)
                Debug.LogError($"[{nameof(BoardEffectsView)}] {nameof(pillTemplate)} 참조가 비어 있습니다.", this);
        }

        /// <summary>효과 목록을 순서대로 다시 그린다 — (이름표, 설명). 비면 줄이 사라진다.</summary>
        public void Set(IReadOnlyList<(string label, string text)> effects)
        {
            foreach (GameObject pill in _pills)
                Destroy(pill);
            _pills.Clear();

            foreach ((string label, string text) in effects)
            {
                RectTransform pill = Instantiate(pillTemplate, pillTemplate.parent);
                pill.gameObject.SetActive(true);
                pill.Find("Label/Text").GetComponent<TMP_Text>().text = label;
                pill.Find("Text").GetComponent<TMP_Text>().text = text;
                _pills.Add(pill.gameObject);
            }
        }
    }
}
