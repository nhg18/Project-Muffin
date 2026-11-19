using Chapchu.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>찹츄 자리의 표시 상태 (10-ui.md 2-1 「찹츄 버튼 상태」). 선언은 자동이라 입력이 아니다 (07-win-condition.md 4.1).</summary>
    public enum ChapchuButtonState
    {
        NotDeclared, // 내가 찹츄 상태가 아니다 — 점 10개로 손패 수
        Declared,    // 내가 찹츄 상태 — 내 턴이 시작될 때 10장이면 승리
    }

    /// <summary>
    /// 우하단 찹츄 자리 = 외치는 말풍선 모양 (10-ui.md 2-1 HUD v2 「찹츄 버튼」). 선언 전: 회색 「찹츄!」 + 점 10개(손패 수). 선언 뒤: 분홍 · 「찹츄 완료」.
    /// 표시만 한다 — 누를 수 없다 (2026-11-14 자동 선언. 이 자리의 쓰임은 미정, 10-ui.md 11절 #21). 「지금 외치기!」 꼬리표 · 느낌 아이콘은 꺼 둔다.
    /// </summary>
    public class ChapchuButtonView : MonoBehaviour
    {
        private const int PipCount = GameServer.ChapchuHandCount; // 07-win-condition.md 4.1
        private const string OverFormat = "+{0}";

        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private Image tailFill;
        [SerializeField] private Image shadow;
        [SerializeField] private TMP_Text label;
        [Tooltip("손패 수 점 10개 (안쪽 채움). 켜진 점은 분홍.")]
        [SerializeField] private Image[] pips = new Image[PipCount];
        [SerializeField] private GameObject pipsRow;
        [SerializeField] private TMP_Text overText;
        [SerializeField] private GameObject readyTag;
        [SerializeField] private GameObject doneTag;
        [SerializeField] private GameObject emIcon;

        [Header("색 · 크기 (HUD v2)")]
        [SerializeField] private Color unavailableFill = Color.white;
        [SerializeField] private Color unavailableLabel = new Color32(0xc4, 0xba, 0xb0, 0xFF);
        [SerializeField] private Color unavailableShadow = new Color32(0xdd, 0xd3, 0xc8, 0xFF);
        [SerializeField] private Color readyFill = new Color32(0xff, 0x4f, 0x8b, 0xFF);
        [SerializeField] private Color readyLabel = Color.white;
        [SerializeField] private Color readyShadow = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color pipOff = Color.white;
        [SerializeField] private Color pipOn = new Color32(0xff, 0x4f, 0x8b, 0xFF);
        [SerializeField] private float unavailableLabelSize = 68f;
        [SerializeField] private float readyLabelSize = 76f;
        [Tooltip("선언 뒤 흰 글자 둘레 잉크 외곽선 두께 (TMP outline, 0~1).")]
        [SerializeField] private float readyOutline = 0.25f;

        private void Awake()
        {
            if (button == null || fill == null || tailFill == null || shadow == null || label == null || pips == null || pips.Length != PipCount || pipsRow == null
                || overText == null || readyTag == null || doneTag == null || emIcon == null)
                Debug.LogError($"[{nameof(ChapchuButtonView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>상태 · 손패 수. 점은 handCount 개까지 켜지고, 10 을 넘으면 「+n」.</summary>
        public void SetState(ChapchuButtonState state, int handCount, int requiredCount)
        {
            bool shout = state == ChapchuButtonState.Declared;

            fill.color = shout ? readyFill : unavailableFill;
            tailFill.color = fill.color;
            label.color = shout ? readyLabel : unavailableLabel;
            label.fontSize = shout ? readyLabelSize : unavailableLabelSize;
            label.outlineWidth = shout ? readyOutline : 0f;
            label.outlineColor = readyShadow;
            shadow.color = shout ? readyShadow : unavailableShadow;
            shadow.gameObject.SetActive(!shout);
            button.interactable = false; // 입력이 아니다 — 선언은 자동 (10-ui.md 7절)

            pipsRow.SetActive(!shout);
            readyTag.SetActive(false);
            doneTag.SetActive(shout);
            emIcon.SetActive(false);

            for (int i = 0; i < pips.Length; i++)
                pips[i].color = i < handCount ? pipOn : pipOff;
            overText.gameObject.SetActive(handCount > requiredCount);
            overText.text = string.Format(OverFormat, handCount - requiredCount);
        }
    }
}
