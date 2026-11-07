using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 손패 장수 배지 — 판 오른쪽 위 모서리의 카드 2장 (10-ui.md 2-1 HUD v2 「손패 카드」). 띠 색이 상태:
    /// 보통 파랑 · 9장 또는 11장 이상 주황(찹츄가 가깝다) · 정확히 10장 분홍. 값을 받아 그리기만 한다.
    /// </summary>
    public class HandCountBadgeView : MonoBehaviour
    {
        private const int ChapchuCount = 10; // 07-win-condition.md 4.1
        private const int NearCount = 9;

        [SerializeField] private TMP_Text countText;
        [SerializeField] private Image band;
        [SerializeField] private Image face;
        [Tooltip("10 이상(두 자리)이면 글자를 이 크기로 줄인다. 0 이면 그대로.")]
        [SerializeField] private float wideFontSize;

        [Header("색 (HUD v2)")]
        [SerializeField] private Color normalBand = new Color32(0x00, 0x9d, 0xff, 0xFF);
        [SerializeField] private Color normalFace = Color.white;
        [SerializeField] private Color normalInk = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color nearBand = new Color32(0xff, 0x9f, 0x1a, 0xFF);
        [SerializeField] private Color nearFace = new Color32(0xff, 0xf1, 0xe0, 0xFF);
        [SerializeField] private Color nearInk = new Color32(0xb3, 0x3f, 0x00, 0xFF);
        [SerializeField] private Color tenBand = new Color32(0xff, 0x4f, 0x8b, 0xFF);
        [SerializeField] private Color tenFace = new Color32(0xff, 0xe3, 0xee, 0xFF);
        [SerializeField] private Color tenInk = new Color32(0xc8, 0x19, 0x5a, 0xFF);

        private float _baseFontSize;

        private void Awake()
        {
            if (countText == null || band == null || face == null)
                Debug.LogError($"[{nameof(HandCountBadgeView)}] 참조 중 비어 있는 것이 있습니다.", this);
            else
                _baseFontSize = countText.fontSize;
        }

        public void SetCount(int count)
        {
            countText.text = count.ToString();
            if (wideFontSize > 0f)
                countText.fontSize = count >= ChapchuCount ? wideFontSize : _baseFontSize;

            bool ten = count == ChapchuCount;
            bool near = count >= NearCount && !ten;
            band.color = ten ? tenBand : near ? nearBand : normalBand;
            face.color = ten ? tenFace : near ? nearFace : normalFace;
            countText.color = ten ? tenInk : near ? nearInk : normalInk;
        }
    }
}
