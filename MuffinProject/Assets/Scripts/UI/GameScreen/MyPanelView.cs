using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>찹츄 버튼의 표시 상태 (07-win-condition.md 4.1 · 10-ui.md 7절). 판정은 방장이 한다.</summary>
    public enum ChapchuButtonState
    {
        Unavailable, // 손패가 10장이 아니다
        Ready,       // 10장 — 누를 수 있다
        Declared,    // 선언했다
    }

    /// <summary>
    /// 좌하단 내 패널 — 이름 · HP · 턴 · 남은 초 · 손패 수 · 찹츄 버튼 (10-ui.md 2-1 「내 패널」).
    /// 값을 받아 표시하고 버튼 탭을 이벤트로만 알린다. 남은 초는 프레젠터가 매 프레임 계산해 넘긴다.
    /// </summary>
    public class MyPanelView : MonoBehaviour
    {
        private const string NicknameFormat = "|{0}";
        private const string HpFormat = "HP {0}";
        private const string HandCountFormat = "{0}/{1}장";
        private const string ChapchuUnavailableFormat = "{0} / {1}장";
        private const string ChapchuReadyText = "지금 외치기!";
        private const string ChapchuDeclaredText = "찹츄 완료";
        private const string MyTurnText = "나의 턴!";
        private const string OtherTurnText = "상대 턴!";

        [Header("헤더")]
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private GameObject chapchuBadge;

        [Header("HP · 턴")]
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private Image turnTag;
        [SerializeField] private TMP_Text turnTagText;
        [SerializeField] private Color myTurnTagColor = new Color32(0xF5, 0x4A, 0x18, 0xFF);
        [SerializeField] private Color otherTurnTagColor = new Color32(0x0A, 0x9C, 0xFF, 0xFF);
        [Tooltip("남은 초 묶음(숫자 · 초). 내 턴에만 보인다.")]
        [SerializeField] private GameObject timer;
        [SerializeField] private TMP_Text secondsText;
        [Tooltip("남은 초가 이 값 이하면 숫자를 경고 색으로 (시안: 5).")]
        [SerializeField] private int urgentSeconds = 5;
        [SerializeField] private Color secondsColor = new Color32(0x11, 0x11, 0x11, 0xFF);
        [SerializeField] private Color urgentSecondsColor = new Color32(0xF5, 0x4A, 0x18, 0xFF);
        [SerializeField] private TMP_Text handCountText;

        [Header("찹츄 버튼")]
        [SerializeField] private Button chapchuButton;
        [SerializeField] private Image chapchuFill;
        [SerializeField] private TMP_Text chapchuLabel;
        [SerializeField] private Image chapchuTag;
        [SerializeField] private TMP_Text chapchuTagText;
        [SerializeField] private GameObject chapchuRing;
        [SerializeField] private Image chapchuShadow;
        [SerializeField] private Color readyFillColor = new Color32(0xF5, 0x4A, 0x18, 0xFF);
        [SerializeField] private Color readyInkColor = Color.white;
        [SerializeField] private Color readyTagFillColor = Color.white;
        [SerializeField] private Color readyTagInkColor = new Color32(0xF5, 0x4A, 0x18, 0xFF);
        [SerializeField] private Color readyShadowColor = new Color32(0x11, 0x11, 0x11, 0xFF);
        [SerializeField] private Color unavailableFillColor = Color.white;
        [SerializeField] private Color unavailableInkColor = new Color32(0x9A, 0xA3, 0xAD, 0xFF);
        [SerializeField] private Color unavailableTagFillColor = new Color32(0xE6, 0xE9, 0xED, 0xFF);
        [SerializeField] private Color unavailableTagInkColor = new Color32(0x6B, 0x74, 0x80, 0xFF);
        [SerializeField] private Color unavailableShadowColor = new Color32(0x9A, 0xA3, 0xAD, 0xFF);

        /// <summary>찹츄 버튼을 눌렀다. 누를 수 있는지는 서버가 거절로 알려준다.</summary>
        public event Action ChapchuClicked;

        private void Awake()
        {
            if (nicknameText == null || hpText == null || hpFill == null || turnTag == null || turnTagText == null || timer == null || secondsText == null || handCountText == null || chapchuButton == null)
                Debug.LogError($"[{nameof(MyPanelView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            chapchuButton.onClick.AddListener(HandleChapchuClicked);
        }

        private void OnDisable()
        {
            chapchuButton.onClick.RemoveListener(HandleChapchuClicked);
        }

        public void SetNickname(string nickname) => nicknameText.text = string.Format(NicknameFormat, nickname);

        public void SetHp(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            hpFill.anchorMax = new Vector2(ratio, 1f);
            hpText.text = string.Format(HpFormat, hp);
        }

        public void SetHandCount(int count, int max) => handCountText.text = string.Format(HandCountFormat, count, max);

        /// <summary>턴 태그 — 내 턴 「나의 턴!」(주황) · 남의 턴 「상대 턴!」(파랑). 남은 초는 내 턴에만 보인다 (10-ui.md 2-1 턴 줄 · 03-turn.md 7절).</summary>
        public void SetMyTurn(bool isMyTurn)
        {
            turnTag.color = isMyTurn ? myTurnTagColor : otherTurnTagColor;
            turnTagText.text = isMyTurn ? MyTurnText : OtherTurnText;
            timer.SetActive(isMyTurn);
        }

        /// <summary>턴 남은 초. 프레젠터가 마감 시각에서 계산해 넘긴다 (09-network.md 8절).</summary>
        public void SetRemainingSeconds(int seconds)
        {
            secondsText.text = seconds.ToString();
            secondsText.color = seconds <= urgentSeconds ? urgentSecondsColor : secondsColor;
        }

        /// <summary>찹츄 버튼 · 배지 상태. handCount · requiredCount 는 비활성 태그 문구("n / 10장")에 쓴다.</summary>
        public void SetChapchu(ChapchuButtonState state, int handCount, int requiredCount)
        {
            bool ready = state != ChapchuButtonState.Unavailable;

            chapchuFill.color = ready ? readyFillColor : unavailableFillColor;
            chapchuLabel.color = ready ? readyInkColor : unavailableInkColor;
            chapchuTag.color = ready ? readyTagFillColor : unavailableTagFillColor;
            chapchuTagText.color = ready ? readyTagInkColor : unavailableTagInkColor;
            chapchuShadow.color = ready ? readyShadowColor : unavailableShadowColor;
            chapchuRing.SetActive(ready);
            chapchuBadge.SetActive(state == ChapchuButtonState.Declared);
            chapchuButton.interactable = state == ChapchuButtonState.Ready;

            chapchuTagText.text = state switch
            {
                ChapchuButtonState.Ready => ChapchuReadyText,
                ChapchuButtonState.Declared => ChapchuDeclaredText,
                _ => string.Format(ChapchuUnavailableFormat, handCount, requiredCount),
            };
        }

        private void HandleChapchuClicked() => ChapchuClicked?.Invoke();
    }
}
