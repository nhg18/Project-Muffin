using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 사용 기준선 — 카드를 끄는 동안만 보이는 가로 줄과 「이 선 위에서 놓으면 사용」 (10-ui.md 4절 「끌다가 놓기」, 프로토타입 v2 #playline).
    /// 끄는 카드가 선을 넘으면(놓으면 사용) 줄이 노랑이 되고 글자는 숨는다. 값을 받아 표시만 한다 — 판정은 HandPresenter.
    /// </summary>
    public class PlayLineView : MonoBehaviour
    {
        [SerializeField] private Graphic line;
        [SerializeField] private GameObject label;
        [SerializeField] private Color normalColor = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.32f);
        [SerializeField] private Color armedColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);

        private void Awake()
        {
            if (line == null || label == null)
                Debug.LogError($"[{nameof(PlayLineView)}] {nameof(line)} · {nameof(label)} 참조 중 비어 있는 것이 있습니다.", this);

            SetVisible(false);
        }

        /// <summary>끄는 동안 켠다.</summary>
        public void SetVisible(bool visible)
        {
            line.gameObject.SetActive(visible);
            label.SetActive(visible);
            if (visible)
                SetArmed(false);
        }

        /// <summary>끄는 카드가 놓으면 사용되는 자리에 있다 — 줄을 노랑으로, 글자는 숨긴다.</summary>
        public void SetArmed(bool armed)
        {
            line.color = armed ? armedColor : normalColor;
            label.SetActive(!armed && line.gameObject.activeSelf);
        }
    }
}
