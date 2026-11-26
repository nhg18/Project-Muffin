using Chapchu.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 손패 장수 — 카드 두 장을 겹친 그림 + 앞 카드의 장수 (통합 화면 UI v3 .hcI). 앞 카드의 면 · 글자색이 상태:
    /// 보통 흰 바탕 · 9장 주황(찹츄 한 장 전) · 정확히 10장 분홍. 값을 받아 그리기만 한다.
    /// </summary>
    public class HandCountBadgeView : MonoBehaviour
    {
        private const int ChapchuCount = GameServer.ChapchuHandCount; // 07-win-condition.md 4.1
        private const int NearCount = ChapchuCount - 1; // 한 장 모자라면 「가깝다」 (주황)

        [SerializeField] private TMP_Text countText;
        [Tooltip("앞 카드의 면 — 상태 색을 칠한다.")]
        [SerializeField] private Graphic face;

        [Header("색 (통합 화면 UI v3 .hcI)")]
        [SerializeField] private Color normalFace = Color.white;
        [SerializeField] private Color normalInk = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color nearFace = new Color32(0xff, 0xf1, 0xe0, 0xFF);
        [SerializeField] private Color nearInk = new Color32(0xb3, 0x3f, 0x00, 0xFF);
        [SerializeField] private Color tenFace = new Color32(0xff, 0xe3, 0xee, 0xFF);
        [SerializeField] private Color tenInk = new Color32(0xc8, 0x19, 0x5a, 0xFF);

        private void Awake()
        {
            if (countText == null || face == null)
                Debug.LogError($"[{nameof(HandCountBadgeView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetCount(int count)
        {
            countText.SetText("{0}", count);

            bool ten = count == ChapchuCount;
            bool near = count == NearCount;
            face.color = ten ? tenFace : near ? nearFace : normalFace;
            countText.color = ten ? tenInk : near ? nearInk : normalInk;
        }
    }
}
