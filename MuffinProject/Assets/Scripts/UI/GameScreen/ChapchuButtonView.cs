using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>찹츄 버튼의 표시 상태 (07-win-condition.md 4.1 · 10-ui.md 7절). 판정은 방장이 한다.</summary>
    public enum ChapchuButtonState
    {
        Unavailable, // 손패가 10장이 아니다 — 점 10개로 진행도
        Ready,       // 10장 — 누를 수 있다
        Declared,    // 선언했다 — 다음 내 턴까지 10장 지키기
    }

    /// <summary>
    /// 우하단 찹츄 버튼 = 외치는 말풍선 (10-ui.md 2-1 HUD v2 「찹츄 버튼」). 비활성: 회색 「찹츄!」 + 점 10개(손패 수). 10장: 분홍 · 흰 글자 잉크 외곽선 · 「지금 외치기!」 · 흔들림.
    /// 선언 뒤: 분홍 · 「찹츄 완료」. 값을 받아 표시하고 탭을 이벤트로만 알린다.
    /// </summary>
    public class ChapchuButtonView : MonoBehaviour
    {
        private const int PipCount = 10; // 07-win-condition.md 4.1
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
        [Tooltip("10장일 때 흰 글자 둘레 잉크 외곽선 두께 (TMP outline, 0~1).")]
        [SerializeField] private float readyOutline = 0.25f;
        [Tooltip("10장일 때 좌우로 흔들리는 각도 · 주기(초).")]
        [SerializeField] private float wobbleAngle = 3f;
        [SerializeField] private float wobbleDuration = 1.2f;

        private Tween _wobble;

        /// <summary>찹츄 버튼을 눌렀다 — 비활성 모양일 때도 알린다 (안내 토스트, 10-ui.md 「찹츄 버튼 상태」). 요청할지 안내할지는 프레젠터가 정한다.</summary>
        public event Action ChapchuClicked;

        private void Awake()
        {
            if (button == null || fill == null || tailFill == null || shadow == null || label == null || pips == null || pips.Length != PipCount || pipsRow == null
                || overText == null || readyTag == null || doneTag == null || emIcon == null)
                Debug.LogError($"[{nameof(ChapchuButtonView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            button.onClick.AddListener(HandleClicked);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(HandleClicked);
            StopWobble();
        }

        /// <summary>상태 · 손패 수. 점은 handCount 개까지 켜지고, 10 을 넘으면 「+n」.</summary>
        public void SetState(ChapchuButtonState state, int handCount, int requiredCount)
        {
            bool shout = state != ChapchuButtonState.Unavailable;

            fill.color = shout ? readyFill : unavailableFill;
            tailFill.color = fill.color;
            label.color = shout ? readyLabel : unavailableLabel;
            label.fontSize = shout ? readyLabelSize : unavailableLabelSize;
            label.outlineWidth = shout ? readyOutline : 0f;
            label.outlineColor = readyShadow;
            shadow.color = shout ? readyShadow : unavailableShadow;
            shadow.gameObject.SetActive(state != ChapchuButtonState.Declared);
            button.interactable = state != ChapchuButtonState.Declared; // 비활성 모양도 탭은 받는다 — 안내 토스트

            pipsRow.SetActive(!shout);
            readyTag.SetActive(state == ChapchuButtonState.Ready);
            doneTag.SetActive(state == ChapchuButtonState.Declared);
            emIcon.SetActive(state == ChapchuButtonState.Ready);

            for (int i = 0; i < pips.Length; i++)
                pips[i].color = i < handCount ? pipOn : pipOff;
            overText.gameObject.SetActive(handCount > requiredCount);
            overText.text = string.Format(OverFormat, handCount - requiredCount);

            if (state == ChapchuButtonState.Ready)
                StartWobble();
            else
                StopWobble();
        }

        private void StartWobble()
        {
            if (_wobble != null) return;

            transform.localRotation = Quaternion.Euler(0f, 0f, -wobbleAngle);
            _wobble = transform.DOLocalRotate(new Vector3(0f, 0f, wobbleAngle), wobbleDuration / 2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        private void StopWobble()
        {
            _wobble?.Kill();
            _wobble = null;
            transform.localRotation = Quaternion.identity;
        }

        private void HandleClicked() => ChapchuClicked?.Invoke();
    }
}
