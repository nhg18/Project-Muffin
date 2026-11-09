using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// HP 뼈다귀 게이지 (10-ui.md 2-1 HUD v2 「HP 뼈다귀」). 막대 채움은 HP 비율, 양 끝 마디는 0 초과 · 최대일 때만 색이 든다.
    /// 색은 51 이상 초록 · 21 ~ 50 주황 · 20 이하 빨강 — 표시 연출일 뿐 규칙이 아니다. 값을 받아 그리기만 한다.
    /// </summary>
    public class BoneGaugeView : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [Tooltip("왼쪽 끝 마디 2개 — HP 가 0 보다 크면 색이 든다.")]
        [SerializeField] private Image[] leftCaps;
        [Tooltip("오른쪽 끝 마디 2개 — HP 가 최대일 때만 색이 든다.")]
        [SerializeField] private Image[] rightCaps;

        [Header("색 (HUD v2)")]
        [Tooltip("HP 가 이 값보다 크면 초록.")]
        [SerializeField] private int highThreshold = 50;
        [Tooltip("HP 가 이 값보다 크면 주황, 이하면 빨강.")]
        [SerializeField] private int lowThreshold = 20;
        [SerializeField] private Color highColor = new Color32(0x3c, 0xbf, 0x72, 0xFF);
        [SerializeField] private Color midColor = new Color32(0xff, 0x9f, 0x1a, 0xFF);
        [SerializeField] private Color lowColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color emptyColor = new Color32(0xff, 0xe6, 0xe3, 0xFF);

        private void Awake()
        {
            if (fill == null || fillImage == null || leftCaps == null || rightCaps == null)
                Debug.LogError($"[{nameof(BoneGaugeView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>HP 와 최대 HP 로 채움 폭 · 색을 맞춘다.</summary>
        public void SetHp(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            Color color = hp > highThreshold ? highColor : hp > lowThreshold ? midColor : lowColor;

            fill.anchorMax = new Vector2(ratio, 1f);
            fillImage.color = color;

            Color left = hp > 0 ? color : emptyColor;
            Color right = hp >= maxHp ? color : emptyColor;
            foreach (Image cap in leftCaps)
                cap.color = left;
            foreach (Image cap in rightCaps)
                cap.color = right;
        }
    }
}
